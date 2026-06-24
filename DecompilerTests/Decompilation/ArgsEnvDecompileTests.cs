using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>args.c</c> и <c>env.c</c>: сигнатура main и обращения к argv/envp.</summary>
public sealed class ArgsEnvDecompileTests
{
    // QuickC/PROGRAMS/args.c → main(int argc, char *argv[]), argv[i], argc (циклы — задача CFG structurer).
    [Fact(Skip = "NotImplemented")]
    public void Decompile_Args_MainUsesArgcArgv()
    {
        var exePath = ExeProvider.Get("args.c");
        var result = DecompileTestHelper.DecompileExe(exePath);

        Assert.True(result.Success);
        var mainSource = DecompileTestHelper.ReadPrimarySource(result);

        Assert.Contains("int main(int argc, char *argv[])", mainSource);
        Assert.Contains("argv[", mainSource);
        Assert.Contains("total:", mainSource);
        Assert.DoesNotContain("(void)argv", mainSource);
        Assert.DoesNotContain("_psp", mainSource);
    }

    [Fact(Skip = "NotImplemented")]
    public void Decompile_Env_MainUsesEnvp()
    {
        var exePath = ExeProvider.Get("env.c");
        var result = DecompileTestHelper.DecompileExe(exePath);

        Assert.True(result.Success);
        var mainSource = DecompileTestHelper.ReadPrimarySource(result);

        Assert.Contains("int main(int argc, char *argv[], char *envp[])", mainSource);
        Assert.Contains("envp[", mainSource);
        Assert.DoesNotContain("(void)argv", mainSource);
        Assert.DoesNotContain("_psp", mainSource);
    }
}
