using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Заменяет обращения к словам стековой структуры на <see cref="MemberExpr"/>
/// и подставляет <see cref="AddressOfExpr"/> для аргументов <c>struct T *</c>.
/// </summary>
public static class StructFieldRewriter
{
    /// <summary>Переписывает операции процедуры с учётом зарегистрированных структур на стеке.</summary>
    public static IReadOnlyList<Operation> Rewrite(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations,
        ProcedureStorage storage,
        Headers.HeaderCatalog headers)
    {
        if (procedure.Expressions is null || procedure.Expressions.Variables.StructLocals.Count == 0)
        {
            return operations;
        }

        var variables = procedure.Expressions.Variables;
        return RewriteList(operations, variables, storage, headers);
    }

    private static List<Operation> RewriteList(
        IReadOnlyList<Operation> operations,
        VariableStorage variables,
        ProcedureStorage storage,
        Headers.HeaderCatalog headers) =>
        operations.Select(op => RewriteOperation(op, variables, storage, headers)).ToList();

    private static Operation RewriteOperation(
        Operation op,
        VariableStorage variables,
        ProcedureStorage storage,
        Headers.HeaderCatalog headers) =>
        op switch
        {
            SetOperation set => new SetOperation(
                set.Dst,
                RewriteExpr(set.Src, variables)),
            StoreOperation store => new StoreOperation(
                RewriteExpr(store.Address, variables),
                store.Segment is null ? null : RewriteExpr(store.Segment, variables),
                RewriteExpr(store.Value, variables)),
            IncOperation inc => new IncOperation(
                RewriteExpr(inc.Target, variables),
                inc.Segment is null ? null : RewriteExpr(inc.Segment, variables)),
            DecOperation dec => new DecOperation(
                RewriteExpr(dec.Target, variables),
                dec.Segment is null ? null : RewriteExpr(dec.Segment, variables)),
            CallOperation call => new CallOperation(
                call.Name,
                RewriteCallArgs(call.Name, call.Args, variables, storage, headers)),
            ReturnOperation ret => new ReturnOperation(
                ret.Value is null ? null : RewriteExpr(ret.Value, variables),
                ret.IsExplicit),
            IfOperation branch => new IfOperation(
                RewriteExpr(branch.Condition, variables),
                RewriteList(branch.ThenBody, variables, storage, headers),
                branch.ElseBody is null ? null : RewriteList(branch.ElseBody, variables, storage, headers)),
            WhileOperation loop => new WhileOperation(
                RewriteExpr(loop.Condition, variables),
                RewriteList(loop.Body, variables, storage, headers)),
            ForOperation loop => new ForOperation(
                loop.Init is null ? null : RewriteOperation(loop.Init, variables, storage, headers),
                loop.Condition is null ? null : RewriteExpr(loop.Condition, variables),
                loop.Iteration is null ? null : RewriteOperation(loop.Iteration, variables, storage, headers),
                RewriteList(loop.Body, variables, storage, headers)),
            _ => op,
        };

    private static IReadOnlyList<Expr> RewriteCallArgs(
        string name,
        IReadOnlyList<Expr> args,
        VariableStorage variables,
        ProcedureStorage storage,
        Headers.HeaderCatalog headers)
    {
        if (!TryResolveSignature(name, storage, headers, out var signature) || signature is null)
        {
            return args.Select(arg => RewriteExpr(arg, variables)).ToList();
        }

        var result = new List<Expr>(args.Count);
        for (var i = 0; i < args.Count; i++)
        {
            var rewritten = RewriteExpr(args[i], variables);
            if (i < signature.Parameters.Count
                && signature.Parameters[i].Type.IsStructPtr
                && rewritten is Variable { Type.IsStruct: true } structVar)
            {
                rewritten = new AddressOfExpr(structVar);
            }

            result.Add(rewritten);
        }

        return result;
    }

    private static Expr RewriteExpr(Expr expr, VariableStorage variables) =>
        expr switch
        {
            Variable variable => RewriteVariable(variable, variables),
            MemExpr mem => RewriteMemExpr(mem, variables),
            Math1Expr m1 => m1 with { Op = RewriteExpr(m1.Op, variables) },
            Math2Expr m2 => m2 with
            {
                First = RewriteExpr(m2.First, variables),
                Second = RewriteExpr(m2.Second, variables),
            },
            CmpExpr cmp => cmp with
            {
                Left = RewriteExpr(cmp.Left, variables),
                Right = RewriteExpr(cmp.Right, variables),
            },
            CallExpr call => call with
            {
                Args = call.Args.Select(arg => RewriteExpr(arg, variables)).ToList(),
            },
            AddressOfExpr addr => addr with { Operand = RewriteExpr(addr.Operand, variables) },
            MemberExpr member => member with { Base = RewriteExpr(member.Base, variables) },
            _ => expr,
        };

    private static Expr RewriteVariable(Variable variable, VariableStorage variables)
    {
        if (variables.TryGetMergedField(variable, out var member) && member is not null)
        {
            return member;
        }

        return variable;
    }

    private static Expr RewriteMemExpr(MemExpr mem, VariableStorage variables)
    {
        if (TryGetBpDisplacement(mem, out var displacement)
            && variables.TryResolveStructFieldAccess(displacement, out var member)
            && member is not null)
        {
            return member;
        }

        return mem with
        {
            Address = RewriteExpr(mem.Address, variables),
            Segment = mem.Segment is null ? null : RewriteExpr(mem.Segment, variables),
        };
    }

    private static bool TryGetBpDisplacement(MemExpr mem, out int displacement)
    {
        displacement = 0;

        if (!IsStackSegment(mem.Segment))
        {
            return false;
        }

        if (mem.Address is ConstExpr constant)
        {
            displacement = NormalizeDisplacement(constant.Value);
            return displacement < 0;
        }

        return false;
    }

    private static bool IsStackSegment(Expr? segment) =>
        segment is null
        || segment is Variable { Name: "varSS" or "_ss" or "SS" };

    private static int NormalizeDisplacement(int value) =>
        value > short.MaxValue ? (short)value : value;

    private static bool TryResolveSignature(
        string name,
        ProcedureStorage storage,
        Headers.HeaderCatalog headers,
        out ProcedureSignature? signature)
    {
        if (storage.TryGetByName(name, out var procedure)
            && procedure is not null
            && procedure.Signature != ProcedureSignature.Unknown)
        {
            signature = procedure.Signature;
            return true;
        }

        if (headers.TryGetSignature(name, out signature) && signature is not null)
        {
            return true;
        }

        signature = null;
        return false;
    }
}
