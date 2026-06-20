using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>long.c</c>: тип <c>long</c> и арифметика QuickC без имён runtime.</summary>
public sealed class LongDecompileTests
{
    // Исходник QuickC/PROGRAMS/long.c:
    //   long mix(long a, long b) { sum/diff/prod/quot/rem/shifted; return sum+...+shifted; }
    // Ожидаемый фрагмент LONG.c:
    //   long sub_0010(long arg0, long arg1) {
    //     var2 = arg0 + arg1; var4 = arg0 - arg1; var6 = arg0 * arg1;
    //     var8 = arg0 / arg1; var10 = arg0 % arg1; var12 = (arg0 << 4) + (arg1 >> 2);
    //     return var2 + var4 + var6 + var8 + var10 + var12;
    //   }
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Long_EmitsLongArithmeticWithoutRuntimeHelpers()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("long.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            var source = DecompileTestHelper.ReadPrimarySource(result);

            Assert.Contains("long sub_0010(long arg0, long arg1)", source);
            Assert.Contains("arg0 + arg1", source);
            Assert.Contains("arg0 - arg1", source);
            Assert.Contains("arg0 * arg1", source);
            Assert.Contains("arg0 / arg1", source);
            Assert.Contains("arg0 % arg1", source);
            Assert.Contains("(arg0 << 4) + (arg1 >> 2)", source);
            Assert.Contains("return var2 + var4 + var6 + var8 + var10 + var12", source);
            Assert.Contains("printf(\"%ld\\n\", sub_0010(0x1234L, 0x5678L))", source);
            Assert.DoesNotContain("_aNlshl", source);
            Assert.DoesNotContain("_aNlshr", source);
            Assert.DoesNotContain("__aNlshl", source);
            Assert.DoesNotContain("__aNlshr", source);
            Assert.DoesNotContain("__aNlmul", source);
            Assert.DoesNotContain("__aNldiv", source);
            Assert.DoesNotContain("__aNlrem", source);
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
