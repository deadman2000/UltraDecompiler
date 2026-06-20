using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>ret.c</c>: void-функции с явным и неявным выходом.</summary>
public sealed class RetDecompileTests
{
    // Исходник QuickC/PROGRAMS/ret.c:
    //   void foo(int flag) { if (flag) { } }
    //   void foo_ret(int flag) { if (flag) return; return; }
    // Ожидаемый фрагмент RET.c:
    //   void sub_0010(int arg0) { if (arg0) { } }          — без return
    //   void sub_0028(int arg0) { if (arg0) return; return; }
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Ret_VoidFunctionsDifferByExplicitReturn()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("ret.c", libraries: ["SLIBCE.LIB"]),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            var source = DecompileTestHelper.ReadPrimarySource(result);

            Assert.Contains("void sub_0010(int arg0)", source);
            Assert.Contains("void sub_0028(int arg0)", source);
            Assert.DoesNotMatch(@"void\s+sub_0010\s*\([^)]*\)\s*\{[^}]*return", source);

            var fooRetStart = source.IndexOf("void sub_0028", StringComparison.Ordinal);
            Assert.True(fooRetStart >= 0);
            var fooRetBody = source[fooRetStart..];
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(fooRetBody, @"\breturn\s*;"));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
