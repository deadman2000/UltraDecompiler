using TestSupport;
using UltraDecompiler.CodeGeneration;
using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

/// <summary>
/// Сквозной round-trip: исходник → QCL → декомпиляция → MAKE → побайтовое сравнение EXE.
/// </summary>
[Trait("Tool", "DosBox")]
public sealed class QuickCProgramRoundTripTests
{
    private const string MemoryModelSuffix = "S";
    private const string CrtLibrary = "SLIBCE.LIB";
    private const string DecompiledSubdirectory = "OUT";
    private const string CompilerFlags = "/nologo /AS /Gs";

    // Для каждого PROGRAMS/*.c: QCL → EXE → Decompiler → MAKE → побайтовое сравнение с оригиналом.
    // Требует DOSBox с QuickC; при отсутствии — тест падает с InvalidOperationException.
    [Theory]
    [MemberData(nameof(QuickCProgramCases.SourceFileMemberData), MemberType = typeof(QuickCProgramCases))]
    public void RoundTrip_CompileDecompileRebuild_MatchesOriginalExe(string sourceFileName)
    {
        if (!DosBoxQuickCAssets.IsDosBoxAvailable)
        {
            throw new InvalidOperationException();
        }

        var sourcePath = QuickCTestAssets.ProgramsPathOf(sourceFileName);
        Assert.True(File.Exists(sourcePath), $"Исходник не найден: {sourcePath}");

        var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
        var targetExeFileName = FormatTargetExeFileName(baseName);
        var workspaceId = CreateWorkspaceId();
        var workspaceDirectory = Path.Combine(QuickCTestAssets.RoundTripWorkRoot, workspaceId);
        var decompiledDirectory = Path.Combine(workspaceDirectory, DecompiledSubdirectory);
        var referenceExePath = Path.Combine(workspaceDirectory, targetExeFileName);
        var rebuiltExePath = Path.Combine(decompiledDirectory, targetExeFileName);
        var dosWorkspacePath = $@"C:\QuickC\RT\{workspaceId}";
        var dosDecompiledPath = $@"{dosWorkspacePath}\{DecompiledSubdirectory}";

        Directory.CreateDirectory(workspaceDirectory);

        try
        {
            File.Copy(sourcePath, Path.Combine(workspaceDirectory, sourceFileName), overwrite: true);

            var compileResult = DosBoxQuickCRunner.Run(
                $@"CD {dosWorkspacePath}",
                $"QCL {CompilerFlags} {sourceFileName} /Fe{targetExeFileName} {CrtLibrary}");

            Assert.True(
                File.Exists(referenceExePath),
                $"QCL не создал {targetExeFileName}.{Environment.NewLine}{compileResult.Output}");

            var decompiler = new Decompiler();
            var decompileResult = decompiler.Decompile(
                referenceExePath,
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                decompiledDirectory);

            Assert.True(decompileResult.Success, "Декомпиляция завершилась неуспешно.");
            Assert.Contains(
                Path.Combine(decompiledDirectory, MakefileGenerator.FileName),
                decompileResult.OutputFiles);

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

    /// <summary>Имя EXE для small-модели: <c>HELLO_S.EXE</c> (лимит 8.3 на stem).</summary>
    private static string FormatTargetExeFileName(string baseName)
    {
        var stem = $"{baseName.ToUpperInvariant()}_{MemoryModelSuffix}";
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
