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

    /// <summary>
    /// Оптимизация паттернов инкремента/декремента в одном блоке.
    /// </summary>
    protected static void OptimizeIncDecPatternsInBlock(ExprBlock block)
    {
        // Фаза 1: NormalizeAddSubOne - преобразование += 1/-= 1 в IncOperation/DecOperation
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

        // Фаза 2: Преобразование var + 65535 в var - 1 (16-битное дополнение до 2)
        for (var i = 0; i < block.Operations.Count; i++)
        {
            if (block.Operations[i] is SetOperation setOp &&
                setOp.Src is Math2Expr { Operation: Math2Operation.Add, First: var first, Second: ConstExpr { Value: 65535 } })
            {
                block.Operations[i] = setOp with { Src = new Math2Expr(Math2Operation.Sub, first, ConstExpr.One) };
            }
        }

        // Фаза 3: Распознавание пре-/пост-инкрементов
        var changed = true;
        while (changed)
        {
            changed = false;

            for (int i = 0; i < block.Operations.Count - 1; i++)
            {
                // Паттерн 1: IncOperation(var1); var2 = var1 → var2 = ++var1
                if (block.Operations[i] is IncOperation incOp &&
                    AssignmentTarget.TryGetVariable(incOp.Target, out var incTargetVar) &&
                    block.Operations[i + 1] is SetOperation nextSet &&
                    nextSet.Src is VariableExpr nextVarExpr &&
                    ReferenceEquals(nextVarExpr.Var, incTargetVar) &&
                    AssignmentTarget.TryGetVariable(nextSet.Dst, out var dstVar) &&
                    !ReferenceEquals(dstVar, incTargetVar))
                {
                    // Заменяем на var2 = ++var1, удаляем IncOperation
                    block.Operations[i] = new SetOperation(nextSet.Dst, new Math1Expr(Math1Operation.PreIncrement, new VariableExpr { Var = incTargetVar }));
                    block.Operations.RemoveAt(i + 1);
                    changed = true;
                    break;
                }

                // Паттерн 1b: DecOperation(var1); var2 = var1 → var2 = --var1
                if (block.Operations[i] is DecOperation decOp &&
                    AssignmentTarget.TryGetVariable(decOp.Target, out var decTargetVar) &&
                    block.Operations[i + 1] is SetOperation nextSet2 &&
                    nextSet2.Src is VariableExpr nextVarExpr2 &&
                    ReferenceEquals(nextVarExpr2.Var, decTargetVar) &&
                    AssignmentTarget.TryGetVariable(nextSet2.Dst, out var dstVar2) &&
                    !ReferenceEquals(dstVar2, decTargetVar))
                {
                    // Заменяем на var2 = --var1, удаляем DecOperation
                    block.Operations[i] = new SetOperation(nextSet2.Dst, new Math1Expr(Math1Operation.PreDecrement, new VariableExpr { Var = decTargetVar }));
                    block.Operations.RemoveAt(i + 1);
                    changed = true;
                    break;
                }

                // Паттерн 2: regAX = var1; IncOperation(var1); var2 = regAX → var2 = var1++
                // Ищем: Set(reg, var); Inc/Dec(var); Set(dst, reg)
                if (block.Operations[i] is SetOperation setRegOp &&
                    AssignmentTarget.TryGetVariable(setRegOp.Dst, out var regVar) &&
                    regVar.IsRegister &&
                    setRegOp.Src is VariableExpr srcVarExpr &&
                    i + 2 < block.Operations.Count)
                {
                    // Проверяем следующую операцию - инкремент/декремент переменной
                    var sourceVar = srcVarExpr.Var;
                    bool foundIncDec = false;
                    bool isIncrement = true;

                    // Проверяем AddAssignOperation или IncOperation
                    if (block.Operations[i + 1] is AddAssignOperation addIncOp2 &&
                        addIncOp2.Value is ConstExpr { Value: 1 } &&
                        AssignmentTarget.ReferencesVariable(addIncOp2.Target, sourceVar))
                    {
                        foundIncDec = true;
                    }
                    else if (block.Operations[i + 1] is IncOperation incOp2 &&
                             AssignmentTarget.ReferencesVariable(incOp2.Target, sourceVar))
                    {
                        foundIncDec = true;
                    }
                    else if (block.Operations[i + 1] is SubAssignOperation subDecOp2 &&
                             subDecOp2.Value is ConstExpr { Value: 1 } &&
                             AssignmentTarget.ReferencesVariable(subDecOp2.Target, sourceVar))
                    {
                        foundIncDec = true;
                        isIncrement = false;
                    }
                    else if (block.Operations[i + 1] is DecOperation decOp2 &&
                             AssignmentTarget.ReferencesVariable(decOp2.Target, sourceVar))
                    {
                        foundIncDec = true;
                        isIncrement = false;
                    }

                    if (foundIncDec)
                    {
                        // Ищем использование регистра
                        for (int j = i + 2; j < block.Operations.Count; j++)
                        {
                            if (block.Operations[j] is SetOperation useRegOp &&
                                useRegOp.Src is VariableExpr useRegVarExpr &&
                                ReferenceEquals(useRegVarExpr.Var, regVar) &&
                                AssignmentTarget.TryGetVariable(useRegOp.Dst, out var useDstVar) &&
                                !ReferenceEquals(useDstVar, regVar))
                            {
                                // Проверяем, что регистр не переопределяется между i и j
                                bool regRedefined = false;
                                for (int k = i + 1; k < j; k++)
                                {
                                    if (block.Operations[k] is SetOperation checkSet &&
                                        AssignmentTarget.TryGetVariable(checkSet.Dst, out var checkVar) &&
                                        ReferenceEquals(checkVar, regVar))
                                    {
                                        regRedefined = true;
                                        break;
                                    }
                                }

                                if (!regRedefined)
                                {
                                    // Заменяем на var2 = var1++ или var2 = var1--
                                    var postOp = isIncrement ? Math1Operation.PostIncrement : Math1Operation.PostDecrement;
                                    block.Operations[j] = new SetOperation(useRegOp.Dst, new Math1Expr(postOp, new VariableExpr { Var = sourceVar }));
                                    // Удаляем IncOperation/DecOperation (теперь на позиции i после удаления setRegOp)
                                    block.Operations.RemoveAt(i + 1);
                                    // Удаляем присваивание регистру
                                    block.Operations.RemoveAt(i);
                                    changed = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (changed)
                        break;
                }
            }
        }
    }
}
