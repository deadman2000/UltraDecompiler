using UltraDecompiler.Decompilation;
using UltraDecompiler.Headers;

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

        Assert.True(catalog.TryGetSignature("printf", out var signature));
        Assert.NotNull(signature);
        Assert.Equal(CTypeKind.Int, signature!.ReturnType.Kind);
        Assert.True(signature.Parameters.Count >= 1);
        Assert.Equal(CTypeKind.Pointer, signature.Parameters[0].Type.Kind);
        Assert.True(signature.IsVariadic);
    }

    // DOS.H: void _disable(void); void _enable(void); — без параметров
    [Fact]
    public void Load_QuickCInclude_DisableEnableHaveNoParameters()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));

        var catalog = HeaderCatalog.Load(includeDir);

        Assert.True(catalog.TryGetSignature("_disable", out var disable));
        Assert.NotNull(disable);
        Assert.True(disable!.ReturnType.IsVoid);
        Assert.Empty(disable.Parameters);
        Assert.False(disable.IsVariadic);

        Assert.True(catalog.TryGetSignature("_enable", out var enable));
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

        Assert.True(catalog.TryGetSignature("perror", out var signature));
        Assert.NotNull(signature);
        Assert.True(signature!.ReturnType.IsVoid);
    }
}
