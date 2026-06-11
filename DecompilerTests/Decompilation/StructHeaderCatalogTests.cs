using UltraDecompiler.Headers;

namespace DecompilerTests.Decompilation;

public class StructHeaderCatalogTests
{
    [Fact]
    public void Load_QuickCInclude_ParsesDosdateStruct()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));

        var catalog = HeaderCatalog.Load(includeDir);

        Assert.True(catalog.TryGetStruct("dosdate_t", out var definition));
        Assert.NotNull(definition);
        Assert.Equal(6, definition!.Size);
        Assert.Equal(["day", "month", "year", "dayofweek"], definition.Fields.Select(static f => f.Name).ToArray());
    }

    [Fact]
    public void Load_QuickCInclude_DosGetdateTakesStructPointer()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));

        var catalog = HeaderCatalog.Load(includeDir);

        Assert.True(catalog.TryGetSignature("_dos_getdate", out var signature));
        Assert.NotNull(signature);
        Assert.True(signature!.Parameters[0].Type.IsStructPtr);
        Assert.Equal("dosdate_t", signature.Parameters[0].Type.Pointee?.StructName);
    }
}
