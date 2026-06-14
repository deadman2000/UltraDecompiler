using UltraDecompiler.Compilation;
using UltraDecompiler.PostProcessing.Epilogue;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Эвристики определения уровня оптимизации QuickC по телам user-процедур.
/// </summary>
public static class OptimizationLevelHeuristics
{
    private const double OdTailEpilogRatioThreshold = 0.6;

    /// <summary>
    /// Определяет <c>/Od</c> vs <c>/Ox</c> по паттернам выхода из функций.
    /// </summary>
    public static OptimizationLevel DetectFromUserProcedures(IEnumerable<DisassembledProcedure> procedures)
    {
        var userProcs = procedures.Where(static p => !p.IsLibrary).ToList();
        if (userProcs.Count == 0)
        {
            return OptimizationLevel.Disabled;
        }

        if (userProcs.Any(static p => HasOxRegisterCounterLoopPattern(p.Instructions)))
        {
            return OptimizationLevel.EnabledFull;
        }

        var main = userProcs.FirstOrDefault(static p =>
            string.Equals(p.Name, "main", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Name, "_main", StringComparison.OrdinalIgnoreCase));

        if (main is not null && HasTailEpilogJump(main.Instructions))
        {
            return OptimizationLevel.Disabled;
        }

        if (userProcs.Any(static p =>
                !HasTailEpilogJump(p.Instructions) && HasImulInstruction(p.Instructions)))
        {
            return OptimizationLevel.EnabledFull;
        }

        var tailEpilogCount = userProcs.Count(static p => HasTailEpilogJump(p.Instructions));
        var ratio = (double)tailEpilogCount / userProcs.Count;

        return ratio >= OdTailEpilogRatioThreshold
            ? OptimizationLevel.Disabled
            : OptimizationLevel.EnabledFull;
    }

    /// <summary>
    /// <c>/Ox</c>: функция завершается inline-эпилогом без JMP в общий хвост.
    /// </summary>
    public static bool HasInlineEpilogueExit(IReadOnlyList<Instruction> instructions)
    {
        if (instructions.Count == 0)
        {
            return false;
        }

        return !HasTailEpilogJump(instructions)
            && instructions.Any(static i => i.IsReturn);
    }

    /// <summary>
    /// Есть безусловный JMP вперёд в блок общего эпилога (характерно для <c>/Od</c>).
    /// </summary>
    public static bool HasTailEpilogJump(IReadOnlyList<Instruction> instructions)
    {
        for (var i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (!instr.IsUnconditionalJump)
            {
                continue;
            }

            var target = instr.JumpTarget;
            if (target < 0 || target <= instr.Offset)
            {
                continue;
            }

            if (!TrySliceFromOffset(instructions, target, out var tail))
            {
                continue;
            }

            if (EpilogueAnalyzer.IsEpilogueTailBlock(tail))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>/Ox</c>: счётчик цикла в регистре (cmp reg,N; jb body) с inc reg в теле.
    /// </summary>
    public static bool HasOxRegisterCounterLoopPattern(IReadOnlyList<Instruction> instructions)
    {
        for (var i = 0; i < instructions.Count - 1; i++)
        {
            var instr = instructions[i];
            if (instr.Mnemonic != Mnemonic.CMP
                || instr.Operand1.Type != OperandType.Register16
                || instr.Operand2.Type != OperandType.Immediate16)
            {
                continue;
            }

            if (instructions[i + 1].Mnemonic is not (Mnemonic.JB or Mnemonic.JL))
            {
                continue;
            }

            var backTarget = instructions[i + 1].JumpTarget;
            if (backTarget < 0 || backTarget >= instr.Offset)
            {
                continue;
            }

            var reg = instr.Operand1.AsGpRegister16();
            if (reg is not (GpRegister16.SI or GpRegister16.DI or GpRegister16.BX or GpRegister16.CX))
            {
                continue;
            }

            if (TryFindRegisterIncrement(instructions, backTarget, instr.Offset, reg))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc cref="HasOxRegisterCounterLoopPattern"/>
    public static bool HasOxRegisterIncrementPattern(IReadOnlyList<Instruction> instructions) =>
        HasOxRegisterCounterLoopPattern(instructions);

    private static bool TryFindRegisterIncrement(
        IReadOnlyList<Instruction> instructions,
        int bodyStart,
        int testOffset,
        GpRegister16 register)
    {
        for (var i = 0; i < instructions.Count; i++)
        {
            if (instructions[i].Offset < bodyStart || instructions[i].Offset >= testOffset)
            {
                continue;
            }

            if (instructions[i].Mnemonic is Mnemonic.INC or Mnemonic.DEC
                && instructions[i].Operand1.Type == OperandType.Register16
                && instructions[i].Operand1.AsGpRegister16() == register)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasImulInstruction(IReadOnlyList<Instruction> instructions) =>
        instructions.Any(static i => i.Mnemonic == Mnemonic.IMUL);

    private static bool TrySliceFromOffset(
        IReadOnlyList<Instruction> instructions,
        int offset,
        out Instruction[] tail)
    {
        for (var j = 0; j < instructions.Count; j++)
        {
            if (instructions[j].Offset != offset)
            {
                continue;
            }

            tail = instructions.Skip(j).ToArray();
            return true;
        }

        tail = [];
        return false;
    }
}
