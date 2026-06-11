using TestSupport;
using UltraDecompiler.Compilation;
using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;
using UltraDecompiler.PostProcessing;

namespace DecompilerTests.Decompilation;

public class StackCheckDetectorTests
{
    [Fact]
    public void Decompile_HelloSmall_DetectsStackCheckAndRemovesChkstkFromC()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var decompiler = new Decompiler();
            var result = decompiler.Decompile(
                ExeProvider.Get("hello.c", stackCheck: true),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            Assert.True(result.CompilerOptions.StackCheckingEnabled);

            var mainSource = File.ReadAllText(
                result.OutputFiles.First(path => path.EndsWith("main.c", StringComparison.Ordinal)));
            Assert.DoesNotContain("_chkstk(", mainSource, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Decompile_AddSmall_DetectsStackCheckAndRemovesChkstkFromAllUserFunctions()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var decompiler = new Decompiler();
            var result = decompiler.Decompile(
                ExeProvider.Get("add.c", stackCheck: true),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            Assert.True(result.CompilerOptions.StackCheckingEnabled);

            foreach (var filePath in result.OutputFiles)
            {
                var source = File.ReadAllText(filePath);
                Assert.DoesNotContain("_chkstk(", source, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void RemoveChkstkCalls_StripsNestedCalls()
    {
        var operations = new List<Operation>
        {
            new SetOperation(new Variable(1), new CallExpr(StackCheckDetector.ChkstkCName, [])),
            new IfOperation(
                new ConstExpr(1),
                [new SetOperation(new Variable(2), new CallExpr(StackCheckDetector.ChkstkCName, []))],
                null),
        };

        var filtered = StackCheckDetector.RemoveChkstkCalls(operations);

        Assert.Empty(filtered);
    }
}
