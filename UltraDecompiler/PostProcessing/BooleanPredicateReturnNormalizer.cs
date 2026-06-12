using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Восстанавливает <c>return s[0]=='-' &amp;&amp; s[1]==c &amp;&amp; s[2]=='\0'</c> из цепочки if/return.
/// </summary>
public static class BooleanPredicateReturnNormalizer
{
    /// <summary>Сворачивает тело предиката-флага в один return с &amp;&amp;.</summary>
    public static IReadOnlyList<Operation> Normalize(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations)
    {
        if (procedure.Signature.ReturnType.IsVoid || operations.Count == 0)
        {
            return operations;
        }

        if (!TryBuildFlagPredicateReturn(operations, out var predicate))
        {
            return operations;
        }

        UpdateCharLetterParameter(procedure);
        return [new ReturnOperation(predicate, IsExplicit: true)];
    }

    private static bool TryBuildFlagPredicateReturn(IReadOnlyList<Operation> operations, out Expr predicate)
    {
        predicate = null!;

        if (operations[0] is not IfOperation outer
            || outer.ElseBody is { Count: > 0 }
            || outer.ThenBody.Count == 0
            || !TryMatchDashTest(outer.Condition, out var stringVar, out var dashTest))
        {
            return false;
        }

        if (outer.ThenBody[0] is not IfOperation letterBranch
            || letterBranch.ElseBody is { Count: > 0 }
            || !TryMatchLetterEqParam(letterBranch.Condition, stringVar, 1, out _, out var letterTest))
        {
            return false;
        }

        if (letterBranch.ThenBody.Count == 0
            || letterBranch.ThenBody[0] is not IfOperation zeroBranch
            || !TryMatchZeroSuccessBranch(zeroBranch, stringVar, 2, out var zeroTest))
        {
            return false;
        }

        predicate = dashTest.BoolAnd(letterTest).BoolAnd(zeroTest);
        return true;
    }

    private static bool TryMatchDashTest(Expr condition, out Variable stringVar, out CmpExpr dashTest)
    {
        stringVar = null!;
        dashTest = null!;

        if (condition is CmpExpr { Operation: CmpOperation.Eq } cmp
            && TryGetCharConst(cmp.Right, out var dash)
            && dash == '-'
            && TryGetStringVar(cmp.Left, out stringVar))
        {
            dashTest = new CmpExpr(CmpOperation.Eq, DerefChar(stringVar, 0), new CharConstExpr('-'));
            return true;
        }

        return false;
    }

    private static bool TryMatchLetterEqParam(
        Expr condition,
        Variable stringVar,
        int index,
        out Variable letter,
        out CmpExpr letterTest)
    {
        letter = null!;
        letterTest = null!;

        if (condition is not CmpExpr { Operation: CmpOperation.Eq } cmp
            || cmp.Right is not Variable param
            || !IsIndexedCharAccess(cmp.Left, stringVar, index))
        {
            return false;
        }

        letter = param;
        letterTest = new CmpExpr(CmpOperation.Eq, DerefChar(stringVar, index), letter);
        return true;
    }

    /// <summary>
    /// <c>if (zero==0) { ; } else { return 0; }</c> — успех при нулевом терминаторе.
    /// </summary>
    private static bool TryMatchZeroSuccessBranch(IfOperation zeroBranch, Variable stringVar, int index, out CmpExpr zeroTest)
    {
        zeroTest = null!;

        if (zeroBranch.ElseBody is not [ReturnOperation { Value: ConstExpr { Value: 0 } }]
            || !IsEmptyBody(zeroBranch.ThenBody)
            || !TryMatchZeroEq(zeroBranch.Condition, stringVar, index))
        {
            return false;
        }

        zeroTest = new CmpExpr(CmpOperation.Eq, DerefChar(stringVar, index), ConstExpr.Zero);
        return true;
    }

    private static bool TryMatchZeroEq(Expr condition, Variable stringVar, int index)
    {
        if (condition is CmpExpr { Operation: CmpOperation.Eq, Right: ConstExpr { Value: 0 } } cmp
            && IsIndexedCharAccess(cmp.Left, stringVar, index))
        {
            return true;
        }

        if (condition is CmpExpr { Operation: CmpOperation.Eq, Right: ConstExpr { Value: 0 } } cbwCmp
            && TryExtractIndexedChar(cbwCmp.Left, stringVar, index))
        {
            return true;
        }

        return false;
    }

    private static bool TryExtractIndexedChar(Expr expr, Variable stringVar, int index)
    {
        foreach (var mem in ExprSubstitution.CollectMemExprs(expr))
        {
            if (IsIndexedCharAccess(mem, stringVar, index))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEmptyBody(IReadOnlyList<Operation> body) =>
        body.Count == 0 || body.All(static op => op is not ReturnOperation and not CallOperation and not SetOperation);

    private static bool TryGetCharConst(Expr expr, out char value)
    {
        switch (expr)
        {
            case CharConstExpr ch:
                value = ch.Value;
                return true;
            case ConstExpr { Value: var number } when number is >= 0 and <= 255:
                value = (char)number;
                return true;
            default:
                value = default;
                return false;
        }
    }

    private static bool TryGetStringVar(Expr expr, out Variable stringVar)
    {
        stringVar = null!;
        foreach (var mem in ExprSubstitution.CollectMemExprs(expr))
        {
            if (PointerDerefFormatter.IsNearPointerDeref(mem)
                && mem.Address is Variable ptr)
            {
                stringVar = ptr;
                return true;
            }
        }

        return false;
    }

    private static bool IsIndexedCharAccess(Expr expr, Variable stringVar, int index)
    {
        if (expr is not MemExpr mem
            || mem.Address is not Math2Expr { Operation: Math2Operation.Add, First: Variable ptr, Second: ConstExpr { Value: var offset } }
            || offset != index
            || !SameVariable(ptr, stringVar))
        {
            return false;
        }

        return PointerDerefFormatter.IsNearDataSegment(mem.Segment);
    }

    private static bool SameVariable(Variable left, Variable right) =>
        left.Name is not null && right.Name is not null
            ? left.Name == right.Name
            : ReferenceEquals(left, right);

    private static MemExpr DerefChar(Variable ptr, int index) =>
        new(new Math2Expr(Math2Operation.Add, ptr, new ConstExpr(index)));

    private static void UpdateCharLetterParameter(DisassembledProcedure procedure)
    {
        if (procedure.Signature.Parameters.Count < 2)
        {
            return;
        }

        var parameters = procedure.Signature.Parameters.ToArray();
        parameters[1] = new ProcedureParameter(CType.Char, parameters[1].Location);
        procedure.Signature = new ProcedureSignature(procedure.Signature.ReturnType, parameters);
    }
}
