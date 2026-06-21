using DecompilerTests.Decompilation;
using TestSupport;
using UltraDecompiler.CodeGeneration;
using UltraDecompiler.Common;

namespace DecompilerTests.RoundTrip;

/// <summary>
/// Сквозной round-trip: ExeProvider → декомпиляция → MAKE → побайтовое сравнение EXE.
/// </summary>
[Trait("Tool", "DosBox")]
public sealed class QuickCProgramRoundTripTests
{
    private const string CrtLibrary = "SLIBCE.LIB";
    private const string DecompiledSubdirectory = "OUT";

    // Для каждого PROGRAMS/*.c: ExeProvider → Decompiler → MAKE → побайтовое сравнение с эталоном.
    // Требует DOSBox с QuickC; при отсутствии — тест падает с InvalidOperationException.
    // Прогон для неоптимизированных сборок (/Od) — основной сценарий QuickCUnoptimizedProfile.
    [Theory]
    [MemberData(nameof(QuickCProgramCases.SourceFileMemberData), MemberType = typeof(QuickCProgramCases))]
    public void RoundTrip_CompileDecompileRebuild_MatchesOriginalExe_Od(string sourceFileName)
    {
        RunRoundTrip(sourceFileName, OptimizationLevel.Disabled);
    }

    // Аналогичный round-trip, но для максимально оптимизированных сборок QuickC (/Ox).
    // Собираем оригинал с /Ox, декомпилируем с QuickCOptimizedProfile (без Od-специфичных проходов),
    // генерируем Makefile с /Ox и проверяем побайтовое совпадение после MAKE.
    // Многие программы с /Ox могут не проходить (xfail в roundtrip_xfail.txt отсеиваются общим фильтром).
    [Theory]
    [MemberData(nameof(QuickCProgramCases.SourceFileMemberData), MemberType = typeof(QuickCProgramCases))]
    public void RoundTrip_CompileDecompileRebuild_MatchesOriginalExe_Ox(string sourceFileName)
    {
        RunRoundTrip(sourceFileName, OptimizationLevel.EnabledFull);
    }

    private void RunRoundTrip(string sourceFileName, OptimizationLevel optimizationLevel)
    {
        if (!DosBoxQuickCAssets.IsDosBoxAvailable)
        {
            throw new InvalidOperationException();
        }

        var builtExePath = ExeProvider.Get(sourceFileName, libraries: [CrtLibrary], optimization: optimizationLevel);
        var targetExeFileName = FormatTargetExeFileName(Path.GetFileNameWithoutExtension(sourceFileName));
        var workspaceId = CreateWorkspaceId();
        var workspaceDirectory = Path.Combine(QuickCTestAssets.RoundTripWorkRoot, workspaceId);
        var decompiledDirectory = Path.Combine(workspaceDirectory, DecompiledSubdirectory);
        var referenceExePath = Path.Combine(workspaceDirectory, targetExeFileName);
        var rebuiltExePath = Path.Combine(decompiledDirectory, targetExeFileName);
        var dosDecompiledPath = $@"C:\QuickC\RT\{workspaceId}\{DecompiledSubdirectory}";

        Directory.CreateDirectory(workspaceDirectory);
        Directory.CreateDirectory(decompiledDirectory);

        try
        {
            File.Copy(builtExePath, referenceExePath, overwrite: true);

            var decompiler = new Decompiler(
                referenceExePath,
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                decompiledDirectory,
                libraryFileNames: [CrtLibrary]);
            var decompileResult = decompiler.Decompile();

            Assert.True(decompileResult.Success, "Декомпиляция завершилась неуспешно.");
            Assert.Contains(
                Path.Combine(decompiledDirectory, MakefileGenerator.FileName),
                decompileResult.OutputFiles);

            // Проверяем, что в возвращённых CompilerOptions уровень оптимизации сохранён (для генерации правильного Makefile).
            Assert.Equal(optimizationLevel, decompileResult.CompilerOptions.OptimizationLevel);

            var makeResult = DosBoxQuickCRunner.Run(
                $@"CD {dosDecompiledPath}",
                "MAKE");

            Assert.True(
                File.Exists(rebuiltExePath),
                $"MAKE не создал {targetExeFileName}.{Environment.NewLine}{makeResult.Output}");

            var referenceBytes = File.ReadAllBytes(referenceExePath);
            var rebuiltBytes = File.ReadAllBytes(rebuiltExePath);

            Assert.True(
                referenceBytes.AsSpan().SequenceEqual(rebuiltBytes),
                DescribeExeMismatch(sourceFileName, referenceBytes, rebuiltBytes, makeResult.Output));
        }
        finally
        {
            if (Directory.Exists(workspaceDirectory))
            {
                Directory.Delete(workspaceDirectory, recursive: true);
            }
        }
    }

    /// <summary>Имя EXE по имени исходника: <c>hello.c</c> → <c>HELLO.EXE</c> (лимит 8.3 на stem).</summary>
    private static string FormatTargetExeFileName(string baseName)
    {
        var stem = baseName.ToUpperInvariant();
        Assert.True(
            stem.Length <= 8,
            $"Имя '{stem}.EXE' нарушает ограничение DOS 8.3: stem должен быть не длиннее 8 символов.");

        return $"{stem}.EXE";
    }

    /// <summary>Короткий (≤8 символов) идентификатор рабочего каталога под <c>QuickC/RT</c>.</summary>
    private static string CreateWorkspaceId() =>
        Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static string DescribeExeMismatch(
        string sourceFileName,
        byte[] referenceBytes,
        byte[] rebuiltBytes,
        string makeOutput)
    {
        var firstDiff = FindFirstDifference(referenceBytes, rebuiltBytes);
        var diffDescription = firstDiff < 0
            ? $"размеры различаются: эталон {referenceBytes.Length} байт, пересборка {rebuiltBytes.Length} байт"
            : $"первое отличие на смещении 0x{firstDiff:X} (эталон 0x{referenceBytes[firstDiff]:X2}, пересборка 0x{rebuiltBytes[firstDiff]:X2})";

        return $"""
            EXE после декомпиляции и MAKE не совпадает с эталоном для {sourceFileName}.
            {diffDescription}
            --- вывод MAKE ---
            {makeOutput}
            """;
    }

    private static int FindFirstDifference(byte[] left, byte[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        for (var i = 0; i < length; i++)
        {
            if (left[i] != right[i])
            {
                return i;
            }
        }

        return left.Length == right.Length ? -1 : length;
    }
}
