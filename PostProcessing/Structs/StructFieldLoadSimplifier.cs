namespace UltraDecompiler.PostProcessing.Structs;

/// <summary>
/// Подставляет <see cref="MemberExpr"/> вместо временных переменных, которым присвоено поле структуры.
/// </summary>
public static class StructFieldLoadSimplifier
{
    /// <summary>
    /// Сворачивает цепочку <c>temp = struct.field; … temp …</c> в прямое использование поля.
    /// </summary>
    public static IReadOnlyList<Operation> Simplify(IReadOnlyList<Operation> operations)
    {
        var fieldByTemp = CollectFieldAssignments(operations);
        return fieldByTemp.Count == 0
            ? operations
            : RewriteList(operations, fieldByTemp);
    }

    private static Dictionary<Variable, MemberExpr> CollectFieldAssignments(IEnumerable<Operation> operations)
    {
        var result = new Dictionary<Variable, MemberExpr>();

        foreach (var op in OperationFlattener.EnumerateNested(operations))
        {
            if (op is not SetOperation { Dst: VariableExpr { Var: var dst }, Src: MemberExpr member } || !dst.IsTemp)
            {
                continue;
            }

            result[dst] = member;
        }

        return result;
    }

    private static List<Operation> RewriteList(
        IReadOnlyList<Operation> operations,
        Dictionary<Variable, MemberExpr> replacements)
    {
        var result = new List<Operation>(operations.Count);

        foreach (var op in operations)
        {
            if (op is SetOperation { Dst: VariableExpr { Var: var dst } } && replacements.ContainsKey(dst))
            {
                continue;
            }

            result.Add(RewriteOperation(op, replacements));
        }

        return result;
    }

    private static Operation RewriteOperation(Operation op, Dictionary<Variable, MemberExpr> replacements) =>
        op switch
        {
            SetOperation set => new SetOperation(set.Dst, ReplaceExpr(set.Src, replacements)),
            StoreOperation store => new StoreOperation(
                ReplaceExpr(store.Address, replacements),
                store.Segment is null ? null : ReplaceExpr(store.Segment, replacements),
                ReplaceExpr(store.Value, replacements)),
            IncOperation inc => new IncOperation(
                ReplaceExpr(inc.Target, replacements),
                inc.Segment is null ? null : ReplaceExpr(inc.Segment, replacements)),
            DecOperation dec => new DecOperation(
                ReplaceExpr(dec.Target, replacements),
                dec.Segment is null ? null : ReplaceExpr(dec.Segment, replacements)),
            CallOperation call => new CallOperation(
                call.Name,
                call.Args.Select(arg => ReplaceExpr(arg, replacements)).ToList()),
            ReturnOperation ret => new ReturnOperation(
                ret.Value is null ? null : ReplaceExpr(ret.Value, replacements),
                ret.IsExplicit),
            IfOperation branch => new IfOperation(
                ReplaceExpr(branch.Condition, replacements),
                RewriteList(branch.ThenBody, replacements),
                branch.ElseBody is null ? null : RewriteList(branch.ElseBody, replacements)),
            WhileOperation loop => new WhileOperation(
                ReplaceExpr(loop.Condition, replacements),
                RewriteList(loop.Body, replacements)),
            ForOperation loop => new ForOperation(
                loop.Init is null ? null : RewriteOperation(loop.Init, replacements),
                loop.Condition is null ? null : ReplaceExpr(loop.Condition, replacements),
                loop.Iteration is null ? null : RewriteOperation(loop.Iteration, replacements),
                RewriteList(loop.Body, replacements)),
            _ => op,
        };

    private static Expr ReplaceExpr(Expr expr, Dictionary<Variable, MemberExpr> replacements) =>
        expr switch
        {
            VariableExpr { Var: var variable } when replacements.TryGetValue(variable, out var member) => member,
            Math1Expr m1 => m1 with { Op = ReplaceExpr(m1.Op, replacements) },
            Math2Expr m2 => m2 with
            {
                First = ReplaceExpr(m2.First, replacements),
                Second = ReplaceExpr(m2.Second, replacements),
            },
            MemExpr mem => mem with
            {
                Address = ReplaceExpr(mem.Address, replacements),
                Segment = mem.Segment is null ? null : ReplaceExpr(mem.Segment, replacements),
            },
            CmpExpr cmp => cmp with
            {
                Left = ReplaceExpr(cmp.Left, replacements),
                Right = ReplaceExpr(cmp.Right, replacements),
            },
            CallExpr call => call with
            {
                Args = call.Args.Select(arg => ReplaceExpr(arg, replacements)).ToList(),
            },
            AddressOfExpr addr => addr with { Operand = ReplaceExpr(addr.Operand, replacements) },
            MemberExpr member => member with { Base = ReplaceExpr(member.Base, replacements) },
            _ => expr,
        };
}