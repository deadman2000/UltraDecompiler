using UltraDecompiler.PostProcessing.Infrastructure;

namespace UltraDecompiler.PostProcessing.Literals;

/// <summary>
/// Подставляет символьные литералы QuickC вместо магических кодов ASCII по типу параметра.
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
            SwitchOperation sw => OperationTreeMapper.MapSwitchBodies(
                sw,
                bodies => MaterializeList(bodies.ToList(), storage)),
            SetOperation set => new SetOperation(set.Dst, MaterializeExpr(set.Src, null)),
            StoreOperation store when FarPointerFormatter.TryFormatStore(store, out _) => new StoreOperation(
                store.Address,
                store.Segment,
                MaterializeStoreValue(store.Value)),
            CallOperation call => new CallOperation(
                call.Name,
                MaterializeCallArgs(call.Name, call.Args, storage)),
            _ => operation,
        };

    private static Expr MaterializeStoreValue(Expr value)
    {
        if (value is ConstExpr { Value: var storeValue }
            && TryToCharLiteral(storeValue, out var literal)
            && storeValue is >= ' ' and <= '~')
        {
            return literal;
        }

        return MaterializeExpr(value, null);
    }

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
                && procedure.Signature.Parameters[i].Type.Kind == CTypeKind.Char)
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
        if (cmp.Right is ConstExpr { Value: var value }
            && cmp.Left is VariableExpr { Var: var left }
            && left.Type?.Kind == CTypeKind.Char
            && TryToCharLiteral(value, out var literal))
        {
            return cmp with { Right = literal };
        }

        return cmp with
        {
            Left = MaterializeExpr(cmp.Left, null),
            Right = MaterializeExpr(cmp.Right, null),
        };
    }

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