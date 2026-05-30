using System.Diagnostics;
using System.Text;

namespace LibParserTests.ImHex;

public sealed partial class ImHexTests
{
    /// <summary>Запускает <c>imhex --pl format</c> и разбирает stderr на ошибки Pattern Language.</summary>
    private static class ImHexPatternRunner
    {
        public sealed record RunResult(bool Success, string Details, string? Json);

        public static RunResult Run(string imhexExe, string inputPath, string patternPath)
        {
            var outputPath = Path.Combine(
                Path.GetTempPath(),
                $"imhex_omf_lib_{Guid.NewGuid():N}.json");

            try
            {
                var arguments = new StringBuilder()
                    .Append("--pl format ")
                    .Append("-i ").Append(Quote(inputPath)).Append(' ')
                    .Append("-p ").Append(Quote(patternPath)).Append(' ')
                    .Append("-o ").Append(Quote(outputPath))
                    .ToString();

                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = imhexExe,
                    Arguments = arguments,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }) ?? throw new InvalidOperationException($"Не удалось запустить ImHex: {imhexExe}");

                var stderr = process.StandardError.ReadToEnd();
                var stdout = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // ImHex пишет ошибки Pattern Language в stdout, не в stderr.
                var combinedOutput = string.Concat(stderr, stdout);
                var patternErrors = CollectPatternErrors(combinedOutput);
                if (patternErrors.Count > 0)
                {
                    return new RunResult(false, string.Join(Environment.NewLine, patternErrors), null);
                }

                if (process.ExitCode != 0)
                {
                    return new RunResult(
                        false,
                        $"Код выхода {process.ExitCode}.{Environment.NewLine}{combinedOutput}",
                        null);
                }

                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                {
                    return new RunResult(false, "ImHex не записал выходной JSON.", null);
                }

                return new RunResult(true, string.Empty, File.ReadAllText(outputPath));
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        private static List<string> CollectPatternErrors(string output)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(output))
            {
                return errors;
            }

            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!IsPatternErrorLine(line))
                {
                    continue;
                }

                errors.Add(line.TrimEnd());
                for (var j = i + 1; j < lines.Length; j++)
                {
                    var followUp = lines[j];
                    if (IsPatternErrorLine(followUp))
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(followUp))
                    {
                        break;
                    }

                    errors.Add(followUp.TrimEnd());
                }
            }

            return errors;
        }

        private static bool IsPatternErrorLine(string line) =>
            line.StartsWith("Pattern Error", StringComparison.OrdinalIgnoreCase)
            || line.Contains("error[E", StringComparison.Ordinal)
            || line.StartsWith("E: error", StringComparison.OrdinalIgnoreCase);

        private static string Quote(string path) =>
            OperatingSystem.IsWindows() ? $"\"{path}\"" : $"\"{path.Replace("\"", "\\\"")}\"";
    }
}
