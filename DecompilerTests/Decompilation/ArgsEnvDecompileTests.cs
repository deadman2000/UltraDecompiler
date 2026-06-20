using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>args.c</c> и <c>env.c</c>: сигнатура main и обращения к argv/envp.</summary>
public sealed class ArgsEnvDecompileTests
{
    // QuickC/PROGRAMS/args.c → main(int argc, char *argv[]), argv[i], argc (циклы — задача CFG structurer).
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Args_MainUsesArgcArgv()
    {
        var exePath = ExeProvider.Get("args.c", libraries: ["SLIBCE.LIB"]);
        var outputDir = Path.Combine(Path.GetTempPath(), "udc_args_" + Guid.NewGuid().ToString("N"));

        try
        {
            var result = new Decompiler().Decompile(
                exePath,
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDir);

            Assert.True(result.Success);
            var mainSource = File.ReadAllText(
                result.OutputFiles.First(static path => path.EndsWith(".c", StringComparison.OrdinalIgnoreCase)));

            Assert.Contains("int main(int argc, char *argv[])", mainSource);
            Assert.Contains("argv[", mainSource);
            Assert.Contains("total:", mainSource);
            Assert.DoesNotContain("(void)argv", mainSource);
            Assert.DoesNotContain("_psp", mainSource);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    // QuickC/PROGRAMS/env.c → main(int argc, char *argv[], char *envp[]), обращения к envp
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Env_MainUsesEnvp()
    {
        var exePath = ExeProvider.Get("env.c", libraries: ["SLIBCE.LIB"]);
        var outputDir = Path.Combine(Path.GetTempPath(), "udc_env_" + Guid.NewGuid().ToString("N"));

        try
        {
            var result = new Decompiler().Decompile(
                exePath,
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDir);

            Assert.True(result.Success);
            var mainSource = File.ReadAllText(
                result.OutputFiles.First(static path => path.EndsWith(".c", StringComparison.OrdinalIgnoreCase)));

            Assert.Contains("int main(int argc, char *argv[], char *envp[])", mainSource);
            Assert.Contains("envp[", mainSource);
            Assert.DoesNotContain("(void)argv", mainSource);
            Assert.DoesNotContain("_psp", mainSource);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }
}