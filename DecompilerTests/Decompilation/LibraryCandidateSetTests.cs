using LibParser.Models;
using UltraDecompiler.Decompilation;

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

    [Fact]
    public void NarrowByEntryPoint_KeepsOnlyCrt0Matches()
    {
        var slibce = CreateLibrary("SLIBCE.LIB", ["__astart"]);
        var math = CreateLibrary("MATH.LIB", ["_sin"]);

        var set = new LibraryCandidateSet([slibce, math]);
        set.NarrowByEntryPointMatches([
            new EntryPointLibraryMatchInfo
            {
                Library = slibce,
                Matches =
                [
                    new LibraryMatchInfo
                    {
                        SymbolName = "__astart",
                        ModulePage = 1,
                        ModuleName = "crt0",
                        ModuleCodeOffset = 0,
                        LibraryFileName = slibce.FileName,
                    },
                ],
            },
        ]);

        Assert.Single(set.Candidates);
        Assert.Equal("SLIBCE.LIB", set.Candidates[0].FileName);
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
