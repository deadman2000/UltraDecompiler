using TestSupport;
using UltraDecompiler.Compilation;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>compound.c</c>: различение a=a±K и a±=K, a=a±b и a±=b.</summary>
public sealed class CompoundDecompileTests
{
    // QuickC/PROGRAMS/compound.c — все варианты составного присваивания.
    [Theory(Skip = "NotImplemented")]
    [InlineData(OptimizationLevel.Disabled)]
    [InlineData(OptimizationLevel.EnabledFull)]
    public void Decompile_Compound_EmitsAssemblyFaithfulStatements(OptimizationLevel optimization)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("compound.c", optimization: optimization),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            var mainSource = File.ReadAllText(
                result.OutputFiles.First(p => p.EndsWith("main.c", StringComparison.Ordinal)));

            Assert.Contains("var1 = var1 + 5", mainSource);
            Assert.Contains("var1 += 5", mainSource);
            Assert.Contains("var1 = var1 - 5", mainSource);
            Assert.Contains("var1 -= 5", mainSource);
            Assert.Contains("var1 = var1 + var2", mainSource);
            Assert.Contains("var1 += var2", mainSource);
            Assert.Contains("var1 = var1 - var2", mainSource);
            Assert.Contains("var1 -= var2", mainSource);
            Assert.DoesNotContain("65535", mainSource);
            Assert.DoesNotContain("65531", mainSource);
            Assert.DoesNotContain("temp1", mainSource);
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