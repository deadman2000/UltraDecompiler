using UltraDecompiler.Ir.Builder;

namespace UltraDecompiler.PostProcessing.Structs;

/// <summary>
/// Подставляет <see cref="MemberExpr"/> вместо временных переменных, которым присвоено поле структуры.
/// </summary>
public static class StructFieldLoadSimplifier
{
    public static IReadOnlyList<Operation> Simplify(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations)
    {
        var fieldByTemp = CollectFieldAssignments(operations);
        var rewritten = fieldByTemp.Count == 0
            ? operations
            : RewriteList(operations, fieldByTemp);

        if (procedure.Expressions is null || procedure.Expressions.Variables.StructLocals.Count == 0)
        {
            return rewritten;
        }

        return RewritePrintfByteFields(procedure, rewritten, procedure.Expressions.Variables);
    }

    /// <summary>
    /// Заменяет временные аргументы <c>printf</c> на поля структуры по последовательности
    /// <c>mov r8, [BP+disp]</c> перед вызовом (паттерн <c>dos.c</c>).
    /// </summary>
    private static IReadOnlyList<Operation> RewritePrintfByteFields(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations,
        VariableStorage variables)
    {
        var byteFields = CollectByteFieldLoads(procedure.Instructions, variables);
        if (byteFields.Count == 0)
        {
            return operations;
        }

        return operations
            .Select(op => RewritePrintfOperation(op, byteFields))
            .ToList();
    }

    private static List<MemberExpr> CollectByteFieldLoads(
        IReadOnlyList<Instruction> instructions,
        VariableStorage variables)
    {
        var result = new List<MemberExpr>();

        foreach (var instr in instructions)
        {
            if (instr.Mnemonic != Mnemonic.MOV
                || instr.Operand1.Type != OperandType.Register8
                || instr.Operand2.Type != OperandType.Memory
                || instr.Operand2.BaseReg != AddressRegister.BP
                || instr.Operand2.IndexReg != AddressRegister.None)
            {
                continue;
            }

            if (!variables.TryResolveStructFieldAccess(instr.Operand2.Value, out var member) || member is null)
            {
                continue;
            }

            result.Add(member);
        }

        return result;
    }

    private static Operation RewritePrintfOperation(Operation op, IReadOnlyList<MemberExpr> byteFields) =>
        op switch
        {
            SetOperation { Src: CallExpr { Name: "printf" } call } set
                => new SetOperation(set.Dst, call with { Args = RewritePrintfArgs(call.Args, byteFields) }),
            CallOperation { Name: "printf" } call
                => call with { Args = RewritePrintfArgs(call.Args, byteFields) },
            IfOperation branch => new IfOperation(
                branch.Condition,
                RewritePrintfList(branch.ThenBody, byteFields),
                branch.ElseBody is null ? null : RewritePrintfList(branch.ElseBody, byteFields)),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                RewritePrintfList(loop.Body, byteFields)),
            ForOperation loop => new ForOperation(
                loop.Init,
                loop.Condition,
                loop.Iteration,
                RewritePrintfList(loop.Body, byteFields)),
            _ => op,
        };

    private static List<Operation> RewritePrintfList(
        IReadOnlyList<Operation> operations,
        IReadOnlyList<MemberExpr> byteFields) =>
        operations.Select(op => RewritePrintfOperation(op, byteFields)).ToList();

    private static IReadOnlyList<Expr> RewritePrintfArgs(
        IReadOnlyList<Expr> args,
        IReadOnlyList<MemberExpr> byteFields)
    {
        if (args.Count < 3 || byteFields.Count == 0)
        {
            return args;
        }

        var result = args.ToList();

        // cdecl variadic: после форматной строки идут аргументы слева направо (month, day, year в dos.c).
        // mov al,[day]; mov al,[month] — в коде QuickC сначала day, затем month.
        if (byteFields.Count >= 2
            && result.Count >= 3
            && result[1] is Variable monthTemp
            && result[2] is Variable dayTemp
            && monthTemp.IsTemp
            && dayTemp.IsTemp)
        {
            result[1] = byteFields[^1];
            result[2] = byteFields[^2];
            return result;
        }

        if (result[1] is Variable firstTemp && firstTemp.IsTemp && byteFields.Count >= 1)
        {
            result[1] = byteFields[0];
        }

        return result;
    }

    private static Dictionary<Variable, MemberExpr> CollectFieldAssignments(IEnumerable<Operation> operations)
    {
        var result = new Dictionary<Variable, MemberExpr>();

        foreach (var op in ExpressionBuilder.EnumerateNested(operations))
        {
            if (op is not SetOperation { Dst: Variable dst, Src: MemberExpr member } || !dst.IsTemp)
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
            if (op is SetOperation { Dst: Variable dst } && replacements.ContainsKey(dst))
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
            Variable variable when replacements.TryGetValue(variable, out var member) => member,
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
