using System.Diagnostics;
using System.Text;

namespace DecompilerTests.Decompilation;

/// <summary>Запуск команд QuickC внутри DOSBox-X (рабочий каталог — корень QuickC).</summary>
internal static class DosBoxQuickCRunner
{
    /// <summary>Результат запуска DOSBox-X.</summary>
    public sealed record RunResult(int ExitCode, string Output);

    /// <summary>
    /// Выполняет команды в эмуляторе после autoexec из <c>dosbox.conf</c>
    /// (монтирование <c>C:</c>, <c>LIB</c>, <c>INCLUDE</c>, <c>PATH</c>).
    /// </summary>
    public static RunResult Run(params string[] commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var arguments = new StringBuilder()
            .Append("-conf dosbox.conf -nopromptfolder -fastlaunch -silent");

        foreach (var command in commands)
        {
            arguments.Append(" -c ").Append(Quote(command));
        }

        arguments.Append(" -c exit");

        var executable = DosBoxQuickCAssets.ResolvedDosBoxExecutable
            ?? throw new InvalidOperationException("DOSBox-X не найден (PATH или DOSBOX_X_PATH).");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments.ToString(),
            WorkingDirectory = QuickCTestAssets.QuickCRootDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException($"Не удалось запустить DOSBox-X: {executable}");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new RunResult(process.ExitCode, string.Concat(stdout, stderr));
    }

    private static string Quote(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}
