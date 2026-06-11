using UltraDecompiler.Headers;

namespace DecompilerTests.Decompilation;

/// <summary>Разбор struct и сигнатур API из эталонных заголовков QuickC (<c>DOS.H</c>).</summary>
public class StructHeaderCatalogTests
{
    // Проверяем, что из DOS.H извлекается struct dosdate_t:
    //   struct dosdate_t { unsigned char day; month; unsigned int year; dayofweek; };
    // Размер 6 байт (1+1+2+1 + выравнивание не требуется для 8086).
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

    // Проверяем сигнатуру void _dos_getdate(struct dosdate_t *) из DOS.H —
    // нужна для типизации вызова при декомпиляции dos.c / dvars.c.
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
