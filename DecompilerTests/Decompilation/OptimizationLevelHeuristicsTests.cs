using TestSupport;
using UltraDecompiler.Common;
using UltraDecompiler.Decompilation.Heuristics;

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
    [Fact(Skip = "NotImplemented")]
    public void Decompile_GlobOd_PreservesDisabledOptimizationLevel()
    {
        var result = DecompileTestHelper.DecompileExample("glob.c");

        Assert.True(result.Success);
        Assert.Equal(OptimizationLevel.Disabled, result.CompilerOptions.OptimizationLevel);
    }

    // Для каждой программы из QuickC/PROGRAMS/*.c (включая xfail roundtrip) проверяем,
    // что сборка с /Od приводит к определению OptimizationLevel.Disabled через эвристики.
    // Проверка выполняется через полный путь Decompiler.Decompile → CompilerOptions,
    // как это происходит в реальном использовании (Decompiler вызывает OptimizationLevelHeuristics).
    [Theory(Skip = "NotImplemented")]
    [MemberData(nameof(QuickCProgramCases.AllSourceFileMemberData), MemberType = typeof(QuickCProgramCases))]
    [Trait("Tool", "DosBox")]
    public void Decompile_AllPrograms_Od_PreservesDisabledOptimizationLevel(string sourceFileName)
    {
        RunDecompileAndAssertOptimizationLevel(
            sourceFileName,
            buildOptimization: OptimizationLevel.Disabled,
            expected: OptimizationLevel.Disabled);
    }

    // Для каждой программы из QuickC/PROGRAMS/*.c проверяем,
    // что сборка с /Ox (/EnabledFull) приводит к определению OptimizationLevel.EnabledFull.
    // Эвристики должны распознать отсутствие хвостовых эпилогов /Od, наличие register-counter loops,
    // imul или низкий процент tail-epilog-jumps.
    [Theory(Skip = "NotImplemented")]
    [MemberData(nameof(QuickCProgramCases.AllSourceFileMemberData), MemberType = typeof(QuickCProgramCases))]
    [Trait("Tool", "DosBox")]
    public void Decompile_AllPrograms_Ox_PreservesEnabledFullOptimizationLevel(string sourceFileName)
    {
        RunDecompileAndAssertOptimizationLevel(
            sourceFileName,
            buildOptimization: OptimizationLevel.EnabledFull,
            expected: OptimizationLevel.EnabledFull);
    }

    private static void RunDecompileAndAssertOptimizationLevel(
        string sourceFileName,
        OptimizationLevel buildOptimization,
        OptimizationLevel expected)
    {
        var exePath = ExeProvider.Get(
            sourceFileName,
            optimization: buildOptimization);

        var result = DecompileTestHelper.DecompileExample(exePath);

        Assert.True(
            result.Success,
            $"Декомпиляция программы '{sourceFileName}' (сборка с {buildOptimization}) завершилась неуспешно. " +
            "Эвристика уровня оптимизации вызывается только при успешном разборе main/user-процедур.");

        Assert.Equal(
            expected,
            result.CompilerOptions.OptimizationLevel);
    }
}
