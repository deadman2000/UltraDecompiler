using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation;

public static class Extensions
{
    extension(Expr expr)
    {
        /// <summary>
        /// Булево И с агрессивным constant folding (0/1) и упрощением Cmp.
        /// Предпочтительный способ использования — через перегрузку оператора &amp;.
        /// </summary>
        public Expr BoolAnd(Expr other)
        {
            if (expr is ConstExpr ca)
            {
                if (ca.Value == 0) return ConstExpr.Zero;
                if (ca.Value != 0) return other;
            }
            if (other is ConstExpr cb)
            {
                if (cb.Value == 0) return ConstExpr.Zero;
                if (cb.Value != 0) return expr;
            }
            return expr.Calculate(Math2Operation.And, other);
        }

        /// <summary>
        /// Булево ИЛИ с агрессивным constant folding (0/1) и упрощением Cmp.
        /// Предпочтительный способ использования — через перегрузку оператора |.
        /// </summary>
        public Expr BoolOr(Expr other)
        {
            if (expr is ConstExpr ca)
            {
                if (ca.Value != 0) return ConstExpr.One;
                if (ca.Value == 0) return other;
            }
            if (other is ConstExpr cb)
            {
                if (cb.Value != 0) return ConstExpr.One;
                if (cb.Value == 0) return expr;
            }
            return expr.Calculate(Math2Operation.Or, other);
        }

        /// <summary>
        /// Булево НЕ с агрессивным constant folding и инверсией известных CmpExpr
        /// (Eq↔Ne, Ult↔Uge и т.д.). Даёт более чистые условия.
        /// Предпочтительный способ использования — через перегрузку оператора !.
        /// </summary>
        public Expr BoolNot()
        {
            if (expr is ConstExpr c)
            {
                return c.Value == 0 ? ConstExpr.One : ConstExpr.Zero;
            }

            // Инверсия известных сравнений — даёт более чистые условия
            if (expr is CmpExpr cmp)
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

            return expr.Calculate(Math1Operation.Not);
        }

        /// <summary>
        /// Булево XOR с constant folding. Полезно для моделирования SF ^ OF.
        /// Предпочтительный способ использования — через перегрузку оператора ^.
        /// </summary>
        public Expr BoolXor(Expr other)
        {
            if (expr is ConstExpr ca)
            {
                if (ca.Value == 0) return other;
                if (ca.Value != 0) return !other;
            }
            if (other is ConstExpr cb)
            {
                if (cb.Value == 0) return expr;
                if (cb.Value != 0) return !expr;
            }
            return expr.Calculate(Math2Operation.Xor, other);
        }

        public Expr LowByte()
        {
            if (expr is ConstExpr c)
                return new ConstExpr(c.Value & 0xff);
            return new Math2Expr(Math2Operation.And, expr, new ConstExpr(0xff));
        }

        public Expr HighByte()
        {
            if (expr is ConstExpr c)
                return new ConstExpr(c.Value >> 8);
            return new Math2Expr(Math2Operation.Shr, expr, new ConstExpr(8));
        }

        /// <summary>
        /// Формирование математического выражения или вычисленного константного значения (constant folding).
        /// Если оба операнда — константы, вычисляет результат сразу как ConstExpr.
        /// Иначе возвращает Math2Expr.
        /// </summary>
        public Expr Calculate(Math2Operation op, Expr second)
        {
            if (expr is ConstExpr c1 && second is ConstExpr c2)
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

            return new Math2Expr(op, expr, second);
        }

        /// <summary>
        /// Унарная версия Calculate с constant folding для Not/Neg.
        /// </summary>
        public Expr Calculate(Math1Operation op)
        {
            if (expr is ConstExpr c)
            {
                var value = op switch
                {
                    Math1Operation.Neg => -c.Value,
                    Math1Operation.Not => ~c.Value,
                    _ => throw new NotImplementedException($"Unsupported Math1Operation in folding: {op}")
                };
                return new ConstExpr(value);
            }

            return new Math1Expr(op, expr);
        }

    }

    extension(RegisterExpressions registers)
    {
        /// <summary>
        /// SF == OF (эквивалентность). Используется для JGE / JG / JLE.
        /// </summary>
        public Expr SfEqOf() => !(registers.SF ^ registers.OF);

        /// <summary>
        /// SF != OF. Используется для JL / JLE.
        /// </summary>
        public Expr SfNeOf() => registers.SF ^ registers.OF;

        /// <summary>
        /// Применяет обновление флагов после арифметической/логической операции.
        /// Устанавливает только ZF = (resultExpr == 0).
        ///
        /// Для ADD/SUB CF устанавливается напрямую в HandleArithmetic (более точная информация).
        /// Для INC/DEC CF намеренно не трогается (согласно x86).
        /// </summary>
        public RegisterExpressions ApplyArithmeticFlags(Expr resultExpr)
        {
            return registers with
            {
                ZF = new CmpExpr(CmpOperation.Eq, resultExpr, ConstExpr.Zero)
            };
        }
    }

    extension(Operand operand)
    {
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
        public Expr GetExpression(ExprBlock block, Segment segmentOverride = Segment.None)
        {
            if (operand.Type == OperandType.Immediate8 || operand.Type == OperandType.Immediate16)
                return new ConstExpr(operand.Value);

            if (operand.Type == OperandType.Register16)
            {
                // Возвращает либо ранее сохранённое выражение (Variable/MathExpr),
                // либо ConstExpr.Zero, если значение регистра ещё неизвестно.
                return block.EndRegisters.Get16(operand.Value);
            }

            if (operand.Type == OperandType.Register8)
            {
                return block.EndRegisters.Get8(operand.Value);
            }

            if (operand.Type == OperandType.SegmentRegister)
            {
                return block.EndRegisters.GetSegment(operand.Value);
            }

            if (operand.Type == OperandType.Memory)
            {
                var (address, segExpr) = operand.BuildMemoryReference(block.EndRegisters, segmentOverride);

                // Пытаемся распознать доступ к известной структуре в памяти (PSP и т.п.)
                var knownVar = block.Variables.TryGetKnownMemoryVariable(address, segExpr);
                if (knownVar != null)
                    return knownVar;

                return new MemExpr(address, segExpr);
            }

            throw new NotImplementedException($"Unsupported operand type: {operand.Type}");
        }

        /// <summary>
        /// Строит символическое выражение эффективного адреса (offset) для memory-операнда.
        /// Сегмент здесь не учитывается — LEA и подобные операции работают только с offset-частью.
        /// </summary>
        public Expr GetEffectiveAddress(in RegisterExpressions registers, Segment segmentOverride = Segment.None)
        {
            Expr? addr = null;

            if (operand.BaseReg != AddressRegister.None)
                addr = registers.Get16((int)operand.BaseReg);

            if (operand.IndexReg != AddressRegister.None)
            {
                var idx = registers.Get16((int)operand.IndexReg);
                addr = addr == null ? idx : addr.Calculate(Math2Operation.Add, idx);
            }

            if (operand.Value != 0 || addr == null)
            {
                var disp = new ConstExpr(operand.Value);
                addr = addr == null ? disp : addr.Calculate(Math2Operation.Add, disp);
            }

            return addr ?? ConstExpr.Zero;
        }

        /// <summary>
        /// Строит описание адреса памяти (address + segment) для операнда Memory.
        /// Используется как для загрузок (MemExpr), так и для записей (StoreOperation).
        /// </summary>
        public (Expr Address, Expr? Segment) BuildMemoryReference(in RegisterExpressions registers, Segment segmentOverride)
        {
            var address = operand.GetEffectiveAddress(registers, segmentOverride);

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
        /// Проверяет, указывают ли два операнда на одно и то же место
        /// (один и тот же регистр или одна и та же ячейка памяти).
        /// Используется для оптимизации XOR reg,reg → 0 и SUB reg,reg → 0.
        /// </summary>
        public bool ReferToSameLocation(Operand other)
        {
            if (operand.Type != other.Type)
                return false;

            return operand.Type switch
            {
                OperandType.Register8 or OperandType.Register16 => operand.Value == other.Value,
                OperandType.Memory => operand.BaseReg == other.BaseReg &&
                                      operand.IndexReg == other.IndexReg &&
                                      operand.Value == other.Value,
                _ => false
            };
        }
    }

    extension(ExprBlock block)
    {
        /// <summary>
        /// Строит MemExpr для чтения из памяти в контексте строковой инструкции.
        /// </summary>
        public Expr BuildStringMemoryRead(Instruction instr, bool isSource, int size)
        {
            var (addr, seg) = block.BuildStringMemoryAddress(!isSource);

            if (instr.Segment != Segment.None)
            {
                int segIdx = instr.Segment switch
                {
                    Segment.ES => 0,
                    Segment.CS => 1,
                    Segment.SS => 2,
                    Segment.DS => 3,
                    _ => 3
                };
                seg = block.EndRegisters.GetSegment(segIdx);
            }

            return new MemExpr(addr, seg);
        }

        /// <summary>
        /// Возвращает адрес и сегмент для строки (DI+ES или SI+DS).
        /// </summary>
        public (Expr Address, Expr? Segment) BuildStringMemoryAddress(bool isDestination)
        {
            Expr ptr = isDestination
                ? block.EndRegisters.Get16(7)  // DI
                : block.EndRegisters.Get16(6); // SI

            Expr? seg = isDestination
                ? block.EndRegisters.GetSegment(0) // ES
                : block.EndRegisters.GetSegment(3); // DS

            return (ptr, seg);
        }

        /// <summary>
        /// Обновляет SI и/или DI после строковой операции.
        /// </summary>
        public static void UpdateStringPointers(int size, bool updateSi = true, bool updateDi = true)
        {
            // Делегируем в существующую логику ExpressionBuilder
            // (пока что, чтобы не дублировать)
            // В будущем эта логика переедет сюда.
        }
    }
}
