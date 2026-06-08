using LibParser.Omf;
using UltraDecompiler.Disassembler;
using UltraDecompiler.LibMatching;
using UltraDecompiler.Parser;

namespace LibMatchingTests;

/// <summary>Поиск смещения <c>_main</c> через crt0 <c>__astart</c> (и тесты универсального LibraryCallResolver).</summary>
public class MainOffsetFinderTests
{
    [Theory]
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void FindFromAstart_HelloMemoryModels(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(modelCase.ExeFileName));
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));
        var entryPoint = (int)parser.EntryPointOffset;

        var mainOffset = LibraryCallResolver.FindMainFromAstart(
            parser.Image,
            parser.RelocationTable,
            library,
            entryPoint,
            RegisterState.InitExe);

        var expectedMainOffset = modelCase.Name switch
        {
            "Small" or "Compact" => 0x10,
            "Medium" or "Large" => 0x0,
            _ => throw new InvalidOperationException($"Неизвестная модель: {modelCase.Name}"),
        };

        Assert.Equal(expectedMainOffset, mainOffset);
    }

    [Theory]
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void FindCalledSymbol_MainFromAstart_UsingGeneralApi(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(modelCase.ExeFileName));
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));
        var entryPoint = (int)parser.EntryPointOffset;

        // Проверяем универсальный API поиска произвольного символа по вызову из другого символа библиотеки.
        var mainOffset = LibraryCallResolver.FindCalledSymbol(
            parser.Image,
            parser.RelocationTable,
            library,
            callerSymbolName: "__astart",
            targetSymbolName: "_main",
            callerImageOffset: entryPoint,
            initRegisters: RegisterState.InitExe,
            callerModuleCodeOffset: 0);

        var expectedMainOffset = modelCase.Name switch
        {
            "Small" or "Compact" => 0x10,
            "Medium" or "Large" => 0x0,
            _ => throw new InvalidOperationException($"Неизвестная модель: {modelCase.Name}"),
        };

        Assert.Equal(expectedMainOffset, mainOffset);
    }
}
