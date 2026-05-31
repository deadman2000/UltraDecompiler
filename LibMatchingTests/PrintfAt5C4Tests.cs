using LibParser.Omf;
using UltraDecompiler.Disassembler;
using UltraDecompiler.LibMatching;
using UltraDecompiler.Parser;

namespace LibMatchingTests;

public class PrintfAt5C4Tests
{
    [Theory]
    [MemberData(nameof(HelloMemoryModelCases.MemberData), MemberType = typeof(HelloMemoryModelCases))]
    public void Match_Printf_AtDiscoveredOffset(HelloMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(modelCase.ExeFileName));
        var lib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));
        var printfOffset = PrintfOffsetFinder.Find(parser, lib);

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            printfOffset,
            lib,
            RegisterState.InitExe,
            symbolName: "_printf",
            moduleName: null);

        Assert.Contains(matches, static m => m.SymbolName == "_printf");
    }

    [Fact]
    public void Match_Printf_At5C4_HelloS()
    {
        const int printfOffset = 0x5C4;
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf("HELLO_S.EXE"));
        var lib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("SLIBCE.LIB"));

        var finderOffset = PrintfOffsetFinder.Find(parser, lib);

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            printfOffset,
            lib,
            RegisterState.InitExe,
            symbolName: "_printf",
            moduleName: null);

        Assert.Equal(printfOffset, finderOffset);
        Assert.Contains(matches, static m => m.SymbolName == "_printf");
    }
}
