using TestSupport;
using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>rot.c</c>: ручной <c>rol()</c> на <c>unsigned</c>.</summary>
public sealed class RotDecompileTests
{
    // Исходник QuickC/PROGRAMS/rot.c:
    //   unsigned rol(unsigned x, int n) { return (x << n) | (x >> (16 - n)); }
    //   printf("%u\n", rol(0x8001, 3));
    // Ожидаемый фрагмент:
    //   unsigned sub_0010(unsigned arg0, int arg1) { return (arg0 << arg1) | (arg0 >> (16 - arg1)); }
    //   printf("%u\n", sub_0010(0x8001, 3));
    [Fact]
    public void Decompile_Rot_UsesUnsignedTypes()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("rot.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            var source = DecompileTestHelper.ReadPrimarySource(result);
            Console.WriteLine(source);

            Assert.Contains("unsigned sub_0010(unsigned arg0", source);
            Assert.Contains("(arg0 << arg1) | (arg0 >> 16 - arg1)", source);
            Assert.Contains("printf(\"%u\\n\",", source);
            Assert.Contains("sub_0010(32769, 3)", source);
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
