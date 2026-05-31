using Common;
using LibParser.Omf;
using UltraDecompiler.LibMatching;

namespace LibMatchingTests;

/// <summary>Сопоставление кода модулей библиотеки (без EXE).</summary>
public class LibModuleMatchingTests
{
    [Theory]
    [InlineData("SLIBCE.LIB", "_printf")]
    [InlineData("CLIBC.LIB", "_printf")]
    [InlineData("MLIBC.LIB", "_printf")]
    [InlineData("LLIBC.LIB", "_printf")]
    public void Match_ModuleCodeAgainstItself_FindsSymbol(string libFileName, string symbolName)
    {
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(libFileName));
        var (_, code) = LibMatchingTestHelpers.RequireModule(library, symbolName);

        var matches = LibraryFunctionMatcher.Match(code.Data, RelocationTable.Empty, 0, library);

        Assert.Contains(matches, m => m.SymbolName == symbolName);
    }

    [Fact]
    public void Match_OffsetInsideFunction_DoesNotFindPrintf()
    {
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("SLIBCE.LIB"));
        var (_, code) = LibMatchingTestHelpers.RequireModule(library, "_printf");

        var matches = LibraryFunctionMatcher.Match(code.Data, RelocationTable.Empty, 1, library);

        Assert.DoesNotContain(matches, static m => m.SymbolName == "_printf");
    }

    [Fact]
    public void Match_PrintfModuleAgainstUnrelatedLibrary_ReturnsEmpty()
    {
        var slibce = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("SLIBCE.LIB"));
        var mathLib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("87.LIB"));
        var (_, code) = LibMatchingTestHelpers.RequireModule(slibce, "_printf");

        var matches = LibraryFunctionMatcher.Match(code.Data, RelocationTable.Empty, 0, mathLib);

        Assert.Empty(matches);
    }

    [Fact]
    public void Match_InvalidOffset_ReturnsEmpty()
    {
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("SLIBCE.LIB"));
        var (_, code) = LibMatchingTestHelpers.RequireModule(library, "_printf");

        var matches = LibraryFunctionMatcher.Match(code.Data, RelocationTable.Empty, code.Data.Length, library);

        Assert.Empty(matches);
    }
}
