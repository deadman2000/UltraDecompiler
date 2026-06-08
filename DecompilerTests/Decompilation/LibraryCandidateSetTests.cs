using LibParser.Models;
using UltraDecompiler.LibMatching;

namespace DecompilerTests.Decompilation;

public class LibraryCandidateSetTests
{
    [Fact]
    public void NarrowBySymbol_RemovesOtherLibrariesWithSameSymbol()
    {
        var slibce = CreateLibrary("SLIBCE.LIB", ["__chkstk", "_printf"]);
        var clibc = CreateLibrary("CLIBC.LIB", ["__chkstk", "_printf"]);
        var math = CreateLibrary("MATH.LIB", ["_sin"]);

        var set = new LibraryCandidateSet([slibce, clibc, math]);
        set.NarrowBySymbol(slibce, "_printf");

        Assert.Equal(["MATH.LIB", "SLIBCE.LIB"], set.Candidates.Select(l => l.FileName).OrderBy(static n => n));
        Assert.Equal(["SLIBCE.LIB"], set.LinkedFileNames);
    }

    private static OmfLibrary CreateLibrary(string fileName, string[] symbols)
    {
        var dict = symbols.ToDictionary(
            static s => s,
            static s => new OmfPublicSymbol { Name = s, ModulePage = 1 });

        return new OmfLibrary
        {
            FileName = fileName,
            Header = new OmfLibraryHeader
            {
                PageSize = 16,
                DictionaryOffset = 0,
                DictionaryBlockCount = 1,
                Flags = 0,
            },
            Modules = [],
            Symbols = dict,
            RawData = [],
        };
    }
}
