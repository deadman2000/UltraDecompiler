using LibMatching;
using LibParser.Omf;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Parser;

namespace LibMatchingTests;

/// <summary>Поиск смещения <c>_main</c> через crt0 <c>__astart</c>.</summary>
public class MainOffsetFinderTests
{
    [Theory]
    [MemberData(nameof(HelloMemoryModelCases.MemberData), MemberType = typeof(HelloMemoryModelCases))]
    public void FindFromAstart_HelloMemoryModels(HelloMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(modelCase.ExeFileName));
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));
        var entryPoint = (int)parser.EntryPointOffset;

        var mainOffset = MainOffsetFinder.FindFromAstart(
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
}
