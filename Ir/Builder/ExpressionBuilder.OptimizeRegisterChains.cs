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
    /// </summary>
    private void OptimizeRegisterChains()
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
                    }
                }

                // Удаляем присваивание регистру
                block.Operations.RemoveAt(i);
                changed = true;
                break; // Начинаем заново после изменения
            }
        }
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
}
