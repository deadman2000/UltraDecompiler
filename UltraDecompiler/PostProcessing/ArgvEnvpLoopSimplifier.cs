using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Упрощает цикл обхода <c>argv</c>/<c>envp</c>: <c>while (envp[i] != 0)</c> вместо цепочки temp-указателей.
/// </summary>
public static class ArgvEnvpLoopSimplifier
{
    /// <summary>
    /// Удаляет подготовку near-указателя перед циклом и переписывает условие на <c>arr[i] != 0</c>.
    /// </summary>
    public static IReadOnlyList<Operation> Simplify(IReadOnlyList<Operation> operations) =>
        SimplifyList(operations.ToList());

    private static List<Operation> SimplifyList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = SimplifyNested(operations[i]);
        }

        for (var i = 0; i < operations.Count - 2; i++)
        {
            if (!TryMatchPointerSetup(operations, i, out var array, out var indexVar, out _, out var cmpOp))
            {
                continue;
            }

            var elementAccess = new CharPtrArrayFormatter.SyntheticLoadExpr($"{array}[{indexVar}]", array, indexVar);
            var rewrittenCondition = new CmpExpr(cmpOp, elementAccess, ConstExpr.Zero);
            var loopBody = operations[i + 2] switch
            {
                WhileOperation loop => loop.Body,
                IfOperation branch => branch.ThenBody,
                _ => [],
            };

            operations[i + 2] = new WhileOperation(rewrittenCondition, loopBody);

            operations.RemoveAt(i + 1);
            operations.RemoveAt(i);
            i = Math.Max(-1, i - 1);
        }

        return operations;
    }

    private static Operation SimplifyNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                SimplifyList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? SimplifyList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(loop.Condition, SimplifyList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? SimplifyNested(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? SimplifyNested(loop.Iteration) : null,
                SimplifyList(loop.Body.ToList())),
            _ => operation,
        };

    private static bool TryMatchPointerSetup(
        IReadOnlyList<Operation> operations,
        int index,
        out Variable array,
        out Variable indexVar,
        out Variable pointerTemp,
        out CmpOperation cmpOp)
    {
        array = null!;
        indexVar = null!;
        pointerTemp = null!;
        cmpOp = default;
        if (operations[index] is not SetOperation scaleSet
            || operations[index + 1] is not SetOperation pointerSet
            || pointerSet.Dst is not Variable pointerTempVar)
        {
            return false;
        }

        if (operations[index + 2] is not (WhileOperation or IfOperation { ElseBody: null or { Count: 0 } }))
        {
            return false;
        }

        if (!TryParseScaledIndex(scaleSet.Src, out indexVar))
        {
            return false;
        }

        if (pointerSet.Src is not Math2Expr { Operation: Math2Operation.Add, First: Variable baseVar } add
            || baseVar.Type?.IsCharPtrPtr != true
            || !IsScaleOffset(add.Second, scaleSet))
        {
            return false;
        }

        array = baseVar;
        pointerTemp = pointerTempVar;

        var condition = operations[index + 2] switch
        {
            WhileOperation loop => loop.Condition,
            IfOperation branch => branch.Condition,
            _ => null,
        };

        if (condition is not CmpExpr { Left: MemExpr mem, Right: ConstExpr { Value: 0 } } cmp
            || mem.Address is not Variable addr
            || !ReferenceEquals(addr, pointerTemp))
        {
            return false;
        }

        if (cmp.Operation is not (CmpOperation.Eq or CmpOperation.Ne))
        {
            return false;
        }

        cmpOp = cmp.Operation;
        return true;
    }

    private static bool TryParseScaledIndex(Expr expr, out Variable indexVar)
    {
        indexVar = null!;

        if (expr is Math2Expr { Operation: Math2Operation.Shl, First: Variable index, Second: ConstExpr { Value: 1 } })
        {
            indexVar = index;
            return true;
        }

        return false;
    }

    private static bool IsScaleOffset(Expr offset, SetOperation scaleSet) =>
        ReferenceEquals(offset, scaleSet.Dst) || ReferenceEquals(offset, scaleSet.Src);
}
