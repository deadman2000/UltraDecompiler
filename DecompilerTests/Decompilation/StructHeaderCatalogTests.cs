
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

    // Проверяем сигнатуру int int86(int, union REGS *, union REGS *) из DOS.H.
    [Fact]
    public void Load_QuickCInclude_Int86TakesUnionRegsPointers()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));

        var catalog = HeaderCatalog.Load(includeDir);

        Assert.True(catalog.TryGetFunction("int86", out var function));
        Assert.NotNull(function);
        Assert.Equal(3, function!.Parameters.Count);
        Assert.True(function.Parameters[1].Type.IsStructPtr);
        Assert.True(function.Parameters[2].Type.IsStructPtr);
        Assert.Equal("REGS", function.Parameters[1].Type.Pointee?.StructName);
        Assert.True(function.Parameters[1].Type.Pointee?.IsUnion);
    }

    // union REGS из DOS.H: 14 байт, поля WORDREGS с префиксом x. (ax, bx, …).
    [Fact]
    public void Load_QuickCInclude_ParsesUnionRegs()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));

        var catalog = HeaderCatalog.Load(includeDir);

        Assert.True(catalog.TryGetStruct("REGS", out var definition));
        Assert.NotNull(definition);
        Assert.True(definition!.IsUnion);
        Assert.Equal(14, definition.Size);
        Assert.Contains(definition.Fields, static f => f.Name == "x.ax" && f.Offset == 0);
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

        Assert.True(catalog.TryGetFunction("_dos_getdate", out var function));
        Assert.NotNull(function);
        Assert.True(function!.Parameters[0].Type.IsStructPtr);
        Assert.Equal("dosdate_t", function.Parameters[0].Type.Pointee?.StructName);
    }
}
