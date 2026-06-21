using LibParser.Models;
using UltraDecompiler.Common;

namespace LibMatchingTests;

internal static class LibMatchingTestHelpers
{
    public static IReadOnlyList<Instruction> Disassemble(
        byte[] image,
        RelocationTable? relocations = null,
        int startOffset = 0) =>
        Disassemble(image, relocations ?? RelocationTable.Empty, startOffset, RegisterState.Unknown);

    public static IReadOnlyList<Instruction> Disassemble(
        byte[] image,
        RelocationTable relocations,
        int startOffset,
        RegisterState initRegisters)
    {
        var disassembler = new X86Disassembler(image, relocations);
        disassembler.Disassemble(startOffset, initRegisters);
        return disassembler.Instructions.OrderBy(static i => i.Offset).ToList();
    }

    public static (OmfModule Module, OmfSegmentData Code) RequireModule(
        OmfLibrary library,
        string symbolName)
    {
        var module = library.FindModuleBySymbol(symbolName)
            ?? throw new InvalidOperationException($"Символ {symbolName} не найден.");
        var code = module.CodeSegments.FirstOrDefault()
            ?? throw new InvalidOperationException($"У модуля {symbolName} нет CODE-сегмента.");
        return (module, code);
    }
}
