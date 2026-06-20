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
    [Fact(Skip = "NotImplemented")]
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
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));

        try
        {
            // Собираем (или берём из кэша) EXE с указанным уровнем оптимизации.
            // Передаём SLIBCE.LIB для консистентности с round-trip тестами и реальными примерами.
            var exePath = ExeProvider.Get(
                sourceFileName,
                libraries: ["SLIBCE.LIB"],
                optimization: buildOptimization);

            var result = new Decompiler().Decompile(
                exePath,
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory,
                libraryFileNames: ["SLIBCE.LIB"]);

            Assert.True(
                result.Success,
                $"Декомпиляция программы '{sourceFileName}' (сборка с {buildOptimization}) завершилась неуспешно. " +
                "Эвристика уровня оптимизации вызывается только при успешном разборе main/user-процедур.");

            Assert.Equal(
                expected,
                result.CompilerOptions.OptimizationLevel);
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
