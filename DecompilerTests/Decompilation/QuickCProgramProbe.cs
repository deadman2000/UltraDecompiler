using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Вспомогательный прогон round-trip для ручной диагностики PROGRAMS/*.c (не xUnit).</summary>
internal static class QuickCProgramProbe
{
    private const string CompilerFlags = "/nologo /AS /Gs";
    private const string CrtLibrary = "SLIBCE.LIB";
    private const string DecompiledSubdirectory = "OUT";

    public static IReadOnlyList<ProbeResult> RunAll()
    {
        var results = new List<ProbeResult>();

        foreach (var path in Directory.EnumerateFiles(QuickCTestAssets.ProgramsDirectory, "*.c"))
        {
            results.Add(RunOne(Path.GetFileName(path)));
        }

        return results;
    }

    public static ProbeResult RunOne(string sourceFileName)
    {
        var result = new ProbeResult { SourceFileName = sourceFileName };

        if (!DosBoxQuickCAssets.IsDosBoxAvailable)
        {
            result.Stage = ProbeStage.Skipped;
            result.Detail = "DOSBox-X недоступен.";
            return result;
        }

        var sourcePath = QuickCTestAssets.ProgramsPathOf(sourceFileName);
        if (!File.Exists(sourcePath))
        {
            result.Stage = ProbeStage.Skipped;
            result.Detail = "Исходник не найден.";
            return result;
        }

        var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
        var targetExeFileName = FormatTargetExeFileName(baseName);
        var workspaceId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
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

            if (!File.Exists(referenceExePath))
            {
                result.Stage = ProbeStage.QclCompile;
                result.Detail = compileResult.Output;
                return result;
            }

            try
            {
                var decompiler = new Decompiler();
                var decompileResult = decompiler.Decompile(
                    referenceExePath,
                    QuickCTestAssets.LibDirectory,
                    QuickCTestAssets.IncludeDirectory,
                    decompiledDirectory);

                if (!decompileResult.Success)
                {
                    result.Stage = ProbeStage.Decompile;
                    result.Detail = "DecompileResult.Success == false (TryResolveMain / crt0).";
                    return result;
                }

                var makeResult = DosBoxQuickCRunner.Run(
                    $@"CD {dosDecompiledPath}",
                    "MAKE");

                if (!File.Exists(rebuiltExePath))
                {
                    result.Stage = ProbeStage.Recompile;
                    result.Detail = TrimMakeOutput(makeResult.Output);
                    return result;
                }

                var referenceBytes = File.ReadAllBytes(referenceExePath);
                var rebuiltBytes = File.ReadAllBytes(rebuiltExePath);

                if (!referenceBytes.AsSpan().SequenceEqual(rebuiltBytes))
                {
                    result.Stage = ProbeStage.ExeMatch;
                    result.Detail = DescribeExeMismatch(referenceBytes, rebuiltBytes, TrimMakeOutput(makeResult.Output));
                    return result;
                }

                result.Stage = ProbeStage.Pass;
                return result;
            }
            catch (Exception ex)
            {
                result.Stage = ProbeStage.Decompile;
                result.Detail = ex.ToString();
                return result;
            }
        }
        finally
        {
            if (Directory.Exists(workspaceDirectory))
            {
                Directory.Delete(workspaceDirectory, recursive: true);
            }
        }
    }

    private static string FormatTargetExeFileName(string baseName)
    {
        return $"{baseName.ToUpperInvariant()}.EXE";
    }

    private static string DescribeExeMismatch(byte[] referenceBytes, byte[] rebuiltBytes, string makeOutput)
    {
        var firstDiff = FindFirstDifference(referenceBytes, rebuiltBytes);
        var diffDescription = firstDiff < 0
            ? $"размеры: эталон {referenceBytes.Length}, пересборка {rebuiltBytes.Length}"
            : $"первое отличие 0x{firstDiff:X} (эталон 0x{referenceBytes[firstDiff]:X2}, пересборка 0x{rebuiltBytes[firstDiff]:X2})";

        return $"{diffDescription}{Environment.NewLine}--- MAKE ---{Environment.NewLine}{makeOutput}";
    }

    private static string TrimMakeOutput(string makeOutput)
    {
        const string marker = "QCL.EXE";
        var index = makeOutput.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return makeOutput;
        }

        var tail = makeOutput[index..];
        var exitIndex = tail.LastIndexOf(">exit", StringComparison.OrdinalIgnoreCase);
        return exitIndex >= 0 ? tail[..exitIndex].Trim() : tail.Trim();
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

    public enum ProbeStage
    {
        Skipped,
        QclCompile,
        Decompile,
        Recompile,
        ExeMatch,
        Pass,
    }

    public sealed class ProbeResult
    {
        public required string SourceFileName { get; init; }

        public ProbeStage Stage { get; set; }

        public string Detail { get; set; } = string.Empty;
    }
}
