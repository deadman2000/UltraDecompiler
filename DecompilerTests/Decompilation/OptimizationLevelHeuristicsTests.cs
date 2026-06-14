using TestSupport;
using UltraDecompiler.Compilation;

namespace DecompilerTests.Decompilation;

/// <summary>Эвристики /Od vs /Ox: glob Od не должен определяться как /Ox.</summary>
public sealed class OptimizationLevelHeuristicsTests
{
    // glob.c (/Od): bump + main без register-loop и без imul → Disabled.
    [Fact]
    public void DetectFromUserProcedures_GlobOd_ReturnsDisabled()
    {
        var parser = new DosExeParser(ExeProvider.Get("glob.c"));
        var bump = X86Disassembler.Disassemble(parser.Image, parser.RelocationTable, 0x10, RegisterState.InitExe);
        var main = X86Disassembler.Disassemble(parser.Image, parser.RelocationTable, 0x25, RegisterState.InitExe);

        var level = OptimizationLevelHeuristics.DetectFromUserProcedures(
        [
            new DisassembledProcedure { Offset = 0x10, Name = "bump", Instructions = bump, IsLibrary = false },
            new DisassembledProcedure { Offset = 0x25, Name = "main", Instructions = main, IsLibrary = false },
        ]);

        Assert.Equal(OptimizationLevel.Disabled, level);
    }

    // vol.c (/Ox): register counter loop в main → EnabledFull.
    [Fact]
    public void DetectFromUserProcedures_VolOx_ReturnsEnabledFull()
    {
        var parser = new DosExeParser(ExeProvider.Get("vol.c", optimization: OptimizationLevel.EnabledFull));
        var main = X86Disassembler.Disassemble(parser.Image, parser.RelocationTable, 0x10, RegisterState.InitExe);

        var level = OptimizationLevelHeuristics.DetectFromUserProcedures(
        [
            new DisassembledProcedure { Offset = 0x10, Name = "main", Instructions = main, IsLibrary = false },
        ]);

        Assert.Equal(OptimizationLevel.EnabledFull, level);
    }

    // Полный Decompiler для glob.c (/Od) должен сохранить /Od в CompilerOptions.
    [Fact]
    public void Decompile_GlobOd_PreservesDisabledOptimizationLevel()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("glob.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            Assert.Equal(OptimizationLevel.Disabled, result.CompilerOptions.OptimizationLevel);
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
