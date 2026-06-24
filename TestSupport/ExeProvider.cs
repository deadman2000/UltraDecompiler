using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using UltraDecompiler.Common;

namespace TestSupport;

/// <summary>
/// Предоставляет пути к EXE-примерам QuickC: возвращает кэшированный файл из <c>QuickC/BUILT</c>
/// или собирает его через DOSBox-X и QCL. Файловые примеры кэшируются с инвалидацией по SHA-256
/// исходника; inline-исходники — по SHA-256 строки и параметрам сборки.
/// </summary>
public static class ExeProvider
{
    private static readonly ConcurrentDictionary<string, Lock> CacheLocks = new(StringComparer.OrdinalIgnoreCase);

    private const string TempExeFileName = "OUT.EXE";

    /// <summary>
    /// Собирает EXE из исходного кода в строке; кэш — по SHA-256 текста и параметрам QCL.
    /// </summary>
    /// <param name="source">Исходный код C для компиляции.</param>
    /// <param name="memoryModel">Модель памяти.</param>
    /// <param name="stackCheck">Включить проверку стека (<c>/Gs</c> при <see langword="false"/>).</param>
    /// <param name="optimization">Уровень оптимизации.</param>
    /// <param name="libraries">OMF-библиотеки для линковки.</param>
    public static string CompileFromSource(
        string source,
        MemoryModel memoryModel = MemoryModel.Small,
        bool stackCheck = false,
        OptimizationLevel optimization = OptimizationLevel.Disabled,
        params string[]? libraries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var normalizedLibraries = NormalizeLibraries(libraries) ?? [];
        var sourceHash = ComputeStringChecksum(source);
        var cachePath = GetInlineCachePath(sourceHash, memoryModel, stackCheck, optimization, normalizedLibraries);

        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        var cacheLock = GetCacheLock(cachePath);
        lock (cacheLock)
        {
            if (File.Exists(cachePath))
            {
                return cachePath;
            }

            EnsureCompiledFromContent(
                source,
                cachePath,
                memoryModel,
                stackCheck,
                optimization,
                normalizedLibraries);
            return cachePath;
        }
    }

    /// <summary>
    /// Возвращает путь к exe-файлу примера, собранного с заданными параметрами.
    /// </summary>
    /// <param name="fileName">Имя исходника примера (например <c>hello.c</c>).</param>
    /// <param name="libraries">
    /// OMF-библиотеки для линковки (<c>SLIBCE.LIB</c>, <c>LIBH.LIB</c> и т.д.).
    /// <see langword="null"/> или пустой список — без явной линковки.
    /// </param>
    public static string Get(
        string fileName,
        MemoryModel memoryModel = MemoryModel.Small,
        bool stackCheck = false,
        OptimizationLevel optimization = OptimizationLevel.Disabled,
        params string[]? libraries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var sourceFileName = NormalizeSourceFileName(fileName);
        var normalizedLibraries = NormalizeLibraries(libraries) ?? [];
        var sourcePath = QuickCTestAssets.ProgramsPathOf(sourceFileName);
        var cachePath = GetCachePath(sourceFileName, memoryModel, stackCheck, optimization, normalizedLibraries);

        if (IsCacheValid(cachePath, sourcePath))
        {
            return cachePath;
        }

        var cacheLock = GetCacheLock(cachePath);
        lock (cacheLock)
        {
            if (IsCacheValid(cachePath, sourcePath))
            {
                return cachePath;
            }

            EnsureCompiled(
                sourceFileName,
                cachePath,
                memoryModel,
                stackCheck,
                optimization,
                normalizedLibraries);
            return cachePath;
        }
    }

    private static Lock GetCacheLock(string cachePath) =>
        CacheLocks.GetOrAdd(cachePath, static _ => new Lock());

    private static string GetChecksumPath(string cachePath) => cachePath + ".srcsha";

    private static bool IsCacheValid(string cachePath, string sourcePath)
    {
        if (!File.Exists(cachePath) || !File.Exists(sourcePath))
        {
            return false;
        }

        var checksumPath = GetChecksumPath(cachePath);
        if (!File.Exists(checksumPath))
        {
            return false;
        }

        var cachedChecksum = File.ReadAllText(checksumPath).Trim();
        var currentChecksum = ComputeSourceChecksum(sourcePath);
        return string.Equals(cachedChecksum, currentChecksum, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteSourceChecksum(string cachePath, string sourcePath) =>
        File.WriteAllText(GetChecksumPath(cachePath), ComputeSourceChecksum(sourcePath));

    private static string ComputeSourceChecksum(string sourcePath)
    {
        using var stream = File.OpenRead(sourcePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string ComputeStringChecksum(string source) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));

    /// <summary>
    /// Строит относительный путь кэша: <c>hello/s_gs_o.exe</c>, <c>long/s_gs_o_slibce_libh.exe</c>.
    /// </summary>
    private static string FormatExeFileName(
        string fileName,
        MemoryModel memoryModel,
        bool stackCheck,
        OptimizationLevel optimization,
        string[] libraries)
    {
        var sourceFileName = NormalizeSourceFileName(fileName);

        var baseName = Path.GetFileNameWithoutExtension(sourceFileName).ToLowerInvariant();
        var exeName = FormatBuildArtifactFileName(memoryModel, stackCheck, optimization, libraries);
        return Path.Combine(baseName, exeName);
    }

    private static string FormatBuildArtifactFileName(
        MemoryModel memoryModel,
        bool stackCheck,
        OptimizationLevel optimization,
        string[] libraries)
    {
        var memoryTag = GetMemoryModelTag(memoryModel);
        var stackTag = stackCheck ? "chk" : "gs";
        var optTag = optimization switch
        {
            OptimizationLevel.Disabled => "od",
            OptimizationLevel.Enabled => "o",
            OptimizationLevel.EnableLoop => "ol",
            OptimizationLevel.EnabledFull => "ox",
            _ => throw new ArgumentOutOfRangeException(nameof(optimization), optimization, null),
        };
        var librariesTag = FormatLibrariesTag(libraries);

        return $"{memoryTag}_{stackTag}_{optTag}{librariesTag}.exe";
    }

    private static string GetCachePath(
        string sourceFileName,
        MemoryModel memoryModel,
        bool stackCheck,
        OptimizationLevel optimization,
        string[] libraries)
    {
        var relativePath = FormatExeFileName(sourceFileName, memoryModel, stackCheck, optimization, libraries);
        return Path.Combine(QuickCTestAssets.BuiltExesDirectory, relativePath);
    }

    /// <summary>Кэш inline-исходников: <c>SRC/&lt;sha256&gt;/s_gs_od.exe</c>.</summary>
    private static string GetInlineCachePath(
        string sourceHash,
        MemoryModel memoryModel,
        bool stackCheck,
        OptimizationLevel optimization,
        string[] libraries)
    {
        var exeName = FormatBuildArtifactFileName(memoryModel, stackCheck, optimization, libraries);
        return Path.Combine(QuickCTestAssets.BuiltExesDirectory, "SRC", sourceHash, exeName);
    }

    private static void EnsureCompiled(
        string sourceFileName,
        string cachePath,
        MemoryModel memoryModel,
        bool stackCheck,
        OptimizationLevel optimization,
        string[] libraries)
    {
        var sourcePath = QuickCTestAssets.ProgramsPathOf(sourceFileName);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Исходник примера не найден: {sourcePath}", sourcePath);
        }

        EnsureCompiledFromContent(
            File.ReadAllText(sourcePath),
            cachePath,
            memoryModel,
            stackCheck,
            optimization,
            libraries,
            checksumSourcePath: sourcePath);
    }

    private static void EnsureCompiledFromContent(
        string sourceContent,
        string cachePath,
        MemoryModel memoryModel,
        bool stackCheck,
        OptimizationLevel optimization,
        string[] libraries,
        string? checksumSourcePath = null)
    {
        if (!DosBoxQuickCAssets.IsDosBoxAvailable)
        {
            throw new InvalidOperationException(
                "DOSBox-X недоступен — невозможно собрать EXE-пример.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

        var workspaceId = CreateWorkspaceId();
        var workspaceDirectory = Path.Combine(QuickCTestAssets.BuiltExesDirectory, "TMP", workspaceId);
        Directory.CreateDirectory(workspaceDirectory);

        var isolatedSourceFileName = $"{workspaceId}.C";
        var isolatedSourcePath = Path.Combine(workspaceDirectory, isolatedSourceFileName);
        File.WriteAllText(isolatedSourcePath, sourceContent);

        var tempHostPath = Path.Combine(workspaceDirectory, TempExeFileName);
        var dosWorkspacePath = $@"C:\QuickC\BUILT\TMP\{workspaceId}";
        var compilerFlags = BuildCompilerFlags(memoryModel, stackCheck, optimization);

        try
        {
            var librariesArg = FormatLibrariesCommandLine(libraries);
            var compileResult = DosBoxQuickCRunner.Run(
                $@"CD {dosWorkspacePath}",
                $@"QCL /nologo {compilerFlags} {isolatedSourceFileName} /Fe{TempExeFileName}{librariesArg}");

            if (!File.Exists(tempHostPath))
            {
                throw new InvalidOperationException(
                    $"QCL не создал {TempExeFileName}.{Environment.NewLine}{compileResult.Output}");
            }

            File.Move(tempHostPath, cachePath, overwrite: true);

            if (checksumSourcePath is not null)
            {
                WriteSourceChecksum(cachePath, checksumSourcePath);
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

    private static string[]? NormalizeLibraries(string[]? libraries)
    {
        if (libraries is null || libraries.Length == 0)
        {
            return [];
        }

        return libraries
            .Select(static lib => lib.Trim())
            .Where(static lib => lib.Length > 0)
            .Select(static lib => Path.GetFileName(lib))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static lib => lib, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Суффикс имени кэша по списку библиотек: <c>_slibce_libh</c>.</summary>
    private static string FormatLibrariesTag(string[] libraries)
    {
        if (libraries.Length == 0)
        {
            return string.Empty;
        }

        var tags = libraries
            .Select(static lib => Path.GetFileNameWithoutExtension(lib).ToLowerInvariant())
            .OrderBy(static tag => tag, StringComparer.Ordinal);

        return "_" + string.Join('_', tags);
    }

    private static string FormatLibrariesCommandLine(string[] libraries) =>
        libraries.Length == 0 ? string.Empty : " " + string.Join(' ', libraries);

    private static string NormalizeSourceFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (name.EndsWith(".EXE", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "ExeProvider.Get ожидает имя исходника (*.c), а не EXE. Укажите параметры сборки явно.",
                nameof(fileName));
        }

        if (!name.EndsWith(".c", StringComparison.OrdinalIgnoreCase))
        {
            name += ".c";
        }

        return name;
    }

    private static string GetMemoryModelTag(MemoryModel memoryModel) =>
        memoryModel switch
        {
            MemoryModel.Small => "s",
            MemoryModel.Compact => "c",
            MemoryModel.Medium => "m",
            MemoryModel.Large => "l",
            _ => throw new ArgumentOutOfRangeException(nameof(memoryModel), memoryModel, "Неподдерживаемая модель памяти."),
        };

    private static string BuildCompilerFlags(
        MemoryModel memoryModel,
        bool stackCheck,
        OptimizationLevel optimization)
    {
        var flags = new List<string> { MemoryModelDetector.GetCompilerFlag(memoryModel) };

        if (!stackCheck)
        {
            flags.Add("/Gs");
        }

        switch (optimization)
        {
            case OptimizationLevel.Disabled:
                flags.Add("/Od");
                break;
            case OptimizationLevel.EnableLoop:
                flags.Add("/Ol");
                break;
            case OptimizationLevel.Enabled:
                flags.Add("/Ot");
                break;
            case OptimizationLevel.EnabledFull:
                flags.Add("/Ox");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(optimization), optimization, null);
        }

        return string.Join(' ', flags.Where(static flag => flag.Length > 0));
    }

    /// <summary>Короткий (≤8 символов) идентификатор изолированного рабочего каталога под <c>QuickC/BUILT/TMP</c>.</summary>
    private static string CreateWorkspaceId() =>
        Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
