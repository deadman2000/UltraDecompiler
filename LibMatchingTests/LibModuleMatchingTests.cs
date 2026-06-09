using Common;
using LibParser.Omf;
using TestSupport;
using UltraDecompiler.Disassembler;
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

    [Fact]
    public void Match_WithSymbolFilter_ChecksOnlyRequestedSymbol()
    {
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("SLIBCE.LIB"));
        var crt0 = Crt0TestHelpers.GetCrt0Module(library);
        var code = crt0.CodeSegments.First(static s => s.IsCode);

        var matches = LibraryFunctionMatcher.Match(
            code.Data,
            RelocationTable.Empty,
            0,
            library,
            RegisterState.Unknown,
            symbolName: "__astart",
            moduleName: null);

        Assert.Single(matches);
        Assert.Equal("__astart", matches[0].SymbolName);
    }

    [Fact]
    public void Match_WithModuleFilter_ChecksOnlyRequestedModule()
    {
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("SLIBCE.LIB"));
        var crt0 = Crt0TestHelpers.GetCrt0Module(library);
        var code = crt0.CodeSegments.First(static s => s.IsCode);

        var matches = LibraryFunctionMatcher.Match(
            code.Data,
            RelocationTable.Empty,
            0,
            library,
            RegisterState.Unknown,
            symbolName: null,
            moduleName: "crt0");

        Assert.Contains(matches, static m => m.SymbolName == "__astart");
        Assert.All(matches, static m => Assert.Equal("crt0", m.ModuleName, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Match_MultiSymbolModule_UsesPubdefOffsetForMalloc()
    {
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("SLIBCE.LIB"));
        var (module, code) = LibMatchingTestHelpers.RequireModule(library, "_malloc");
        var mallocOffset = module.TryGetCodeOffset("_malloc");
        Assert.NotNull(mallocOffset);
        Assert.True(mallocOffset > 0);

        var mallocMatches = LibraryFunctionMatcher.Match(
            code.Data,
            RelocationTable.Empty,
            mallocOffset.Value,
            library,
            RegisterState.Unknown,
            symbolName: "_malloc",
            moduleName: null);

        Assert.Single(mallocMatches);
        Assert.Equal("_malloc", mallocMatches[0].SymbolName);
        Assert.Equal(mallocOffset.Value, mallocMatches[0].ModuleCodeOffset);

        var freeMatches = LibraryFunctionMatcher.Match(
            code.Data,
            RelocationTable.Empty,
            0,
            library,
            RegisterState.Unknown,
            symbolName: "_free",
            moduleName: null);

        Assert.Single(freeMatches);
        Assert.Equal(0, freeMatches[0].ModuleCodeOffset);

        var wrongOffset = LibraryFunctionMatcher.Match(
            code.Data,
            RelocationTable.Empty,
            mallocOffset.Value,
            library,
            RegisterState.Unknown,
            symbolName: "_free",
            moduleName: null);

        Assert.Empty(wrongOffset);
    }

    [Fact]
    public void Match_WithSymbolAndModuleFilter_ReturnsEmptyForWrongSymbol()
    {
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("SLIBCE.LIB"));
        var crt0 = Crt0TestHelpers.GetCrt0Module(library);
        var code = crt0.CodeSegments.First(static s => s.IsCode);

        var matches = LibraryFunctionMatcher.Match(
            code.Data,
            RelocationTable.Empty,
            0,
            library,
            RegisterState.Unknown,
            symbolName: "_printf",
            moduleName: "crt0");

        Assert.Empty(matches);
    }
}
