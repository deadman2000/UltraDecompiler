using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation;

public partial class ExpressionBuilder
{
    /// <summary>
    /// Булевы версии And/Or/Not для построения условий переходов.
    /// Выполняют агрессивное упрощение, когда один или оба операнда — константы (0/1).
    /// Используются в BuildJumpCondition.
    /// </summary>
    private static Expr BoolAnd(Expr a, Expr b)
    {
        if (a is ConstExpr ca)
        {
            if (ca.Value == 0) return ConstExpr.Zero;
            if (ca.Value != 0) return b;
        }
        if (b is ConstExpr cb)
        {
            if (cb.Value == 0) return ConstExpr.Zero;
            if (cb.Value != 0) return a;
        }
        return Calculate(Math2Operation.And, a, b);
    }

    private static Expr BoolOr(Expr a, Expr b)
    {
        if (a is ConstExpr ca)
        {
            if (ca.Value != 0) return ConstExpr.One;
            if (ca.Value == 0) return b;
        }
        if (b is ConstExpr cb)
        {
            if (cb.Value != 0) return ConstExpr.One;
            if (cb.Value == 0) return a;
        }
        return Calculate(Math2Operation.Or, a, b);
    }

    private static Expr BoolNot(Expr e)
    {
        if (e is ConstExpr c)
        {
            return c.Value == 0 ? ConstExpr.One : ConstExpr.Zero;
        }

        // Инверсия известных сравнений — даёт более чистые условия
        if (e is CmpExpr cmp)
        {
            var invertedOp = cmp.Operation switch
            {
                CmpOperation.Eq => CmpOperation.Ne,
                CmpOperation.Ne => CmpOperation.Eq,
                CmpOperation.Ult => CmpOperation.Uge,
                CmpOperation.Uge => CmpOperation.Ult,
                CmpOperation.Ule => CmpOperation.Ugt,
                CmpOperation.Ugt => CmpOperation.Ule,
                _ => (CmpOperation?)null
            };

            if (invertedOp.HasValue)
            {
                return new CmpExpr(invertedOp.Value, cmp.Left, cmp.Right);
            }
        }

        return Calculate(Math1Operation.Not, e);
    }

    /// <summary>
    /// Строит символическое выражение эффективного адреса (offset) для memory-операнда.
    /// Сегмент здесь не учитывается — LEA и подобные операции работают только с offset-частью.
    /// </summary>
    private Expr GetEffectiveAddress(Operand operand, in RegisterExpressions registers, Segment segmentOverride = Segment.None)
    {
        Expr? addr = null;

        if (operand.BaseReg != AddressRegister.None)
            addr = registers.Get16((int)operand.BaseReg);

        if (operand.IndexReg != AddressRegister.None)
        {
            var idx = registers.Get16((int)operand.IndexReg);
            addr = addr == null ? idx : Calculate(Math2Operation.Add, addr, idx);
        }

        if (operand.Value != 0 || addr == null)
        {
            var disp = new ConstExpr(operand.Value);
            addr = addr == null ? disp : Calculate(Math2Operation.Add, addr, disp);
        }

        return addr ?? ConstExpr.Zero;
    }

    /// <summary>
    /// Преобразует Operand (из дизассемблера) в символическое выражение (Expr).
    /// 
    /// Это центральная точка, где мы "поднимаем" низкоуровневые операнды
    /// в наше высокоуровневое представление.
    /// 
    /// Поддерживаемые типы:
    /// - Immediate → ConstExpr
    /// - Регистры (8/16, сегментные) → текущее символическое значение из RegisterExpressions
    /// - Memory → MemExpr(адрес, сегмент). 
    ///   Сегмент заполняется либо из явного префикса (ES:/CS:/SS:/DS:), либо по умолчанию
    ///   (BP-адресация → SS, всё остальное → DS).
    /// </summary>
    private Expr GetExpression(Operand operand, in RegisterExpressions registers, Segment segmentOverride = Segment.None)
    {
        if (operand.Type == OperandType.Immediate8 || operand.Type == OperandType.Immediate16)
            return new ConstExpr(operand.Value);

        if (operand.Type == OperandType.Register16)
        {
            // Возвращает либо ранее сохранённое выражение (Variable/MathExpr),
            // либо ConstExpr.Zero, если значение регистра ещё неизвестно.
            return registers.Get16(operand.Value);
        }

        if (operand.Type == OperandType.Register8)
        {
            return registers.Get8(operand.Value);
        }

        if (operand.Type == OperandType.SegmentRegister)
        {
            return registers.GetSegment(operand.Value);
        }

        if (operand.Type == OperandType.Memory)
        {
            var (address, segExpr) = BuildMemoryReference(operand, registers, segmentOverride);

            // Пытаемся распознать доступ к известной структуре в памяти (PSP и т.п.)
            var knownVar = Variables.TryGetKnownMemoryVariable(address, segExpr);
            if (knownVar != null)
                return knownVar;

            return new MemExpr(address, segExpr);
        }

        throw new NotImplementedException($"Unsupported operand type: {operand.Type}");
    }

    /// <summary>
    /// Строит описание адреса памяти (address + segment) для операнда Memory.
    /// Используется как для загрузок (MemExpr), так и для записей (StoreOperation).
    /// </summary>
    private (Expr Address, Expr? Segment) BuildMemoryReference(
        Operand operand,
        in RegisterExpressions registers,
        Segment segmentOverride)
    {
        var address = GetEffectiveAddress(operand, registers, segmentOverride);

        Expr? segExpr;

        if (segmentOverride != Segment.None)
        {
            int segIdx = segmentOverride switch
            {
                Segment.ES => 0,
                Segment.CS => 1,
                Segment.SS => 2,
                Segment.DS => 3,
                _ => throw new NotImplementedException($"Segment override {segmentOverride} is not supported")
            };
            segExpr = registers.GetSegment(segIdx);
        }
        else
        {
            bool usesStackSegment = operand.BaseReg == AddressRegister.BP ||
                                    operand.IndexReg == AddressRegister.BP;

            int defaultSegIdx = usesStackSegment ? 2 : 3; // SS : DS
            segExpr = registers.GetSegment(defaultSegIdx);
        }

        return (address, segExpr);
    }

    /// <summary>
    /// Формирование математического выражения или вычисленного константного значения (constant folding).
    /// Если оба операнда — константы, вычисляет результат сразу как ConstExpr.
    /// Иначе возвращает Math2Expr.
    /// </summary>
    private static Expr Calculate(Math2Operation op, Expr first, Expr second)
    {
        if (first is ConstExpr c1 && second is ConstExpr c2)
        {
            var value = op switch
            {
                Math2Operation.Add => c1.Value + c2.Value,
                Math2Operation.Sub => c1.Value - c2.Value,
                Math2Operation.Shl => c1.Value << c2.Value,
                Math2Operation.Shr => c1.Value >> c2.Value,
                Math2Operation.And => c1.Value & c2.Value,
                Math2Operation.Or => c1.Value | c2.Value,
                Math2Operation.Xor => c1.Value ^ c2.Value,
                _ => throw new NotImplementedException($"Unsupported Math2Operation in folding: {op}")
            };
            return new ConstExpr(value);
        }

        return new Math2Expr(op, first, second);
    }

    /// <summary>
    /// Унарная версия Calculate с constant folding для Not/Neg.
    /// </summary>
    private static Expr Calculate(Math1Operation op, Expr operand)
    {
        if (operand is ConstExpr c)
        {
            var value = op switch
            {
                Math1Operation.Neg => -c.Value,
                Math1Operation.Not => ~c.Value,
                _ => throw new NotImplementedException($"Unsupported Math1Operation in folding: {op}")
            };
            return new ConstExpr(value);
        }

        return new Math1Expr(op, operand);
    }

    /// <summary>
    /// Применяет обновление флагов после арифметической/логической операции.
    /// Устанавливает только ZF = (resultExpr == 0).
    ///
    /// Для ADD/SUB CF устанавливается напрямую в HandleArithmetic (более точная информация).
    /// Для INC/DEC CF намеренно не трогается (согласно x86).
    /// </summary>
    private static RegisterExpressions ApplyArithmeticFlags(RegisterExpressions regs, Expr resultExpr)
    {
        return regs with
        {
            ZF = new CmpExpr(CmpOperation.Eq, resultExpr, ConstExpr.Zero)
            // CF, SF, OF оставляем как есть
        };
    }
}
