using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation;

public partial class ExpressionBuilder
{
    private static Expr Negate(Expr e) => Calculate(Math1Operation.Not, e);
    private static Expr And(Expr a, Expr b) => Calculate(Math2Operation.And, a, b);
    private static Expr Or(Expr a, Expr b) => Calculate(Math2Operation.Or, a, b);

    private static Expr GetFlagOrTrue(Expr? flagExpr) => flagExpr ?? ConstExpr.One; // fallback (всегда "истина" если флаг неизвестен)

    /// <summary>
    /// Строит символическое условие для взятия ConditionalBlock по Jcc-инструкции
    /// и текущему состоянию флагов (из EndRegisters).
    /// </summary>
    private static Expr BuildJumpCondition(Instruction jumpInstr, RegisterExpressions registers)
    {
        var zf = GetFlagOrTrue(registers.ZF);
        var cf = GetFlagOrTrue(registers.CF);
        var sf = GetFlagOrTrue(registers.SF);
        var of = GetFlagOrTrue(registers.OF);

        // SF == OF  (используем эквивалент XOR: (a&b) | (!a & !b) )
        var sfEqOf = Or(And(sf, of), And(Negate(sf), Negate(of)));

        return jumpInstr.Mnemonic switch
        {
            // Равенство (лучше всего поддерживается после CMP/TEST)
            Mnemonic.JE => zf,
            Mnemonic.JNE => Negate(zf),

            // Беззнаковые сравнения
            Mnemonic.JB => cf,
            Mnemonic.JAE => Negate(cf),

            Mnemonic.JBE => Or(cf, zf),
            Mnemonic.JA => And(Negate(cf), Negate(zf)),

            // Знаковый бит
            Mnemonic.JS => sf,
            Mnemonic.JNS => Negate(sf),

            // Знаковые сравнения
            Mnemonic.JL => Negate(sfEqOf),
            Mnemonic.JGE => sfEqOf,
            Mnemonic.JLE => Or(zf, Negate(sfEqOf)),
            Mnemonic.JG => And(Negate(zf), sfEqOf),

            // Переполнение
            Mnemonic.JO => of,
            Mnemonic.JNO => Negate(of),

            // Чётность (редко используется в высокоуровневом коде)
            Mnemonic.JP => zf, // заглушка (PF не отслеживаем)
            Mnemonic.JNP => Negate(zf),

            // Специальные (CX-based) — упрощённо
            Mnemonic.JCXZ => ConstExpr.Zero, // TODO: CX == 0

            // Циклы — упрощённо
            Mnemonic.LOOP => Negate(zf),
            Mnemonic.LOOPE => zf,
            Mnemonic.LOOPNE => Negate(zf),

            _ => ConstExpr.One
        };
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

        Expr? segExpr = null;

        if (segmentOverride != Segment.None)
        {
            int segIdx = segmentOverride switch
            {
                Segment.ES => 0,
                Segment.CS => 1,
                Segment.SS => 2,
                Segment.DS => 3,
                _ => -1
            };
            if (segIdx >= 0)
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
                Math2Operation.Or  => c1.Value | c2.Value,
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
}
