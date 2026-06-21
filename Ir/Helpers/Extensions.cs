namespace UltraDecompiler.Ir.Helpers;

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
        /// (Eq-Ne, Ult-Uge и т.д.). Даёт более чистые условия.
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
                    Math2Operation.Mul => c1.Value * c2.Value,
                    Math2Operation.Div => c1.Value / c2.Value,
                    Math2Operation.Mod => c1.Value % c2.Value,
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

    extension(Operand operand)
    {
        /// <summary>
        /// Преобразует Operand (из дизассемблера) в символическое выражение (Expr).
        /// </summary>
        public Expr GetExpression(ExprBlock block, Segment segmentOverride = Segment.None)
        {
            switch (operand.Type)
            {
                case OperandType.Immediate8:
                case OperandType.Immediate16:
                    if (operand.Relocation is not null)
                        return new ImageOffsetExpr(operand.Relocation, operand.Value);
                    return new ConstExpr(operand.Value);
                case OperandType.Register16:
                    // Возвращает либо ранее сохранённое выражение (Variable/MathExpr),
                    // либо ConstExpr.Zero, если значение регистра ещё неизвестно.
                    return block.Variables.Get(operand.AsGpRegister16()).ToGet();
                case OperandType.Register8:
                    return block.Variables.Get(operand.AsGpRegister8());
                case OperandType.SegmentRegister:
                    return block.Variables.Get(operand.AsCpuSegmentRegister()).ToGet();
                case OperandType.Memory:
                    {
                        if (operand.BaseReg == AddressRegister.BP &&
                            operand.IndexReg == AddressRegister.None)
                        {
                            // Сначала параметры (argN), потом локальные переменные (varN) по [BP+disp]
                            var slot = block.Variables.TryGetStackParameter(operand.Value)
                                       ?? block.Variables.TryGetStackLocal(operand.Value);
                            if (slot != null)
                                return slot.ToGet();
                        }

                        var (address, segExpr) = operand.BuildMemoryReference(block, segmentOverride);

                        // Пытаемся распознать доступ к известной структуре в памяти (PSP и т.п.)
                        var knownVar = block.Variables.TryGetKnownMemoryVariable(address, segExpr);
                        if (knownVar != null)
                            return knownVar.ToGet();

                        return new MemExpr(address, segExpr);
                    }

                default:
                    throw new NotImplementedException($"Unsupported operand type: {operand.Type}");
            }
        }

        /// <summary>
        /// Строит символическое выражение эффективного адреса (offset) для memory-операнда.
        /// Сегмент здесь не учитывается — LEA и подобные операции работают только с offset-частью.
        /// </summary>
        public Expr GetEffectiveAddress(ExprBlock block, Segment segmentOverride = Segment.None)
        {
            Expr? addr = null;

            if (operand.BaseReg != AddressRegister.None)
                addr = block.Variables.Get((GpRegister16)operand.BaseReg).ToGet();

            if (operand.IndexReg != AddressRegister.None)
            {
                var idx = block.Variables.Get((GpRegister16)operand.IndexReg).ToGet();
                addr = addr == null ? idx : addr.Calculate(Math2Operation.Add, idx);
            }

            if (operand.Value != 0 || addr == null)
            {
                Expr disp = operand.Relocation is not null
                    ? new ImageOffsetExpr(operand.Relocation, operand.Value)
                    : new ConstExpr(operand.Value);
                addr = addr == null ? disp : addr.Calculate(Math2Operation.Add, disp);
            }

            return addr ?? ConstExpr.Zero;
        }

        /// <summary>
        /// Строит описание адреса памяти (address + segment) для операнда Memory.
        /// Используется как для загрузок (MemExpr), так и для записей (StoreOperation).
        /// </summary>
        public (Expr Address, Expr? Segment) BuildMemoryReference(ExprBlock block, Segment segmentOverride)
        {
            var address = operand.GetEffectiveAddress(block, segmentOverride);

            Expr? segExpr;

            if (segmentOverride != Segment.None)
            {
                segExpr = block.Variables.Get(segmentOverride.ToCpuSegmentRegister()).ToGet();
            }
            else
            {
                bool usesStackSegment = operand.BaseReg == AddressRegister.BP ||
                                        operand.IndexReg == AddressRegister.BP;

                segExpr = block.Variables.Get(usesStackSegment ? CpuSegmentRegister.SS : CpuSegmentRegister.DS).ToGet();
            }

            return (address, segExpr);
        }

        /// <summary>
        /// Эмитит инкремент или декремент операнда.
        /// Для локальной переменной по [BP+disp] создаёт <see cref="IncOperation"/> / <see cref="DecOperation"/>.
        /// Иначе — операцию с адресом памяти.
        /// </summary>
        public void EmitIncDec(ExprBlock block, Segment segmentOverride, bool isInc)
        {
            if (operand.Type != OperandType.Memory)
                throw new InvalidOperationException("EmitIncDec может вызываться только для memory-операнда");

            if (operand.BaseReg == AddressRegister.BP && operand.IndexReg == AddressRegister.None)
            {
                var local = block.Variables.TryGetStackLocal(operand.Value);
                if (local != null)
                {
                    block.Operations.Add(isInc ? new IncOperation(local.ToSet()) : new DecOperation(local.ToSet()));
                    return;
                }

                var param = block.Variables.TryGetStackParameter(operand.Value);
                if (param != null)
                {
                    block.Operations.Add(isInc ? new IncOperation(param.ToSet()) : new DecOperation(param.ToSet()));
                    return;
                }
            }

            var (addr, seg) = operand.BuildMemoryReference(block, segmentOverride);
            block.Operations.Add(isInc ? new IncOperation(addr, seg) : new DecOperation(addr, seg));
        }
        /// <summary>
        /// Эмитит составное присваивание <c>target += value</c> / <c>target -= value</c>
        /// (QuickC: <c>add/sub [mem], imm|reg</c>).
        /// </summary>
        public bool TryEmitCompoundAssign(
            ExprBlock block,
            Segment segmentOverride,
            bool isAdd,
            Expr value,
            Expr currentValue,
            out Expr result)
        {
            result = null!;

            if (operand.Type != OperandType.Memory)
            {
                return false;
            }

            result = isAdd
                ? currentValue.Calculate(Math2Operation.Add, value)
                : currentValue.Calculate(Math2Operation.Sub, value);

            if (operand.BaseReg == AddressRegister.BP && operand.IndexReg == AddressRegister.None)
            {
                var local = block.Variables.TryGetStackLocal(operand.Value);
                if (local != null)
                {
                    block.Operations.Add(isAdd
                        ? new AddAssignOperation(local.ToSet(), value)
                        : new SubAssignOperation(local.ToSet(), value));
                    return true;
                }

                var param = block.Variables.TryGetStackParameter(operand.Value);
                if (param != null)
                {
                    block.Operations.Add(isAdd
                        ? new AddAssignOperation(param.ToSet(), value)
                        : new SubAssignOperation(param.ToSet(), value));
                    return true;
                }
            }

            var (addr, seg) = operand.BuildMemoryReference(block, segmentOverride);
            block.Operations.Add(isAdd
                ? new AddAssignOperation(addr, value, seg)
                : new SubAssignOperation(addr, value, seg));
            return true;
        }

        /// <summary>
        /// Эмитит запись в память по операнду.
        /// Если это обращение к локальной переменной по [BP + отрицательное_смещение],
        /// создаёт SetOperation на соответствующую Variable (поддержка локалов).
        /// Иначе создаёт StoreOperation (для глобалов, параметров, [bx+si] и т.д.).
        /// </summary>
        public void EmitStore(ExprBlock block, Segment segmentOverride, Expr value)
        {
            if (operand.Type != OperandType.Memory)
                throw new InvalidOperationException("EmitStore может вызываться только для memory-операнда");

            if (operand.BaseReg == AddressRegister.BP && operand.IndexReg == AddressRegister.None)
            {
                var local = block.Variables.TryGetStackLocal(operand.Value);
                if (local != null)
                {
                    block.Operations.Add(new SetOperation(local.ToSet(), value));
                    return;
                }

                var param = block.Variables.TryGetStackParameter(operand.Value);
                if (param != null)
                {
                    block.Operations.Add(new SetOperation(param.ToSet(), value));
                    return;
                }
            }

            var (addr, seg) = operand.BuildMemoryReference(block, segmentOverride);
            block.Operations.Add(new StoreOperation(addr, seg, value));
        }

        /// <summary>
        /// Проверяет, указывают ли два операнда на одно и то же место
        /// (один и тот же регистр или одна и та же ячейка памяти).
        /// Используется для оптимизации XOR reg,reg > 0 и SUB reg,reg > 0.
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

    extension(GpRegister8 reg)
    {
        /// <summary>
        /// Возвращает соответствующий 16-битный регистр для 8-битного.
        /// AL/AH -> AX, BL/BH -> BX, CL/CH -> CX, DL/DH -> DX.
        /// </summary>
        public GpRegister16 ToGpRegister16() => reg switch
        {
            GpRegister8.AL or GpRegister8.AH => GpRegister16.AX,
            GpRegister8.BL or GpRegister8.BH => GpRegister16.BX,
            GpRegister8.CL or GpRegister8.CH => GpRegister16.CX,
            GpRegister8.DL or GpRegister8.DH => GpRegister16.DX,
            _ => throw new ArgumentOutOfRangeException(nameof(reg), reg, null)
        };
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
                seg = block.Variables.Get(instr.Segment.ToCpuSegmentRegister()).ToGet();
            }

            return new MemExpr(addr, seg);
        }

        /// <summary>
        /// Возвращает адрес и сегмент для строки (DI+ES или SI+DS).
        /// </summary>
        public (Expr Address, Expr? Segment) BuildStringMemoryAddress(bool isDestination)
        {
            Expr ptr = isDestination
                ? block.Variables.Get(GpRegister16.DI).ToGet()
                : block.Variables.Get(GpRegister16.SI).ToGet();

            Expr? seg = isDestination
                ? block.Variables.Get(CpuSegmentRegister.ES).ToGet()
                : block.Variables.Get(CpuSegmentRegister.DS).ToGet();

            return (ptr, seg);
        }
    }
}
