using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>recur.c</c>: рекурсивный <c>fact()</c>.</summary>
public sealed class RecurDecompileTests
{
    // Исходник QuickC/PROGRAMS/recur.c:
    //   int fact(int n) { if (n <= 1) return 1; return n * fact(n - 1); }
    // Ожидаемый фрагмент sub_0010.c (fact):
    //   if (arg0 <= 1) return 1;
    //   return arg0 * sub_0010(arg0 - 1);
    [Fact]
    public void Decompile_Recur_FactFunctionIsValidC()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("recur.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            var source = DecompileTestHelper.ReadPrimarySource(result);

            Assert.DoesNotContain("div op", source);
            Assert.Contains("if (arg0 <= 1)", source);
            Assert.Contains("return 1", source);
            Assert.Contains("sub_0010(arg0 - 1)", source);
            Assert.Contains("arg0 * sub_0010(arg0 - 1)", source);
            var factProc = result.Procedures.All.First(p => p.Name == "sub_0010");
            Assert.False(factProc.Signature.ReturnType.IsVoid);
            Assert.Single(factProc.Signature.Parameters);

            Assert.Contains("sub_0010(5)", source);
            Assert.Contains("printf(\"%d\\n\",", source);
            Assert.DoesNotContain("temp1", source);
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
