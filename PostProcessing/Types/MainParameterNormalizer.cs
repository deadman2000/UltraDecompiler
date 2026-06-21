namespace UltraDecompiler.PostProcessing.Types;

/// <summary>
/// Восстанавливает стандартные параметры <c>main</c>: <c>argc</c>, <c>argv[]</c> и опционально <c>envp[]</c>.
/// </summary>
public static class MainParameterNormalizer
{
    private const int ArgcOffset = 4;
    private const int ArgvOffset = 6;
    private const int EnvpOffset = 8;

    /// <summary>
    /// Нормализует параметры и сигнатуру <c>main</c>, переименовывает переменные в IR.
    /// </summary>
    public static IReadOnlyList<Operation> Normalize(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations)
    {
        if (procedure.Name != "main" || procedure.Expressions is null)
        {
            return operations;
        }

        var hasArgc = DetectBpOffsetAccess(procedure.Instructions, ArgcOffset);
        var hasArgv = DetectBpOffsetAccess(procedure.Instructions, ArgvOffset);
        var hasEnvp = DetectBpOffsetAccess(procedure.Instructions, EnvpOffset);

        if (!hasArgc && !hasArgv && !hasEnvp)
        {
            procedure.Expressions.SetParameters([]);
            procedure.Signature = new ProcedureSignature(procedure.Signature.ReturnType, []);
            return operations;
        }

        if (hasEnvp)
        {
            hasArgc = true;
            hasArgv = true;
        }
        else if (hasArgv)
        {
            hasArgc = true;
        }

        var (parameters, renames) = procedure.Expressions.Variables.EnsureMainParameters(
            hasArgc,
            hasArgv,
            hasEnvp);
        procedure.Expressions.SetParameters(parameters);

        var signatureParams = parameters
            .Select(static fp => new ProcedureParameter(
                fp.StackOffset switch
                {
                    ArgcOffset => CType.Int,
                    ArgvOffset => CType.CharPtrPtr,
                    EnvpOffset => CType.CharPtrPtr,
                    _ => CType.Int,
                },
                new StackParameter(fp.StackOffset)))
            .ToList();

        procedure.Signature = new ProcedureSignature(
            procedure.Signature.ReturnType,
            signatureParams);

        return RewriteOperations(operations, renames);
    }

    private static bool DetectBpOffsetAccess(IReadOnlyList<Instruction> instructions, int offset)
    {
        foreach (var instr in instructions)
        {
            if (HasBpOffset(instr.Operand1, offset) || HasBpOffset(instr.Operand2, offset))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBpOffset(Operand operand, int offset) =>
        operand.Type == OperandType.Memory
        && operand.BaseReg == AddressRegister.BP
        && operand.IndexReg == AddressRegister.None
        && operand.Value == offset;

    private static IReadOnlyList<Operation> RewriteOperations(
        IReadOnlyList<Operation> operations,
        IReadOnlyDictionary<Variable, Variable> renames)
    {
        var list = operations.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            list[i] = RewriteNested(list[i], renames);
        }

        return list;
    }

    private static Operation RewriteNested(
        Operation operation,
        IReadOnlyDictionary<Variable, Variable> renames) =>
        operation switch
        {
            SetOperation set => set with { Src = RewriteExpr(set.Src, renames), Dst = RewriteExpr(set.Dst, renames) },
            StoreOperation store => store with
            {
                Address = RewriteExpr(store.Address, renames),
                Segment = store.Segment is null ? null : RewriteExpr(store.Segment, renames),
                Value = RewriteExpr(store.Value, renames),
            },
            CallOperation call => call with { Args = call.Args.Select(arg => RewriteExpr(arg, renames)).ToList() },
            ReturnOperation ret => ret with
            {
                Value = ret.Value is null ? null : RewriteExpr(ret.Value, renames),
            },
            IncOperation inc => inc with
            {
                Target = RewriteExpr(inc.Target, renames),
                Segment = inc.Segment is null ? null : RewriteExpr(inc.Segment, renames),
            },
            DecOperation dec => dec with
            {
                Target = RewriteExpr(dec.Target, renames),
                Segment = dec.Segment is null ? null : RewriteExpr(dec.Segment, renames),
            },
            IfOperation branch => new IfOperation(
                RewriteExpr(branch.Condition, renames),
                RewriteOperations(branch.ThenBody, renames),
                branch.ElseBody is null ? null : RewriteOperations(branch.ElseBody, renames)),
            WhileOperation loop => new WhileOperation(
                RewriteExpr(loop.Condition, renames),
                RewriteOperations(loop.Body, renames)),
            ForOperation loop => new ForOperation(
                loop.Init is null ? null : RewriteNested(loop.Init, renames),
                loop.Condition is null ? null : RewriteExpr(loop.Condition, renames),
                loop.Iteration is null ? null : RewriteNested(loop.Iteration, renames),
                RewriteOperations(loop.Body, renames)),
            _ => operation,
        };

    private static Variable Rename(Variable variable, IReadOnlyDictionary<Variable, Variable> renames) =>
        renames.TryGetValue(variable, out var renamed) ? renamed : variable;

    private static Expr RewriteExpr(Expr expr, IReadOnlyDictionary<Variable, Variable> renames)
    {
        if (expr is VariableExpr { Var: var variable })
        {
            return Rename(variable, renames).ToGet();
        }

        return expr switch
        {
            MemExpr mem => RewriteMemExpr(mem, renames),
            Math1Expr m => m with { Op = RewriteExpr(m.Op, renames) },
            Math2Expr m => m with
            {
                First = RewriteExpr(m.First, renames),
                Second = RewriteExpr(m.Second, renames),
            },
            CmpExpr cmp => cmp with
            {
                Left = RewriteExpr(cmp.Left, renames),
                Right = RewriteExpr(cmp.Right, renames),
            },
            CallExpr call => call with { Args = call.Args.Select(arg => RewriteExpr(arg, renames)).ToList() },
            MemberExpr member => member with { Base = RewriteExpr(member.Base, renames) },
            IncDecExpr inc => inc with { Operand = RewriteExpr(inc.Operand, renames) },
            AddressOfExpr addr => addr with { Operand = RewriteExpr(addr.Operand, renames) },
            _ => expr,
        };
    }

    private static Expr RewriteMemExpr(MemExpr mem, IReadOnlyDictionary<Variable, Variable> renames)
    {
        var renamedMem = mem with
        {
            Address = RewriteExpr(mem.Address, renames),
            Segment = mem.Segment is null ? null : RewriteExpr(mem.Segment, renames),
        };

        if (CharPtrArrayFormatter.TryRewriteLoad(renamedMem, out var rewritten))
        {
            return rewritten;
        }

        return renamedMem;
    }
}
