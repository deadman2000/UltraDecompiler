using System.Text;
using UltraDecompiler.Common;

namespace UltraDecompiler.CodeGeneration;

/// <summary>Параметры генерации Makefile для сборки декомпилированного проекта QuickC.</summary>
public sealed record MakefileOptions
{
    /// <summary>Имя выходного .EXE (например <c>HELLO.EXE</c>).</summary>
    public required string TargetExeFileName { get; init; }

    /// <summary>Имена исходных .c файлов в каталоге вывода (без пути).</summary>
    public required IReadOnlyList<string> SourceFileNames { get; init; }

    /// <summary>Восстановленные флаги компилятора.</summary>
    public required CompilerOptions CompilerOptions { get; init; }

    /// <summary>Подключаемые OMF-библиотеки (имена файлов).</summary>
    public required IReadOnlyList<string> LibraryFileNames { get; init; }

    /// <summary>Абсолютный путь к каталогу вывода (для относительных путей в Makefile).</summary>
    public required string OutputDirectory { get; init; }
}

/// <summary>
/// Генерирует Makefile (NMAKE / GNU make под DOS) для пересборки декомпилированных исходников через QuickC.
/// </summary>
public static class MakefileGenerator
{
    private const string MakefileFileName = "Makefile";

    /// <summary>Имя создаваемого файла в каталоге вывода.</summary>
    public static string FileName => MakefileFileName;

    /// <summary>Формирует текст Makefile по восстановленным параметрам сборки.</summary>
    public static string FormatMakefile(MakefileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TargetExeFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);

        if (options.SourceFileNames.Count == 0)
        {
            throw new ArgumentException("Список исходных файлов не может быть пустым.", nameof(options));
        }

        var sb = new StringBuilder();

        AppendVariables(sb, options);
        AppendTargets(sb, options);

        return sb.ToString();
    }

    /// <summary>Записывает Makefile в <paramref name="outputDirectory"/> и возвращает полный путь.</summary>
    public static string WriteMakefile(MakefileOptions options, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var content = FormatMakefile(options);
        var path = Path.Combine(outputDirectory, MakefileFileName);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static void AppendVariables(StringBuilder sb, MakefileOptions options)
    {
        sb.AppendLine("CC     := QCL.EXE");
        sb.AppendLine("CFLAGS := /nologo " + options.CompilerOptions.GetQuickCCompilerFlags());
        sb.AppendLine("LIBS   := " + string.Join(' ', options.LibraryFileNames));
        sb.AppendLine("SRCS   := " + string.Join(' ', options.SourceFileNames));
        sb.AppendLine($"TARGET := {options.TargetExeFileName}");
        sb.AppendLine();
    }

    private static void AppendTargets(StringBuilder sb, MakefileOptions options)
    {
        sb.AppendLine(".PHONY: all clean");
        sb.AppendLine();
        sb.AppendLine("all: $(TARGET)");
        sb.AppendLine();
        sb.AppendLine("$(TARGET): $(SRCS)");
        sb.AppendLine("\t$(CC) $(CFLAGS) /Fe$(TARGET) $(LIBS) $(SRCS)");
        sb.AppendLine();
        sb.AppendLine("clean:");
        sb.AppendLine("\tdel *.obj");
        sb.AppendLine("\tdel $(TARGET)");
    }

}
