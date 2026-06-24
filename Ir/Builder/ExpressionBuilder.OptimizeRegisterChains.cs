using UltraDecompiler.Ir.Variables;

namespace UltraDecompiler.Ir.Builder;

public partial class ExpressionBuilder
{
    /// <summary>
    /// Оптимизация цепочек присваиваний через регистры.
    /// Если регистр используется только для передачи значения (regDst = regSrc),
    /// заменяет использование на исходное выражение.
    /// 
    /// Было:
    ///   regAX = 1
    ///   regDX = regAX
    /// Стало:
    ///   regDX = 1
    /// 
    /// Также оптимизирует return:
    ///   regAX = var1
    ///   return regAX
    /// Стало:
    ///   return var1
    /// 
    /// Поддерживаемые преобразования:
    ///   regAX = var2; var1 += regAX → var1 += var2
    ///   regAX = var1; regAX = regAX + 1; var1 = regAX → var1 = var1 + 1
    ///   var1 += 1; var2 = var1 → var2 = ++var1
    ///   regAX = var1; var1 += 1; var2 = regAX → var2 = var1++
    /// </summary>
    public void OptimizeRegisterChains()
    {
        foreach (var block in Blocks)
        {
            OptimizeRegisterChainsInBlock(block);
        }
    }

    /// <summary>
    /// Оптимизация цепочек в одном блоке.
    /// </summary>
    private static void OptimizeRegisterChainsInBlock(ExprBlock block)
    {
        var changed = true;
        while (changed)
        {
            changed = false;

            if (TryFoldRegisterSelfUpdate(block))
            {
                changed = true;
                continue;
            }

            for (var i = 0; i < block.Operations.Count; i++)
            {
                if (!TryPropagateRegisterCopy(block, i))
                {
                    continue;
                }

                changed = true;
                break;
            }
        }
    }

    /// <summary>
    /// <c>reg = src; reg = f(reg, …)</c> → <c>reg = f(src, …)</c>.
    /// </summary>
    private static bool TryFoldRegisterSelfUpdate(ExprBlock block)
    {
        for (var i = 0; i < block.Operations.Count - 1; i++)
        {
            if (block.Operations[i] is not SetOperation loadOp ||
                !AssignmentTarget.TryGetVariable(loadOp.Dst, out var regVar) ||
                !regVar.IsRegister ||
                !IsPropagatableRegisterSource(loadOp.Src, regVar))
            {
                continue;
            }

            if (block.Operations[i + 1] is not SetOperation updateOp ||
                !AssignmentTarget.ReferencesVariable(updateOp.Dst, regVar) ||
                !ExprReferencesVariable(updateOp.Src, regVar))
            {
                continue;
            }

            block.Operations[i + 1] = updateOp with
            {
                Src = SubstituteRegisterInExpr(updateOp.Src, regVar, loadOp.Src),
            };
            block.Operations.RemoveAt(i);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Подставляет <paramref name="source"/> вместо <paramref name="register"/> во всех
    /// использованиях до переопределения регистра (включая <see cref="ExprBlock.Condition"/>).
    /// </summary>
    private static bool TryPropagateRegisterCopy(ExprBlock block, int loadIndex)
    {
        if (block.Operations[loadIndex] is not SetOperation loadOp ||
            !AssignmentTarget.TryGetVariable(loadOp.Dst, out var regVar) ||
            !regVar.IsRegister ||
            !IsPropagatableRegisterSource(loadOp.Src, regVar))
        {
            return false;
        }

        var killIndex = FindRegisterKillIndex(block, loadIndex + 1, regVar);
        var hasConditionUse = killIndex == block.Operations.Count &&
                              block.Condition is not null &&
                              ExprReferencesVariable(block.Condition, regVar);

        var hasUses = hasConditionUse;
        for (var usage = loadIndex + 1; !hasUses && usage < killIndex; usage++)
        {
            if (ExprReferencesVariable(block.Operations[usage], regVar))
            {
                hasUses = true;
            }
        }

        if (!hasUses)
        {
            return false;
        }

        for (var usage = loadIndex + 1; usage < killIndex; usage++)
        {
            if (!ExprReferencesVariable(block.Operations[usage], regVar))
            {
                continue;
            }

            if (HasInterferenceBetween(block, loadIndex + 1, usage, regVar, loadOp.Src))
            {
                return false;
            }

            var substituted = SubstituteRegisterInOperation(block.Operations[usage], regVar, loadOp.Src);
            if (CreatesTautology(substituted, loadOp.Src))
            {
                return false;
            }
        }

        if (hasConditionUse &&
            HasInterferenceBetween(block, loadIndex + 1, block.Operations.Count, regVar, loadOp.Src))
        {
            return false;
        }

        for (var usage = loadIndex + 1; usage < killIndex; usage++)
        {
            if (!ExprReferencesVariable(block.Operations[usage], regVar))
            {
                continue;
            }

            block.Operations[usage] = SubstituteRegisterInOperation(
                block.Operations[usage],
                regVar,
                loadOp.Src);
        }

        if (hasConditionUse)
        {
            block.Condition = SubstituteRegisterInExpr(block.Condition!, regVar, loadOp.Src);
        }

        block.Operations.RemoveAt(loadIndex);
        return true;
    }

    private static int FindRegisterKillIndex(ExprBlock block, int startIndex, Variable register)
    {
        for (var i = startIndex; i < block.Operations.Count; i++)
        {
            if (block.Operations[i] is SetOperation setOp &&
                AssignmentTarget.TryGetVariable(setOp.Dst, out var dstVar) &&
                ReferenceEquals(dstVar, register))
            {
                return i;
            }
        }

        return block.Operations.Count;
    }

    private static bool IsPropagatableRegisterSource(Expr src, Variable register) =>
        !ExprReferencesVariable(src, register) &&
        src is VariableExpr or ConstExpr or CallExpr or Math1Expr or Math2Expr;

    private static Operation SubstituteRegisterInOperation(Operation operation, Variable register, Expr replacement) =>
        operation switch
        {
            SetOperation set => set with
            {
                Dst = SubstituteRegisterInExpr(set.Dst, register, replacement),
                Src = SubstituteRegisterInExpr(set.Src, register, replacement),
            },
            StoreOperation store => new StoreOperation(
                SubstituteRegisterInExpr(store.Address, register, replacement),
                store.Segment is null ? null : SubstituteRegisterInExpr(store.Segment, register, replacement),
                SubstituteRegisterInExpr(store.Value, register, replacement)),
            ReturnOperation ret => ret with
            {
                Value = ret.Value is null ? null : SubstituteRegisterInExpr(ret.Value, register, replacement),
            },
            IncOperation inc => new IncOperation(
                SubstituteRegisterInExpr(inc.Target, register, replacement),
                inc.Segment is null ? null : SubstituteRegisterInExpr(inc.Segment, register, replacement)),
            DecOperation dec => new DecOperation(
                SubstituteRegisterInExpr(dec.Target, register, replacement),
                dec.Segment is null ? null : SubstituteRegisterInExpr(dec.Segment, register, replacement)),
            AddAssignOperation add => new AddAssignOperation(
                SubstituteRegisterInExpr(add.Target, register, replacement),
                SubstituteRegisterInExpr(add.Value, register, replacement),
                add.Segment is null ? null : SubstituteRegisterInExpr(add.Segment, register, replacement)),
            SubAssignOperation sub => new SubAssignOperation(
                SubstituteRegisterInExpr(sub.Target, register, replacement),
                SubstituteRegisterInExpr(sub.Value, register, replacement),
                sub.Segment is null ? null : SubstituteRegisterInExpr(sub.Segment, register, replacement)),
            CallOperation call => new CallOperation(
                call.Name,
                call.Args.Select(arg => SubstituteRegisterInExpr(arg, register, replacement)).ToList()),
            _ => operation,
        };

    private static bool ExprReferencesVariable(Operation operation, Variable variable) =>
        operation switch
        {
            SetOperation set => ExprReferencesVariable(set.Dst, variable) ||
                                ExprReferencesVariable(set.Src, variable),
            StoreOperation store => ExprReferencesVariable(store.Address, variable) ||
                                    ExprReferencesVariable(store.Value, variable) ||
                                    (store.Segment is not null && ExprReferencesVariable(store.Segment, variable)),
            ReturnOperation { Value: { } value } => ExprReferencesVariable(value, variable),
            IncOperation inc => ExprReferencesVariable(inc.Target, variable) ||
                                (inc.Segment is not null && ExprReferencesVariable(inc.Segment, variable)),
            DecOperation dec => ExprReferencesVariable(dec.Target, variable) ||
                                (dec.Segment is not null && ExprReferencesVariable(dec.Segment, variable)),
            AddAssignOperation add => ExprReferencesVariable(add.Target, variable) ||
                                      ExprReferencesVariable(add.Value, variable) ||
                                      (add.Segment is not null && ExprReferencesVariable(add.Segment, variable)),
            SubAssignOperation sub => ExprReferencesVariable(sub.Target, variable) ||
                                      ExprReferencesVariable(sub.Value, variable) ||
                                      (sub.Segment is not null && ExprReferencesVariable(sub.Segment, variable)),
            CallOperation call => call.Args.Any(arg => ExprReferencesVariable(arg, variable)),
            _ => false,
        };

    private static Expr SubstituteRegisterInExpr(Expr expr, Variable register, Expr replacement)
    {
        if (expr is VariableExpr { Var: var variable } && ReferenceEquals(variable, register))
        {
            return replacement;
        }

        return expr switch
        {
            ConstExpr or CharConstExpr or StringExpr or ImageOffsetExpr => expr,
            VariableExpr => expr,
            MemberExpr member => member with { Base = SubstituteRegisterInExpr(member.Base, register, replacement) },
            IncDecExpr inc => inc with { Operand = SubstituteRegisterInExpr(inc.Operand, register, replacement) },
            AddressOfExpr addr => addr with { Operand = SubstituteRegisterInExpr(addr.Operand, register, replacement) },
            Math1Expr math1 => math1 with { Op = SubstituteRegisterInExpr(math1.Op, register, replacement) },
            Math2Expr math2 => math2 with
            {
                First = SubstituteRegisterInExpr(math2.First, register, replacement),
                Second = SubstituteRegisterInExpr(math2.Second, register, replacement),
            },
            MemExpr mem => mem with
            {
                Address = SubstituteRegisterInExpr(mem.Address, register, replacement),
                Segment = mem.Segment is null ? null : SubstituteRegisterInExpr(mem.Segment, register, replacement),
            },
            CmpExpr cmp => cmp with
            {
                Left = SubstituteRegisterInExpr(cmp.Left, register, replacement),
                Right = SubstituteRegisterInExpr(cmp.Right, register, replacement),
            },
            CallExpr call => call with
            {
                Args = call.Args.Select(arg => SubstituteRegisterInExpr(arg, register, replacement)).ToList(),
            },
            LongExpr longExpr => longExpr with
            {
                Low = SubstituteRegisterInExpr(longExpr.Low, register, replacement),
                High = SubstituteRegisterInExpr(longExpr.High, register, replacement),
            },
            _ => expr,
        };
    }

    private static bool ExprReferencesVariable(Expr expr, Variable variable) =>
        expr switch
        {
            VariableExpr varExpr => ReferenceEquals(varExpr.Var, variable),
            Math2Expr math2 => ExprReferencesVariable(math2.First, variable) ||
                               ExprReferencesVariable(math2.Second, variable),
            Math1Expr math1 => ExprReferencesVariable(math1.Op, variable),
            CmpExpr cmp => ExprReferencesVariable(cmp.Left, variable) ||
                           ExprReferencesVariable(cmp.Right, variable),
            CallExpr call => call.Args.Any(arg => ExprReferencesVariable(arg, variable)),
            MemExpr mem => ExprReferencesVariable(mem.Address, variable) ||
                           (mem.Segment is not null && ExprReferencesVariable(mem.Segment, variable)),
            MemberExpr member => ExprReferencesVariable(member.Base, variable),
            IncDecExpr inc => ExprReferencesVariable(inc.Operand, variable),
            AddressOfExpr addr => ExprReferencesVariable(addr.Operand, variable),
            LongExpr longExpr => ExprReferencesVariable(longExpr.Low, variable) ||
                                 ExprReferencesVariable(longExpr.High, variable),
            _ => false,
        };

    /// <summary>
    /// Проверяет, есть ли интерференция (переопределение регистра или модификация источника) 
    /// между startIndex и usageIndex.
    /// </summary>
    private static bool HasInterferenceBetween(ExprBlock block, int startIndex, int usageIndex, Variable register, Expr source)
    {
        for (int i = startIndex; i < usageIndex; i++)
        {
            var op = block.Operations[i];

            // Переопределение регистра
            if (op is SetOperation setOp &&
                AssignmentTarget.TryGetVariable(setOp.Dst, out var setDstVar) &&
                setDstVar.Name == register.Name)
            {
                return true;
            }

            // Модификация исходной переменной (если источник - переменная)
            // Это критично для пост-инкремента: reg = a; INC a; b = reg
            // Нельзя заменять b = reg на b = a, потому что a уже изменён
            if (source is VariableExpr srcVarExpr &&
                OpModifiesVariable(op, srcVarExpr.Var))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Проверяет, модифицирует ли операция переменную.
    /// </summary>
    private static bool OpModifiesVariable(Operation op, Variable variable)
    {
        return op switch
        {
            SetOperation setOp => AssignmentTarget.ReferencesVariable(setOp.Dst, variable),
            StoreOperation store => AssignmentTarget.ReferencesVariable(store.Address, variable),
            IncOperation inc => AssignmentTarget.ReferencesVariable(inc.Target, variable),
            DecOperation dec => AssignmentTarget.ReferencesVariable(dec.Target, variable),
            AddAssignOperation addAssign => AssignmentTarget.ReferencesVariable(addAssign.Target, variable),
            SubAssignOperation subAssign => AssignmentTarget.ReferencesVariable(subAssign.Target, variable),
            _ => false
        };
    }

    /// <summary>
    /// Проверяет, создаст ли замена тавтологию (например, var1 = var1).
    /// </summary>
    private static bool CreatesTautology(Operation op, Expr source)
    {
        return op switch
        {
            SetOperation setOp when AssignmentTarget.TryGetVariable(setOp.Dst, out var dstVar) &&
                                   source is VariableExpr srcVarExpr &&
                                   ReferenceEquals(dstVar, srcVarExpr.Var) => true,
            StoreOperation store when AssignmentTarget.TryGetVariable(store.Address, out var addrVar) &&
                                      source is VariableExpr storeSrcVarExpr &&
                                      ReferenceEquals(addrVar, storeSrcVarExpr.Var) => true,
            _ => false
        };
    }

    /// <summary>Параметры оптимизации inc/dec в IR.</summary>
    protected readonly struct IncDecPatternOptions
    {
        public static IncDecPatternOptions Default { get; } = new();

        /// <summary>/Ox: <c>a = a ± 1</c> эквивалентно <c>a++/a--</c>.</summary>
        public static IncDecPatternOptions Optimized { get; } = new() { NormalizeSelfAddSubOne = true };

        public bool NormalizeSelfAddSubOne { get; init; }
    }

    /// <summary>
    /// Оптимизация паттернов инкремента/декремента в одном блоке (/Od).
    /// </summary>
    protected static void OptimizeIncDecPatternsInBlock(ExprBlock block) =>
        OptimizeIncDecPatternsInBlock(block, IncDecPatternOptions.Default);

    /// <summary>
    /// Оптимизация паттернов инкремента/декремента в одном блоке.
    /// </summary>
    protected static void OptimizeIncDecPatternsInBlock(ExprBlock block, IncDecPatternOptions options)
    {
        NormalizeAddSubOneInBlock(block);

        if (options.NormalizeSelfAddSubOne)
        {
            NormalizeSelfAddSubOneInBlock(block);
        }

        NormalizeAdd65535InBlock(block);
        RecognizePrePostIncDecInBlock(block, options);
    }

    /// <summary>Преобразование <c>+= 1</c>/<c>-= 1</c> в <c>IncOperation</c>/<c>DecOperation</c>.</summary>
    private static void NormalizeAddSubOneInBlock(ExprBlock block)
    {
        for (var i = 0; i < block.Operations.Count; i++)
        {
            switch (block.Operations[i])
            {
                case AddAssignOperation { Value: ConstExpr { Value: 1 }, Target: var target }:
                    if (AssignmentTarget.TryGetVariable(target, out var targetVar))
                    {
                        block.Operations[i] = new IncOperation(targetVar.ToSet());
                    }
                    break;
                case SubAssignOperation { Value: ConstExpr { Value: 1 }, Target: var target2 }:
                    if (AssignmentTarget.TryGetVariable(target2, out var targetVar2))
                    {
                        block.Operations[i] = new DecOperation(targetVar2.ToSet());
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// /Ox: <c>var = var ± 1</c> → <c>var++/var--</c> (в оптимизированном коде QuickC эквивалентно).
    /// </summary>
    private static void NormalizeSelfAddSubOneInBlock(ExprBlock block)
    {
        for (var i = 0; i < block.Operations.Count; i++)
        {
            if (block.Operations[i] is not SetOperation setOp ||
                !AssignmentTarget.TryGetVariable(setOp.Dst, out var dstVar))
            {
                continue;
            }

            switch (setOp.Src)
            {
                case Math2Expr
                {
                    Operation: Math2Operation.Add,
                    First: VariableExpr { Var: var srcVar },
                    Second: ConstExpr { Value: 1 },
                } when ReferenceEquals(dstVar, srcVar):
                    block.Operations[i] = new IncOperation(dstVar.ToSet());
                    break;
                case Math2Expr
                {
                    Operation: Math2Operation.Sub,
                    First: VariableExpr { Var: var srcVar2 },
                    Second: ConstExpr { Value: 1 },
                } when ReferenceEquals(dstVar, srcVar2):
                    block.Operations[i] = new DecOperation(dstVar.ToSet());
                    break;
            }
        }
    }

    /// <summary>Преобразование <c>var + 65535</c> в <c>var - 1</c> (16-битное дополнение до 2).</summary>
    private static void NormalizeAdd65535InBlock(ExprBlock block)
    {
        for (var i = 0; i < block.Operations.Count; i++)
        {
            if (block.Operations[i] is SetOperation setOp &&
                setOp.Src is Math2Expr { Operation: Math2Operation.Add, First: var first, Second: ConstExpr { Value: 65535 } })
            {
                block.Operations[i] = setOp with { Src = new Math2Expr(Math2Operation.Sub, first, ConstExpr.One) };
            }
        }
    }

    /// <summary>Распознавание пре-/пост-инкрементов и самообновления через регистр.</summary>
    private static void RecognizePrePostIncDecInBlock(ExprBlock block, IncDecPatternOptions options)
    {
        var changed = true;
        while (changed)
        {
            changed = false;

            for (int i = 0; i < block.Operations.Count - 1; i++)
            {
                if (options.NormalizeSelfAddSubOne && TryRecognizeRegisterSelfIncDec(block, i))
                {
                    changed = true;
                    break;
                }

                if (TryRecognizePrefixIncDec(block, i))
                {
                    changed = true;
                    break;
                }

                if (TryRecognizePostfixIncDec(block, i))
                {
                    changed = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// /Ox: <c>reg = var; inc/dec reg; var = reg</c> → <c>var = var ± 1</c>
    /// (QuickC инкрементирует регистр, а не память; для round-trip нужен явный <c>a = a ± 1</c>).
    /// </summary>
    private static bool TryRecognizeRegisterSelfIncDec(ExprBlock block, int index)
    {
        if (index + 2 >= block.Operations.Count ||
            block.Operations[index] is not SetOperation loadOp ||
            !AssignmentTarget.TryGetVariable(loadOp.Dst, out var regVar) ||
            !regVar.IsRegister ||
            loadOp.Src is not VariableExpr { Var: var sourceVar } ||
            !TryGetIncDecOnRegister(block.Operations[index + 1], regVar, out var isIncrement) ||
            block.Operations[index + 2] is not SetOperation storeOp ||
            !AssignmentTarget.TryGetVariable(storeOp.Dst, out var destVar) ||
            !ReferenceEquals(destVar, sourceVar) ||
            storeOp.Src is not VariableExpr { Var: var storedReg } ||
            !ReferenceEquals(storedReg, regVar))
        {
            return false;
        }

        var mathOp = isIncrement ? Math2Operation.Add : Math2Operation.Sub;
        block.Operations[index] = new SetOperation(
            sourceVar.ToSet(),
            new Math2Expr(mathOp, sourceVar.ToGet(), ConstExpr.One));
        block.Operations.RemoveAt(index + 2);
        block.Operations.RemoveAt(index + 1);
        return true;
    }

    /// <summary>Проверяет inc/dec (или <c>reg = reg ± 1</c>) на регистре.</summary>
    private static bool TryGetIncDecOnRegister(Operation operation, Variable reg, out bool isIncrement)
    {
        isIncrement = true;
        return operation switch
        {
            IncOperation { Target: var target } when AssignmentTarget.ReferencesVariable(target, reg) => true,
            DecOperation { Target: var target } when AssignmentTarget.ReferencesVariable(target, reg) =>
                SetIncDecDirection(isIncrement: false, out isIncrement),
            SetOperation
            {
                Dst: var dst,
                Src: Math2Expr
                {
                    Operation: Math2Operation.Add,
                    First: VariableExpr { Var: var src },
                    Second: ConstExpr { Value: 1 },
                },
            } when ReferenceEquals(dst, reg) && ReferenceEquals(src, reg) => true,
            SetOperation
            {
                Dst: var dst,
                Src: Math2Expr
                {
                    Operation: Math2Operation.Sub,
                    First: VariableExpr { Var: var src },
                    Second: ConstExpr { Value: 1 },
                },
            } when ReferenceEquals(dst, reg) && ReferenceEquals(src, reg) =>
                SetIncDecDirection(isIncrement: false, out isIncrement),
            _ => false,
        };
    }

    /// <summary><c>Inc/Dec(var); dst = var</c> → <c>dst = ++/--var</c>.</summary>
    private static bool TryRecognizePrefixIncDec(ExprBlock block, int index)
    {
        if (index + 1 >= block.Operations.Count)
        {
            return false;
        }

        if (block.Operations[index] is IncOperation incOp &&
            AssignmentTarget.TryGetVariable(incOp.Target, out var sourceVar) &&
            !sourceVar.IsRegister)
        {
            return TryReplacePrefixIncDec(block, index, sourceVar, Math1Operation.PreIncrement);
        }

        if (block.Operations[index] is DecOperation decOp &&
            AssignmentTarget.TryGetVariable(decOp.Target, out sourceVar) &&
            !sourceVar.IsRegister)
        {
            return TryReplacePrefixIncDec(block, index, sourceVar, Math1Operation.PreDecrement);
        }

        return false;
    }

    private static bool TryReplacePrefixIncDec(
        ExprBlock block,
        int index,
        Variable sourceVar,
        Math1Operation prefixOp)
    {
        if (block.Operations[index + 1] is not SetOperation nextSet ||
            nextSet.Src is not VariableExpr nextVarExpr ||
            !ReferenceEquals(nextVarExpr.Var, sourceVar) ||
            !AssignmentTarget.TryGetVariable(nextSet.Dst, out var dstVar) ||
            ReferenceEquals(dstVar, sourceVar))
        {
            return false;
        }

        block.Operations[index] = new SetOperation(
            nextSet.Dst,
            new Math1Expr(prefixOp, new VariableExpr { Var = sourceVar }));
        block.Operations.RemoveAt(index + 1);
        return true;
    }

    /// <summary><c>reg = var; Inc/Dec(var); dst = reg</c> → <c>dst = var++/var--</c>.</summary>
    private static bool TryRecognizePostfixIncDec(ExprBlock block, int index)
    {
        if (block.Operations[index] is not SetOperation setRegOp ||
            !AssignmentTarget.TryGetVariable(setRegOp.Dst, out var regVar) ||
            !regVar.IsRegister ||
            setRegOp.Src is not VariableExpr srcVarExpr ||
            index + 2 >= block.Operations.Count)
        {
            return false;
        }

        var sourceVar = srcVarExpr.Var;
        if (!TryGetIncDecOnVariable(block.Operations[index + 1], sourceVar, out var isIncrement))
        {
            return false;
        }

        for (int j = index + 2; j < block.Operations.Count; j++)
        {
            if (block.Operations[j] is not SetOperation useRegOp ||
                useRegOp.Src is not VariableExpr useRegVarExpr ||
                !ReferenceEquals(useRegVarExpr.Var, regVar) ||
                !AssignmentTarget.TryGetVariable(useRegOp.Dst, out var useDstVar) ||
                ReferenceEquals(useDstVar, regVar))
            {
                continue;
            }

            for (int k = index + 1; k < j; k++)
            {
                if (block.Operations[k] is SetOperation checkSet &&
                    AssignmentTarget.TryGetVariable(checkSet.Dst, out var checkVar) &&
                    ReferenceEquals(checkVar, regVar))
                {
                    return false;
                }
            }

            var postOp = isIncrement ? Math1Operation.PostIncrement : Math1Operation.PostDecrement;
            block.Operations[j] = new SetOperation(
                useRegOp.Dst,
                new Math1Expr(postOp, new VariableExpr { Var = sourceVar }));
            block.Operations.RemoveAt(index + 1);
            block.Operations.RemoveAt(index);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Проверяет, инкрементирует/декрементирует ли операция переменную на 1
    /// (<c>Inc/Dec</c> или <c>+= 1/-= 1</c>).
    /// </summary>
    private static bool TryGetIncDecOnVariable(Operation operation, Variable variable, out bool isIncrement)
    {
        isIncrement = true;
        return operation switch
        {
            AddAssignOperation { Value: ConstExpr { Value: 1 }, Target: var target }
                when AssignmentTarget.ReferencesVariable(target, variable) => true,
            IncOperation { Target: var target } when AssignmentTarget.ReferencesVariable(target, variable) => true,
            SubAssignOperation { Value: ConstExpr { Value: 1 }, Target: var target }
                when AssignmentTarget.ReferencesVariable(target, variable) =>
                SetIncDecDirection(isIncrement: false, out isIncrement),
            DecOperation { Target: var target } when AssignmentTarget.ReferencesVariable(target, variable) =>
                SetIncDecDirection(isIncrement: false, out isIncrement),
            _ => false,
        };
    }

    private static bool SetIncDecDirection(bool isIncrement, out bool result)
    {
        result = isIncrement;
        return true;
    }
}
