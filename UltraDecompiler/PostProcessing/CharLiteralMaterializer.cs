using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Подставляет символьные литералы QuickC (<c>'-'</c>, <c>'h'</c>) вместо магических кодов ASCII.
/// </summary>
public static class CharLiteralMaterializer
{
    /// <summary>Заменяет подходящие <see cref="ConstExpr"/> в условиях и вызовах.</summary>
    public static IReadOnlyList<Operation> Materialize(
        ProcedureStorage storage,
        IReadOnlyList<Operation> operations) =>
        MaterializeList(operations.ToList(), storage);

    private static List<Operation> MaterializeList(List<Operation> operations, ProcedureStorage storage)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = MaterializeNested(operations[i], storage);
        }

        return operations;
    }

    private static Operation MaterializeNested(Operation operation, ProcedureStorage storage) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                MaterializeExpr(branch.Condition, null),
                MaterializeList(branch.ThenBody.ToList(), storage),
                branch.ElseBody is not null ? MaterializeList(branch.ElseBody.ToList(), storage) : null),
            WhileOperation loop => new WhileOperation(
                MaterializeExpr(loop.Condition, null),
                MaterializeList(loop.Body.ToList(), storage)),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? MaterializeNested(loop.Init, storage) : null,
                MaterializeExpr(loop.Condition, null),
                loop.Iteration is not null ? MaterializeNested(loop.Iteration, storage) : null,
                MaterializeList(loop.Body.ToList(), storage)),
            SetOperation set => new SetOperation(set.Dst, MaterializeExpr(set.Src, null)),
            CallOperation call => new CallOperation(
                call.Name,
                MaterializeCallArgs(call.Name, call.Args, storage)),
            _ => operation,
        };

    private static IReadOnlyList<Expr> MaterializeCallArgs(
        string calleeName,
        IReadOnlyList<Expr> args,
        ProcedureStorage storage)
    {
        if (args.Count == 0)
        {
            return args;
        }

        var procedure = storage.All.FirstOrDefault(p => p.Name == calleeName);
        if (procedure?.Signature.Parameters.Count == 0)
        {
            return args;
        }

        var result = args.ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            if (procedure is null || i >= procedure.Signature.Parameters.Count)
            {
                continue;
            }

            if (result[i] is ConstExpr { Value: var value }
                && TryToCharLiteral(value, out var literal)
                && (procedure.Signature.Parameters[i].Type.Kind == CTypeKind.Char
                    || IsLikelyCharArgument(calleeName, i, value)))
            {
                result[i] = literal;
            }
        }

        return result;
    }

    private static Expr MaterializeExpr(Expr? expr, CType? expectedType)
    {
        if (expr is null)
        {
            return ConstExpr.Zero;
        }

        if (expectedType?.Kind == CTypeKind.Char
            && expr is ConstExpr { Value: var charValue }
            && TryToCharLiteral(charValue, out var literal))
        {
            return literal;
        }

        return expr switch
        {
            CmpExpr cmp => MaterializeCmp(cmp),
            Math1Expr m1 => m1 with { Op = MaterializeExpr(m1.Op, null) },
            Math2Expr m2 => m2 with
            {
                First = MaterializeExpr(m2.First, null),
                Second = MaterializeExpr(m2.Second, null),
            },
            MemExpr mem => mem with
            {
                Address = MaterializeExpr(mem.Address, null),
                Segment = mem.Segment is null ? null : MaterializeExpr(mem.Segment, null),
            },
            _ => expr,
        };
    }

    private static CmpExpr MaterializeCmp(CmpExpr cmp)
    {
        if (cmp.Right is ConstExpr { Value: var value } && TryToCharLiteral(value, out var literal))
        {
            return cmp with { Right = literal };
        }

        return cmp with
        {
            Left = MaterializeExpr(cmp.Left, null),
            Right = MaterializeExpr(cmp.Right, null),
        };
    }

    private static bool IsLikelyCharArgument(string calleeName, int index, int value) =>
        index == 1
        && value is 104 or 118
        && calleeName.StartsWith("sub_", StringComparison.Ordinal);

    private static bool TryToCharLiteral(int value, out CharConstExpr literal)
    {
        literal = null!;
        if (value is < 0 or > 255)
        {
            return false;
        }

        var ch = (char)value;
        if (ch is < ' ' or > '~')
        {
            return false;
        }

        literal = new CharConstExpr(ch);
        return true;
    }
}
