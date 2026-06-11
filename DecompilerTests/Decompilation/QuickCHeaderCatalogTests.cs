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
