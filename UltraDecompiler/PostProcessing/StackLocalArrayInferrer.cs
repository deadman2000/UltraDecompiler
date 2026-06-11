using UltraDecompiler.Decompilation;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Восстанавливает объявления локальных массивов <c>char[N]</c> на стеке по смещениям <c>[BP-offset]</c>.
/// </summary>
public static class StackLocalArrayInferrer
{
    /// <summary>
    /// Помечает локали стекового кадра как <c>char[N]</c>, вычисляя N по границам между соседними смещениями.
    /// </summary>
    public static void Infer(DisassembledProcedure procedure, IReadOnlyList<Operation> operations)
    {
        if (procedure.Expressions is null)
        {
            return;
        }

        var stackLocals = procedure.Expressions.Variables.StackLocals;
        if (stackLocals.Count == 0)
        {
            InferFromOperations(procedure, operations);
            return;
        }

        var sorted = stackLocals.OrderBy(static e => e.Offset).ToList();
        var arrayOffsets = CollectLeaLocalOffsets(procedure.Instructions);

        for (var i = 0; i < sorted.Count; i++)
        {
            var entry = sorted[i];
            if (!arrayOffsets.Contains(entry.Offset))
            {
                continue;
            }

            var size = ComputeArraySize(sorted, i);
            if (size > 0)
            {
                MarkCharArray(entry.Variable, size);
            }
        }
    }

    /// <summary>Смещения локалов, чей адрес берётся через <c>lea reg, [BP-disp]</c> — базы массивов.</summary>
    private static HashSet<int> CollectLeaLocalOffsets(IReadOnlyList<Instruction> instructions)
    {
        var offsets = new HashSet<int>();

        foreach (var instr in instructions)
        {
            if (instr.Mnemonic != Mnemonic.LEA
                || instr.Operand2.Type != OperandType.Memory
                || instr.Operand2.BaseReg != AddressRegister.BP
                || instr.Operand2.IndexReg != AddressRegister.None
                || instr.Operand2.Value >= 0
                || instr.Operand2.Value % 2 != 0)
            {
                continue;
            }

            offsets.Add(instr.Operand2.Value);
        }

        return offsets;
    }

    /// <summary>
    /// Размер массива по базе <paramref name="offset"/> и следующему более высокому локальному смещению (или BP).
    /// </summary>
    private static int ComputeArraySize(IReadOnlyList<(int Offset, Variable Variable)> sortedByOffset, int index)
    {
        var offset = sortedByOffset[index].Offset;

        if (index < sortedByOffset.Count - 1)
        {
            // Между buf2@[BP-50] и buf@[BP-20]: 30 байт.
            return sortedByOffset[index + 1].Offset - offset;
        }

        // Верхний массив в кадре: от [BP+offset] до BP (offset отрицательный).
        return -offset;
    }

    /// <summary>
    /// Fallback, если смещения [BP-disp] не собраны: одна переменная + размер кадра из пролога.
    /// </summary>
    private static void InferFromOperations(DisassembledProcedure procedure, IReadOnlyList<Operation> operations)
    {
        var allocSize = TryGetStackFrameAllocationSize(procedure.Instructions);
        if (allocSize is null or <= 0)
        {
            return;
        }

        var paramVars = procedure.Expressions!.Parameters
            .Select(static p => p.Variable)
            .ToHashSet();

        var locals = new HashSet<Variable>();
        foreach (var op in ExpressionBuilder.EnumerateNested(operations))
        {
            foreach (var variable in CollectOperationVariables(op))
            {
                if (paramVars.Contains(variable)
                    || variable.Name is "_psp" or "retAddr"
                    || variable.Name?.StartsWith("arg", StringComparison.Ordinal) == true)
                {
                    continue;
                }

                locals.Add(variable);
            }
        }

        if (locals.Count != 1)
        {
            return;
        }

        MarkCharArray(locals.First(), allocSize.Value);
    }

    private static void MarkCharArray(Variable variable, int allocSize)
    {
        variable.Type = CType.Char;
        variable.ArraySize = allocSize;
    }

    private static IEnumerable<Variable> CollectOperationVariables(Operation op) =>
        op switch
        {
            SetOperation set => ExprSubstitution.CollectVariables(set.Src).Concat([set.Dst]),
            StoreOperation store => ExprSubstitution.CollectVariables(store.Address)
                .Concat(ExprSubstitution.CollectVariables(store.Segment))
                .Concat(ExprSubstitution.CollectVariables(store.Value)),
            CallOperation call => call.Args.SelectMany(ExprSubstitution.CollectVariables),
            ReturnOperation ret => ExprSubstitution.CollectVariables(ret.Value),
            IfOperation branch => ExprSubstitution.CollectVariables(branch.Condition)
                .Concat(branch.ThenBody.SelectMany(CollectOperationVariables))
                .Concat(branch.ElseBody?.SelectMany(CollectOperationVariables) ?? []),
            WhileOperation loop => ExprSubstitution.CollectVariables(loop.Condition)
                .Concat(loop.Body.SelectMany(CollectOperationVariables)),
            ForOperation loop => (loop.Init is not null ? CollectOperationVariables(loop.Init) : [])
                .Concat(ExprSubstitution.CollectVariables(loop.Condition))
                .Concat(loop.Iteration is not null ? CollectOperationVariables(loop.Iteration) : [])
                .Concat(loop.Body.SelectMany(CollectOperationVariables)),
            _ => [],
        };

    private static int? TryGetStackFrameAllocationSize(IReadOnlyList<Instruction> instructions) =>
        TryGetChkstkAllocationSize(instructions)
        ?? TryGetSubSpAllocationSize(instructions)
        ?? TryGetEnterAllocationSize(instructions);

    private static int? TryGetChkstkAllocationSize(IReadOnlyList<Instruction> instructions)
    {
        for (var i = 0; i < instructions.Count - 1; i++)
        {
            if (instructions[i].Mnemonic != Mnemonic.MOV
                || instructions[i].Operand1.Type != OperandType.Register16
                || instructions[i].Operand1.AsGpRegister16() != GpRegister16.AX
                || instructions[i].Operand2.Type != OperandType.Immediate16)
            {
                continue;
            }

            if (instructions[i + 1].Mnemonic != Mnemonic.CALL)
            {
                continue;
            }

            return instructions[i].Operand2.Value;
        }

        return null;
    }

    /// <summary>QuickC с <c>/Gs</c>: <c>sub sp, N</c> сразу после <c>mov bp, sp</c>.</summary>
    private static int? TryGetSubSpAllocationSize(IReadOnlyList<Instruction> instructions)
    {
        for (var i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (instr.Mnemonic != Mnemonic.SUB
                || instr.Operand1.Type != OperandType.Register16
                || instr.Operand1.AsGpRegister16() != GpRegister16.SP
                || instr.Operand2.Type != OperandType.Immediate16)
            {
                continue;
            }

            if (!HasRecentMovBpSp(instructions, i))
            {
                continue;
            }

            return instr.Operand2.Value;
        }

        return null;
    }

    /// <summary><c>enter N, 0</c> — альтернативный пролог выделения локалов.</summary>
    private static int? TryGetEnterAllocationSize(IReadOnlyList<Instruction> instructions)
    {
        foreach (var instr in instructions)
        {
            if (instr.Mnemonic == Mnemonic.ENTER
                && instr.Operand1.Type == OperandType.Immediate16
                && instr.Operand1.Value > 0)
            {
                return instr.Operand1.Value;
            }
        }

        return null;
    }

    private static bool HasRecentMovBpSp(IReadOnlyList<Instruction> instructions, int subSpIndex)
    {
        for (var i = subSpIndex - 1; i >= 0 && subSpIndex - i <= 4; i--)
        {
            var instr = instructions[i];
            if (instr.Mnemonic == Mnemonic.MOV
                && instr.Operand1.Type == OperandType.Register16
                && instr.Operand1.AsGpRegister16() == GpRegister16.BP
                && instr.Operand2.Type == OperandType.Register16
                && instr.Operand2.AsGpRegister16() == GpRegister16.SP)
            {
                return true;
            }
        }

        return false;
    }
}
