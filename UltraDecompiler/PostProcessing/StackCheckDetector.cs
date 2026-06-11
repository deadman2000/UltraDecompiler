using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Определяет режим проверки стека QuickC по вызовам <c>_chkstk</c> в прологах функций
/// и удаляет эти вызовы из IR перед генерацией C-кода.
/// </summary>
public static class StackCheckDetector
{
    /// <summary>Имя runtime-функции проверки стека в сгенерированном C (<c>__chkstk</c> линкера).</summary>
    public const string ChkstkCName = "_chkstk";

    /// <summary>Имя символа в OMF-библиотеке.</summary>
    public const string ChkstkLinkerName = "__chkstk";

    /// <summary>
    /// Анализирует хранилище процедур: ищет вызов <c>_chkstk</c> в прологе пользовательских функций.
    /// </summary>
    public static bool Analyze(ProcedureStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var chkstkOffset = TryGetChkstkOffset(storage);
        return storage.All
            .Where(static p => !p.IsLibrary)
            .Any(p => HasChkstkAtEntry(p, chkstkOffset));
    }

    /// <summary>
    /// Определяет режим по плоскому списку операций (для одиночной функции без ProcedureStorage).
    /// </summary>
    public static bool AnalyzeFromOperations(IReadOnlyList<Operation> operations) =>
        ExpressionBuilder.EnumerateNested(operations).Any(IsChkstkOperation);

    /// <summary>Удаляет все вызовы <c>_chkstk</c> из дерева операций.</summary>
    public static IReadOnlyList<Operation> RemoveChkstkCalls(IReadOnlyList<Operation> operations) =>
        FilterList(operations);

    /// <summary>Проверяет, является ли операция вызовом <c>_chkstk</c>.</summary>
    public static bool IsChkstkOperation(Operation operation) =>
        operation switch
        {
            SetOperation { Src: CallExpr call } => IsChkstkName(call.Name),
            CallOperation call => IsChkstkName(call.Name),
            _ => false,
        };

    /// <summary>Проверяет, является ли имя вызовом runtime-проверки стека.</summary>
    public static bool IsChkstkName(string name) =>
        string.Equals(name, ChkstkCName, StringComparison.Ordinal)
        || string.Equals(name, ChkstkLinkerName, StringComparison.Ordinal);

    private static int? TryGetChkstkOffset(ProcedureStorage storage)
    {
        if (storage.TryGetByName(ChkstkCName, out var procedure) && procedure is not null)
        {
            return procedure.Offset;
        }

        foreach (var proc in storage.All.Where(static p => p.IsLibrary))
        {
            if (string.Equals(proc.LibraryMatch?.SymbolName, ChkstkLinkerName, StringComparison.Ordinal))
            {
                return proc.Offset;
            }
        }

        return null;
    }

    /// <summary>
    /// Проверяет пролог: после <c>push bp; mov bp, sp</c> (или <c>enter</c>) и опционального
    /// <c>mov ax, imm16</c> первый <c>call</c> ведёт на <c>_chkstk</c>.
    /// </summary>
    private static bool HasChkstkAtEntry(DisassembledProcedure procedure, int? chkstkOffset)
    {
        var instructions = procedure.Instructions;
        if (instructions.Count == 0)
        {
            return false;
        }

        var index = SkipPrologue(instructions);
        if (index < instructions.Count && IsMovAxImmediate(instructions[index]))
        {
            index++;
        }

        if (index >= instructions.Count || !instructions[index].IsCall)
        {
            return false;
        }

        var target = instructions[index].JumpTarget;
        if (chkstkOffset is int offset && target == offset)
        {
            return true;
        }

        return false;
    }

    private static int SkipPrologue(IReadOnlyList<Instruction> instructions)
    {
        if (instructions[0].Mnemonic == Mnemonic.ENTER)
        {
            return 1;
        }

        for (var i = 0; i < instructions.Count - 1; i++)
        {
            if (IsPushBp(instructions[i]) && IsMovBpSp(instructions[i + 1]))
            {
                return i + 2;
            }
        }

        return 0;
    }

    private static bool IsMovAxImmediate(Instruction instruction) =>
        instruction.Mnemonic == Mnemonic.MOV
        && instruction.Operand1.Type == OperandType.Register16
        && instruction.Operand1.AsGpRegister16() == GpRegister16.AX
        && instruction.Operand2.Type == OperandType.Immediate16;

    private static bool IsPushBp(Instruction instruction) =>
        instruction.Mnemonic == Mnemonic.PUSH
        && instruction.Operand1.Type == OperandType.Register16
        && instruction.Operand1.AsGpRegister16() == GpRegister16.BP;

    private static bool IsMovBpSp(Instruction instruction) =>
        instruction.Mnemonic == Mnemonic.MOV
        && instruction.Operand1.Type == OperandType.Register16
        && instruction.Operand1.AsGpRegister16() == GpRegister16.BP
        && instruction.Operand2.Type == OperandType.Register16
        && instruction.Operand2.AsGpRegister16() == GpRegister16.SP;

    private static List<Operation> FilterList(IReadOnlyList<Operation> operations)
    {
        var result = new List<Operation>(operations.Count);
        foreach (var operation in operations)
        {
            if (IsChkstkOperation(operation))
            {
                continue;
            }

            var transformed = TransformNested(operation);
            if (transformed is not null)
            {
                result.Add(transformed);
            }
        }

        return result;
    }

    private static Operation? TransformNested(Operation operation) =>
        operation switch
        {
            IfOperation branch => CreateIfOrNull(
                branch.Condition,
                FilterList(branch.ThenBody),
                branch.ElseBody is not null ? FilterList(branch.ElseBody) : null),
            WhileOperation loop => CreateWhileOrNull(loop.Condition, FilterList(loop.Body)),
            ForOperation loop => CreateForOrNull(
                loop.Init is not null ? FilterSingle(loop.Init) : null,
                loop.Condition,
                loop.Iteration is not null ? FilterSingle(loop.Iteration) : null,
                FilterList(loop.Body)),
            _ => operation,
        };

    private static IfOperation? CreateIfOrNull(
        Expr condition,
        IReadOnlyList<Operation> thenBody,
        IReadOnlyList<Operation>? elseBody)
    {
        if (thenBody.Count == 0 && (elseBody is null || elseBody.Count == 0))
        {
            return null;
        }

        return new IfOperation(condition, thenBody, elseBody);
    }

    private static WhileOperation? CreateWhileOrNull(Expr condition, IReadOnlyList<Operation> body) =>
        body.Count == 0 ? null : new WhileOperation(condition, body);

    private static ForOperation? CreateForOrNull(
        Operation? init,
        Expr? condition,
        Operation? iteration,
        IReadOnlyList<Operation> body)
    {
        if (init is null && iteration is null && body.Count == 0)
        {
            return null;
        }

        return new ForOperation(init, condition, iteration, body);
    }

    private static Operation? FilterSingle(Operation operation) =>
        IsChkstkOperation(operation) ? null : TransformNested(operation);
}
