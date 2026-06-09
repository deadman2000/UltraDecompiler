namespace LibParserTests;

using LibParser.Omf;

public sealed class OmfPubdefParserTests
{
    [Fact]
    public void Parse_SLIbCe_Nmalloc_HasDistinctOffsetsForFreeAndMalloc()
    {
        if (!QuickCLibAssets.Exists("SLIBCE.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("SLIBCE.LIB"));
        var module = lib.FindModuleBySymbol("_malloc");
        Assert.NotNull(module);
        Assert.Equal("nmalloc", module.DisplayName, ignoreCase: true);

        var free = module.FindPublicSymbol("_free");
        var malloc = module.FindPublicSymbol("_malloc");
        Assert.NotNull(free);
        Assert.NotNull(malloc);
        Assert.Equal(0, free.Offset);
        Assert.True(malloc.Offset > free.Offset);
        Assert.Equal(malloc.Offset, module.TryGetCodeOffset("_malloc"));
    }

    [Fact]
    public void Parse_CLibC_Printf_HasPubdefAtZero()
    {
        if (!QuickCLibAssets.Exists("CLIBC.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("CLIBC.LIB"));
        var module = lib.FindModuleBySymbol("_printf");
        Assert.NotNull(module);

        var printf = module.FindPublicSymbol("_printf");
        Assert.NotNull(printf);
        Assert.Equal(0, printf.Offset);
        Assert.Equal(0, module.TryGetCodeOffset("_printf"));
    }
}
