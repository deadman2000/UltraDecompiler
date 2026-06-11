using TestSupport;
using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

/// <summary>Интеграционные тесты декомпиляции <c>copy.c</c> (указатели, while, строковый литерал).</summary>
public sealed class CopyDecompileTests
{
    [Fact]
    public void Decompile_Copy_EmitsCharArrayLocal()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("copy.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);

            var mainSource = File.ReadAllText(result.OutputFiles.First(p => p.EndsWith("main.c", StringComparison.Ordinal)));
            Assert.Contains("char var8[20];", mainSource);
            Assert.Contains("int var9;", mainSource);
            Assert.Contains("char var10[30];", mainSource);
            Assert.Contains("int var11;", mainSource);
            Assert.DoesNotContain("char var9[", mainSource);
            Assert.DoesNotContain("char var11[", mainSource);
            Assert.Contains("var9 = 10;", mainSource);
            Assert.Contains("var11 = 8;", mainSource);
            Assert.Contains("sub_0010(var8, \"test\")", mainSource);
            Assert.Contains("sub_0010(var10, \"test3\")", mainSource);
            Assert.Contains("printf(\"%s\\n\", var8)", mainSource);
            Assert.Contains("printf(\"%d, %d\\n\", var9, var11)", mainSource);
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
    public void Decompile_Copy2_EmitsPointerLoopWithoutTempLocals()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("copy2.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);

            var copySource = File.ReadAllText(result.OutputFiles.First(p => p.EndsWith("sub_0010.c", StringComparison.Ordinal)));
            Assert.Contains("while (*arg1 != 0)", copySource);
            Assert.Contains("*arg0 = *arg1", copySource);
            Assert.Contains("arg0 = arg0 + 1", copySource);
            Assert.Contains("arg1 = arg1 + 1", copySource);
            Assert.Contains("*arg0 = 0", copySource);
            Assert.DoesNotContain("var10", copySource);
            Assert.DoesNotContain("var11", copySource);
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
    public void Decompile_CopySmall_EmitsPointerLoopAndStringLiteral()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("copy.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);

            var copyProc = result.Procedures.All.FirstOrDefault(p => p.Name.StartsWith("sub_", StringComparison.Ordinal) && !p.IsLibrary);
            Assert.NotNull(copyProc);
            Assert.Equal(CType.Void, copyProc!.Signature.ReturnType);
            Assert.Equal(2, copyProc.Signature.Parameters.Count);
            Assert.True(copyProc.Signature.Parameters[0].Type.IsCharPtr);
            Assert.True(copyProc.Signature.Parameters[1].Type.IsCharPtr);

            var copySource = File.ReadAllText(result.OutputFiles.First(p => p.EndsWith($"{copyProc.Name}.c", StringComparison.Ordinal)));
            Assert.Contains("while (*arg1 != 0)", copySource);
            Assert.Contains("*arg0 = *arg1", copySource);
            Assert.Contains("arg0 = arg0 + 1", copySource);
            Assert.Contains("arg1 = arg1 + 1", copySource);
            Assert.Contains("*arg0 = 0", copySource);
            Assert.DoesNotContain("_psp:[", copySource);
            Assert.DoesNotContain("varSS:[", copySource);

            var mainSource = File.ReadAllText(result.OutputFiles.First(p => p.EndsWith("main.c", StringComparison.Ordinal)));
            Assert.Contains("char var8[20];", mainSource);
            Assert.DoesNotContain("char* var8", mainSource);
            Assert.Contains("sub_0010(var8, \"test\")", mainSource);
            Assert.Contains("printf(\"%s\\n\", var8)", mainSource);
            Assert.DoesNotContain("printf(\"%s\\n\", 655", mainSource);
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
