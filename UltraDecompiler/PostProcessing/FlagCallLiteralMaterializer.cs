using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Подставляет <c>'h'</c>/<c>'v'</c> в вызовы предиката-флага (<c>sub_XXXX(..., 104)</c>).
/// </summary>
public static class FlagCallLiteralMaterializer
{
    /// <summary>Материализует литералы букв в вызовах <c>sub_*</c>.</summary>
    public static IReadOnlyList<Operation> Materialize(IReadOnlyList<Operation> operations) =>
        MaterializeList(operations.ToList());

    private static List<Operation> MaterializeList(List<Operation> operations)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = MaterializeNested(operations[i]);
        }

        return operations;
    }

    private static Operation MaterializeNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                MaterializeExpr(branch.Condition),
                MaterializeList(branch.ThenBody.ToList()),
                branch.ElseBody is not null ? MaterializeList(branch.ElseBody.ToList()) : null),
            WhileOperation loop => new WhileOperation(
                MaterializeExpr(loop.Condition),
                MaterializeList(loop.Body.ToList())),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? MaterializeNested(loop.Init) : null,
                MaterializeExpr(loop.Condition),
                loop.Iteration is not null ? MaterializeNested(loop.Iteration) : null,
                MaterializeList(loop.Body.ToList())),
            SwitchOperation sw => OperationTreeMapper.MapSwitchBodies(
                sw,
                bodies => MaterializeList(bodies.ToList())),
            CallOperation call => MaterializeCall(call),
            SetOperation set => new SetOperation(set.Dst, MaterializeExpr(set.Src)),
            _ => operation,
        };

    private static CallOperation MaterializeCall(CallOperation call)
    {
        if (!call.Name.StartsWith("sub_", StringComparison.Ordinal) || call.Args.Count < 2)
        {
            return call;
        }

        var args = call.Args.ToArray();
        if (args[1] is ConstExpr { Value: var value } && TryToCharLiteral(value, out var literal))
        {
            args[1] = literal;
            return new CallOperation(call.Name, args);
        }

        return call;
    }

    private static Expr MaterializeExpr(Expr? expr)
    {
        if (expr is null)
        {
            return ConstExpr.Zero;
        }

        return expr switch
        {
            CallExpr call => MaterializeCallExpr(call),
            CmpExpr cmp => cmp with
            {
                Left = MaterializeExpr(cmp.Left),
                Right = MaterializeExpr(cmp.Right),
            },
            Math1Expr m1 => m1 with { Op = MaterializeExpr(m1.Op) },
            Math2Expr m2 => m2 with
            {
                First = MaterializeExpr(m2.First),
                Second = MaterializeExpr(m2.Second),
            },
            _ => expr,
        };
    }

    private static CallExpr MaterializeCallExpr(CallExpr call)
    {
        if (!call.Name.StartsWith("sub_", StringComparison.Ordinal) || call.Args.Count < 2)
        {
            return call;
        }

        var args = call.Args.ToArray();
        if (args[1] is ConstExpr { Value: var value } && TryToCharLiteral(value, out var literal))
        {
            args[1] = literal;
            return call with { Args = args };
        }

        return call;
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
