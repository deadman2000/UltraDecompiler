namespace DecompilerTests.Decompilation;

/// <summary>Разбор сигнатур стандартных функций из эталонных заголовков QuickC.</summary>
public class QuickCHeaderCatalogTests
{
    // STDIO.H: int printf(const char *, ...); — variadic, первый аргумент char*
    [Fact]
    public void Load_QuickCInclude_ParsesPrintfSignature()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));

        var catalog = HeaderCatalog.Load(includeDir);

        Assert.True(catalog.TryGetFunction("printf", out var function));
        Assert.NotNull(function);
        Assert.Equal(CTypeKind.Int, function!.ReturnType.Kind);
        Assert.True(function.Parameters.Count >= 1);
        Assert.Equal(CTypeKind.Pointer, function.Parameters[0].Type.Kind);
        Assert.True(function.IsVariadic);
    }

    // DOS.H: void _disable(void); void _enable(void); — без параметров
    [Fact]
    public void Load_QuickCInclude_DisableEnableHaveNoParameters()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));

        var catalog = HeaderCatalog.Load(includeDir);

        Assert.True(catalog.TryGetFunction("_disable", out var disable));
        Assert.NotNull(disable);
        Assert.True(disable!.ReturnType.IsVoid);
        Assert.Empty(disable.Parameters);
        Assert.False(disable.IsVariadic);

        Assert.True(catalog.TryGetFunction("_enable", out var enable));
        Assert.NotNull(enable);
        Assert.True(enable!.ReturnType.IsVoid);
        Assert.Empty(enable.Parameters);
        Assert.False(enable.IsVariadic);
    }

    // void perror(const char *); — возвращаемый тип void
    [Fact]
    public void Load_QuickCInclude_PerrorIsVoid()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));

        var catalog = HeaderCatalog.Load(includeDir);

        Assert.True(catalog.TryGetFunction("perror", out var function));
        Assert.NotNull(function);
        Assert.True(function!.ReturnType.IsVoid);
    }
}
