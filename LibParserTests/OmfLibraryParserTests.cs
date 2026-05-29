namespace LibParserTests;

using LibParser.Omf;

public sealed class OmfLibraryParserTests
{
    [Fact]
    public void Parse_87Lib_HeaderAndModules()
    {
        if (!QuickCLibAssets.Exists("87.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("87.LIB"));

        Assert.Equal(16, lib.Header.PageSize);
        Assert.Equal(3584, lib.Header.DictionaryOffset);
        Assert.Equal(1, lib.Header.DictionaryBlockCount);
        Assert.Equal(2, lib.Modules.Count);
        Assert.Contains(lib.Modules, static m => m.DisplayName == "873");
        Assert.Contains(lib.Modules, static m => m.HeaderName.Contains("EMULATOR", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_87Lib_DictionarySymbols()
    {
        if (!QuickCLibAssets.Exists("87.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("87.LIB"));

        Assert.True(lib.Symbols.ContainsKey("__fpmath"));
        Assert.Equal(1, lib.Symbols["__fpmath"].ModulePage);

        var module = lib.FindModuleBySymbol("__fpmath");
        Assert.NotNull(module);
        Assert.Contains(module.CodeSegments, static s => s.Data.Length > 0);
    }

    [Fact]
    public void FindModuleBySymbol_Printf_CLibC()
    {
        if (!QuickCLibAssets.Exists("CLIBC.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("CLIBC.LIB"));

        Assert.True(lib.Symbols.TryGetValue("_printf", out var symbol));

        var module = lib.FindModuleBySymbol("_printf");
        Assert.NotNull(module);
        Assert.Equal(symbol.ModulePage, module.PageNumber);
        Assert.Equal(symbol.ModulePage * lib.Header.PageSize, module.FileOffset);
        Assert.True(
            module.DisplayName.Contains("printf", StringComparison.OrdinalIgnoreCase)
            || module.HeaderName.Contains("printf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(module.CodeSegments, static s => s.IsCode && s.Data.Length > 0);
    }

    [Fact]
    public void FindModuleBySymbol_FpInstall87_UsesDictionaryPage()
    {
        if (!QuickCLibAssets.Exists("87.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("87.LIB"));

        Assert.True(lib.Symbols.TryGetValue("__FPINSTALL87", out var symbol));
        Assert.Equal(173, symbol.ModulePage);

        var module = lib.FindModuleBySymbol("__FPINSTALL87");
        Assert.NotNull(module);
        Assert.Equal(173, module.PageNumber);
        Assert.Equal(173 * lib.Header.PageSize, module.FileOffset);
    }

    [Fact]
    public void Parse_CLibC_ManyModulesAndCrt0()
    {
        if (!QuickCLibAssets.Exists("CLIBC.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("CLIBC.LIB"));

        Assert.Equal(16, lib.Header.PageSize);
        Assert.True(lib.Modules.Count > 200);
        Assert.True(lib.Symbols.Count > 400);

        var crtModule = lib.Modules.FirstOrDefault(static m =>
            m.DisplayName.Equals("crt0", StringComparison.OrdinalIgnoreCase)
            || m.HeaderName.Contains("CRT0", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(crtModule);
        Assert.Contains(crtModule.Segments, static s => s.IsCode && s.Data.Length > 0);
    }

    [Theory]
    [InlineData("87.LIB")]
    [InlineData("CLIBC.LIB")]
    [InlineData("CLIBCE.LIB")]
    [InlineData("CLIBFP.LIB")]
    [InlineData("EM.LIB")]
    [InlineData("GRAPHICS.LIB")]
    [InlineData("LIBH.LIB")]
    [InlineData("LLIBC.LIB")]
    [InlineData("LLIBFP.LIB")]
    [InlineData("MLIBC.LIB")]
    [InlineData("MLIBFP.LIB")]
    [InlineData("SLIBC.LIB")]
    [InlineData("SLIBCE.LIB")]
    [InlineData("SLIBFP.LIB")]
    public void Parse_AllQuickCLibraries_Succeed(string libName)
    {
        if (!QuickCLibAssets.Exists(libName))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf(libName));
        Assert.NotEmpty(lib.Modules);
        Assert.NotEmpty(lib.Symbols);
        Assert.All(lib.Modules, static m => Assert.NotEmpty(m.HeaderName));
    }

    [Fact]
    public void Parse_InvalidData_Throws()
    {
        Assert.Throws<InvalidDataException>(() => OmfLibraryParser.Parse([0x4D, 0x5A, 0x00]));
    }
}
