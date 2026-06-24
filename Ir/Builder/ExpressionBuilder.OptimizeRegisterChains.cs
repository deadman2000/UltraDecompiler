using UltraDecompiler.Ir.Expressions;
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

            for (int i = 0; i < block.Operations.Count; i++)
            {
                if (block.Operations[i] is not SetOperation setOp)
                    continue;

                if (!AssignmentTarget.TryGetVariable(setOp.Dst, out var dstVar))
                    continue;

                // Оптимизируем только регистры
                if (!dstVar.IsRegister)
                    continue;

                // Проверяем, что это простое присваивание (переменная или константа)
                if (setOp.Src is not (VariableExpr or ConstExpr))
                    continue;

                // Ищем все простые использования этого регистра в этом же блоке
                var usages = FindAllSimpleUsages(block, i + 1, dstVar, setOp.Src);

                if (usages.Count == 0)
                    continue;

                // Проверяем, что нет интерференции между присваиванием и использованиями
                bool hasInterference = false;
                foreach (var usage in usages)
                {
                    if (HasInterferenceBetween(block, i + 1, usage, dstVar, setOp.Src))
                    {
                        hasInterference = true;
                        break;
                    }

                    // Проверяем, что замена не создаст тавтологию (var = var)
                    if (CreatesTautology(block.Operations[usage], setOp.Src))
                    {
                        hasInterference = true;
                        break;
                    }
                }

                if (hasInterference)
                    continue;

                // Заменяем все использования регистра на исходное выражение
                foreach (var usage in usages)
                {
                    switch (block.Operations[usage])
                    {
                        case SetOperation nextSet:
                            block.Operations[usage] = nextSet with { Src = setOp.Src };
                            break;

                        case StoreOperation store:
                            block.Operations[usage] = store with { Value = setOp.Src };
                            break;

                        case ReturnOperation ret:
                            block.Operations[usage] = ret with { Value = setOp.Src };
                            break;

                        case IncOperation inc:
                            block.Operations[usage] = inc with { Target = setOp.Src };
                            break;

                        case DecOperation dec:
                            block.Operations[usage] = dec with { Target = setOp.Src };
                            break;

                        case AddAssignOperation addAssign:
                            block.Operations[usage] = addAssign with { Target = setOp.Src };
                            break;

                        case SubAssignOperation subAssign:
                            block.Operations[usage] = subAssign with { Target = setOp.Src };
                            break;
                    }
                }

                // Удаляем присваивание регистру
                block.Operations.RemoveAt(i);
                changed = true;
                break; // Начинаем заново после изменения
            }
        }

        // Вторая фаза: оптимизация арифметических цепочек
        // reg = var; reg = reg + const; dst = reg → dst = var + const
        OptimizeArithmeticRegisterChains(block);
    }

    /// <summary>
    /// Оптимизация арифметических цепочек через регистры.
    /// Обрабатывает паттерны вида:
    ///   reg = var; reg = reg + const; dst = reg → dst = var + const
    ///   reg = var; reg = reg + 1; var = reg → var = var + 1
    /// </summary>
    private static void OptimizeArithmeticRegisterChains(ExprBlock block)
    {
        var changed = true;
        while (changed)
        {
            changed = false;

            for (int i = 0; i < block.Operations.Count - 2; i++)
            {
                // Паттерн: reg = src; reg = reg + const; dst = reg
                if (block.Operations[i] is not SetOperation loadOp ||
                    !AssignmentTarget.TryGetVariable(loadOp.Dst, out var regVar) ||
                    !regVar.IsRegister ||
                    loadOp.Src is not (VariableExpr or ConstExpr))
                {
                    continue;
                }

                if (block.Operations[i + 1] is not SetOperation arithOp ||
                    !AssignmentTarget.ReferencesVariable(arithOp.Dst, regVar) ||
                    arithOp.Src is not Math2Expr mathExpr ||
                    !ExprReferencesVariable(mathExpr.First, regVar))
                {
                    continue;
                }

                // Проверяем, что вторая операция - это арифметика с константой
                if (mathExpr.Second is not ConstExpr constExpr)
                {
                    continue;
                }

                // Ищем использование регистра в третьей операции
                for (int j = i + 2; j < block.Operations.Count; j++)
                {
                    if (block.Operations[j] is SetOperation useOp &&
                        useOp.Src is VariableExpr useVarExpr &&
                        ReferenceEquals(useVarExpr.Var, regVar) &&
                        AssignmentTarget.TryGetVariable(useOp.Dst, out _))
                    {
                        // Проверяем, что регистр не переопределяется между i+1 и j
                        bool regRedefined = false;
                        for (int k = i + 2; k < j; k++)
                        {
                            if (block.Operations[k] is SetOperation checkSet &&
                                AssignmentTarget.ReferencesVariable(checkSet.Dst, regVar))
                            {
                                regRedefined = true;
                                break;
                            }
                        }

                        if (regRedefined)
                            continue;

                        // Создаём новое выражение: src + const
                        var newSrc = new Math2Expr(mathExpr.Operation, loadOp.Src, constExpr);
                        block.Operations[j] = useOp with { Src = newSrc };

                        // Удаляем присваивания регистру
                        block.Operations.RemoveAt(i + 1);
                        block.Operations.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }

                if (changed)
                    break;
            }
        }
    }

    private static bool ExprReferencesVariable(Expr expr, Variable variable)
    {
        return expr switch
        {
            VariableExpr varExpr => ReferenceEquals(varExpr.Var, variable),
            Math2Expr math2 => ExprReferencesVariable(math2.First, variable) ||
                               ExprReferencesVariable(math2.Second, variable),
            Math1Expr math1 => ExprReferencesVariable(math1.Op, variable),
            _ => false
        };
    }

    /// <summary>
    /// Находит все простые использования регистра в блоке.
    /// Останавливается при переопределении регистра.
    /// </summary>
    private static List<int> FindAllSimpleUsages(ExprBlock block, int startIndex, Variable register, Expr source)
    {
        var usages = new List<int>();

        for (int i = startIndex; i < block.Operations.Count; i++)
        {
            var op = block.Operations[i];

            // Если регистр переопределяется — остановить поиск
            if (op is SetOperation setOp &&
                AssignmentTarget.TryGetVariable(setOp.Dst, out var setDstVar) &&
                setDstVar.Name == register.Name)
            {
                break;
            }

            // Проверяем простое использование
            if (IsSimpleRegisterUsage(op, register))
            {
                usages.Add(i);
            }
        }

        return usages;
    }

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
    /// Проверяет, является ли операция простым использованием регистра (Set, Store, Return, Inc, Dec с reg).
    /// </summary>
    private static bool IsSimpleRegisterUsage(Operation op, Variable register)
    {
        return op switch
        {
            SetOperation setOp => setOp.Src is VariableExpr varExpr && varExpr.Var.Name == register.Name,
            StoreOperation store => store.Value is VariableExpr storeVarExpr && storeVarExpr.Var.Name == register.Name,
            ReturnOperation ret => ret.Value is VariableExpr retVarExpr && retVarExpr.Var.Name == register.Name,
            IncOperation inc => inc.Target is VariableExpr incVarExpr && incVarExpr.Var.Name == register.Name,
            DecOperation dec => dec.Target is VariableExpr decVarExpr && decVarExpr.Var.Name == register.Name,
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
