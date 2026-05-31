using LibParser.Omf;
using UltraDecompiler.Disassembler;
using UltraDecompiler.LibMatching;
using UltraDecompiler.Parser;

namespace LibMatchingTests;

/// <summary>Сопоставление <c>__chkstk</c> в hello (small model).</summary>
public class ChkstkMatchingTests
{
    [Fact]
    public void Match_Chkstk_At027C_HelloS()
    {
        const int chkstkOffset = 0x27C;
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf("HELLO_S.EXE"));
        var lib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("SLIBCE.LIB"));

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            chkstkOffset,
            lib,
            RegisterState.InitExe,
            symbolName: "__chkstk",
            moduleName: null);

        Assert.Contains(matches, static m => m.SymbolName == "__chkstk");
    }
}
