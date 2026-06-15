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
        // Паттерн 1: CMP reg, immediate; JB/JL назад (классический /Ox с константой)
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

        // Паттерн 2: CMP reg, [BP+arg]; JGE/JL назад с INC reg в теле (оптимизированный цикл с argc)
        if (HasOxRegisterCompareWithStackVariable(instructions))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// <c>/Ox</c>: счётчик в регистре сравнивается с переменной из стека (argc),
    /// например: mov AX, [BP+something]; cmp SI, AX; jge ... (выход вперёд)
    /// </summary>
    private static bool HasOxRegisterCompareWithStackVariable(IReadOnlyList<Instruction> instructions)
    {
        // Ищем паттерн оптимизированного цикла QuickC /Ox:
        // - INC/DEC регистра (счётчик в конце цикла)
        // - MOV AX, [BP+arg] (загрузка argc)
        // - CMP reg, AX
        // - Jxx вперёд (выход из цикла)
        // - JMP назад (возврат в начало цикла)
        //
        // Структура:
        // body:
        //   ...
        //   inc SI          ; счётчик
        //   mov AX, [BP+4]  ; argc
        //   cmp SI, AX
        //   jge exit        ; вперёд
        //   jmp body        ; назад

        for (var i = 0; i < instructions.Count - 4; i++)
        {
            // Ищем MOV AX, [BP+something] (загрузка аргумента)
            if (instructions[i].Mnemonic != Mnemonic.MOV
                || instructions[i].Operand1.Type != OperandType.Register16
                || instructions[i].Operand1.AsGpRegister16() != GpRegister16.AX
                || instructions[i].Operand2.Type != OperandType.Memory
                || instructions[i].Operand2.BaseReg != AddressRegister.BP)
            {
                continue;
            }

            var loadOffset = instructions[i].Offset;

            // Ищем CMP reg, AX сразу после MOV
            if (i + 1 >= instructions.Count
                || instructions[i + 1].Mnemonic != Mnemonic.CMP
                || instructions[i + 1].Operand1.Type != OperandType.Register16
                || instructions[i + 1].Operand2.Type != OperandType.Register16
                || instructions[i + 1].Operand2.AsGpRegister16() != GpRegister16.AX)
            {
                continue;
            }

            var cmpInstr = instructions[i + 1];
            var cmpReg = cmpInstr.Operand1.AsGpRegister16();

            // Регистр должен быть SI, DI, BX или CX (регистры-индексы)
            if (cmpReg is not (GpRegister16.SI or GpRegister16.DI or GpRegister16.BX or GpRegister16.CX))
            {
                continue;
            }

            // Ищем условный переход вперёд сразу после CMP (выход из цикла)
            if (i + 2 >= instructions.Count)
            {
                continue;
            }

            var conditionalJump = instructions[i + 2];
            if (conditionalJump.Mnemonic is not (Mnemonic.JGE or Mnemonic.JG or Mnemonic.JAE or Mnemonic.JA))
            {
                continue;
            }

            var jumpTarget = conditionalJump.JumpTarget;
            // Переход должен быть вперёд
            if (jumpTarget < 0 || jumpTarget <= conditionalJump.Offset)
            {
                continue;
            }

            // Ищем INC/DEC регистра-счётчика в диапазоне от начала инструкции до MOV (перед загрузкой)
            // Это означает, что INC находится в теле цикла перед проверкой
            if (TryFindRegisterIncrementBeforeOffset(instructions, loadOffset, cmpReg))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Ищет INC/DEC регистра до указанного смещения (в теле цикла).
    /// </summary>
    private static bool TryFindRegisterIncrementBeforeOffset(
        IReadOnlyList<Instruction> instructions,
        int beforeOffset,
        GpRegister16 register)
    {
        for (var i = 0; i < instructions.Count; i++)
        {
            if (instructions[i].Offset >= beforeOffset)
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
