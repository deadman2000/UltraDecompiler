namespace Tools;

/// <summary>
/// Проверяет, что смещение указывает на начало процедуры QuickC (стандартный пролог).
/// </summary>
internal static class FunctionStartValidator
{
    /// <summary>
    /// Декодирует первые инструкции с <paramref name="offset"/> и проверяет пролог
    /// (<c>push bp; mov bp, sp</c> или <c>enter</c>).
    /// </summary>
    public static bool IsFunctionStart(DosExeParser parser, int offset)
    {
        ArgumentNullException.ThrowIfNull(parser);

        if (offset < 0 || offset >= parser.Image.Length)
        {
            return false;
        }

        var initRegisters = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;
        var instructions = X86Disassembler.Disassemble(
            parser.Image,
            parser.RelocationTable,
            offset,
            initRegisters);

        var prefix = CollectLinearPrefix(instructions, offset);
        return prefix.Count > 0 && PrologueDetector.HasStandardPrologue(prefix);
    }

    /// <summary>
    /// Берёт непрерывную цепочку инструкций от точки входа (без пропусков по смещению).
    /// </summary>
    private static List<Instruction> CollectLinearPrefix(IReadOnlyList<Instruction> instructions, int offset)
    {
        var ordered = instructions
            .Where(i => i.Offset >= offset)
            .OrderBy(i => i.Offset)
            .ToList();

        var prefix = new List<Instruction>();
        var expected = offset;

        foreach (var instr in ordered)
        {
            if (instr.Offset != expected)
            {
                break;
            }

            prefix.Add(instr);
            expected += instr.Size;

            if (prefix.Count >= 4)
            {
                break;
            }
        }

        return prefix;
    }
}
