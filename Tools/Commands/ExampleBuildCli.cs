using McMaster.Extensions.CommandLineUtils;
using TestSupport;
using UltraDecompiler.Common;

namespace Tools.Commands;

/// <summary>Параметры сборки примера QuickC для <see cref="ExeProvider"/>.</summary>
internal readonly record struct ExampleBuildParameters(
    MemoryModel MemoryModel,
    bool StackCheck,
    OptimizationLevel Optimization,
    string[] Libraries);

/// <summary>Общие CLI-опции сборки примеров и разрешение пути к EXE.</summary>
internal sealed class ExampleBuildCliOptions
{
    private readonly CommandOption _modelOption;
    private readonly CommandOption _gsOption;
    private readonly CommandOption _chkstkOption;
    private readonly CommandOption _odOption;
    private readonly CommandOption _oxOption;
    private readonly CommandOption _otOption;
    private readonly CommandOption _olOption;
    private readonly CommandOption _optOption;
    private readonly CommandOption _libOption;

    private ExampleBuildCliOptions(
        CommandOption modelOption,
        CommandOption gsOption,
        CommandOption chkstkOption,
        CommandOption odOption,
        CommandOption oxOption,
        CommandOption otOption,
        CommandOption olOption,
        CommandOption optOption,
        CommandOption libOption)
    {
        _modelOption = modelOption;
        _gsOption = gsOption;
        _chkstkOption = chkstkOption;
        _odOption = odOption;
        _oxOption = oxOption;
        _otOption = otOption;
        _olOption = olOption;
        _optOption = optOption;
        _libOption = libOption;
    }

    /// <summary>Регистрирует опции сборки примера на команде CLI.</summary>
    public static ExampleBuildCliOptions AddTo(CommandLineApplication cmd)
    {
        var modelOption = cmd.Option(
            "--model <MODEL>",
            "Модель памяти: s/small, c/compact, m/medium, l/large (по умолчанию — small)",
            CommandOptionType.SingleValue);

        var gsOption = cmd.Option(
            "--gs",
            "Отключить проверку стека (/Gs); по умолчанию",
            CommandOptionType.NoValue);

        var chkstkOption = cmd.Option(
            "--chkstk",
            "Включить проверку стека (без /Gs)",
            CommandOptionType.NoValue);

        var odOption = cmd.Option(
            "--od",
            "Оптимизация отключена (/Od); по умолчанию",
            CommandOptionType.NoValue);

        var oxOption = cmd.Option(
            "--ox",
            "Максимальная оптимизация (/Ox)",
            CommandOptionType.NoValue);

        var otOption = cmd.Option(
            "--ot",
            "Оптимизация по скорости (/Ot)",
            CommandOptionType.NoValue);

        var olOption = cmd.Option(
            "--ol",
            "Оптимизация циклов (/Ol)",
            CommandOptionType.NoValue);

        var optOption = cmd.Option(
            "--opt <LEVEL>",
            "Уровень оптимизации: od, ox, ot, ol (альтернатива --od/--ox/…)",
            CommandOptionType.SingleValue);

        var libOption = cmd.Option(
            "--lib <LIB>",
            "OMF-библиотека для линковки при сборке примера (можно повторять)",
            CommandOptionType.MultipleValue);

        return new ExampleBuildCliOptions(
            modelOption,
            gsOption,
            chkstkOption,
            odOption,
            oxOption,
            otOption,
            olOption,
            optOption,
            libOption);
    }

    public bool HasAnyValue =>
        _modelOption.HasValue()
        || _gsOption.HasValue()
        || _chkstkOption.HasValue()
        || _odOption.HasValue()
        || _oxOption.HasValue()
        || _otOption.HasValue()
        || _olOption.HasValue()
        || _optOption.HasValue()
        || (_libOption.Values?.Any(static v => !string.IsNullOrWhiteSpace(v)) ?? false);

    public ExampleBuildParameters Parse()
    {
        if (_gsOption.HasValue() && _chkstkOption.HasValue())
        {
            throw new ArgumentException("Нельзя одновременно указывать --gs и --chkstk.");
        }

        var memoryModel = ParseMemoryModel(_modelOption.Value());
        var stackCheck = _chkstkOption.HasValue();
        var optimization = ParseOptimization();
        var libraries = (_libOption.Values ?? [])
            .Where(static v => !string.IsNullOrWhiteSpace(v))
            .Select(static v => v!.Trim())
            .ToArray();

        return new ExampleBuildParameters(memoryModel, stackCheck, optimization, libraries);
    }

    private OptimizationLevel ParseOptimization()
    {
        var levels = new List<OptimizationLevel>();

        if (_odOption.HasValue())
        {
            levels.Add(OptimizationLevel.Disabled);
        }

        if (_oxOption.HasValue())
        {
            levels.Add(OptimizationLevel.EnabledFull);
        }

        if (_otOption.HasValue())
        {
            levels.Add(OptimizationLevel.Enabled);
        }

        if (_olOption.HasValue())
        {
            levels.Add(OptimizationLevel.EnableLoop);
        }

        if (_optOption.HasValue())
        {
            levels.Add(ParseOptimizationLevel(_optOption.Value()));
        }

        return levels.Count switch
        {
            0 => OptimizationLevel.Disabled,
            1 => levels[0],
            _ => throw new ArgumentException(
                "Укажите только один уровень оптимизации: --od, --ox, --ot, --ol или --opt."),
        };
    }

    private static MemoryModel ParseMemoryModel(string? modelText)
    {
        if (string.IsNullOrWhiteSpace(modelText))
        {
            return MemoryModel.Small;
        }

        return modelText.Trim().ToLowerInvariant() switch
        {
            "s" or "small" or "as" => MemoryModel.Small,
            "c" or "compact" or "ac" => MemoryModel.Compact,
            "m" or "medium" or "am" => MemoryModel.Medium,
            "l" or "large" or "al" => MemoryModel.Large,
            _ => throw new ArgumentException(
                $"Неизвестная модель памяти: {modelText}. Допустимо: s, c, m, l или small, compact, medium, large."),
        };
    }

    private static OptimizationLevel ParseOptimizationLevel(string? optLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optLevel);

        return optLevel.Trim().ToLowerInvariant() switch
        {
            "od" => OptimizationLevel.Disabled,
            "ox" => OptimizationLevel.EnabledFull,
            "ot" => OptimizationLevel.Enabled,
            "ol" => OptimizationLevel.EnableLoop,
            _ => throw new ArgumentException(
                $"Неизвестный уровень оптимизации: {optLevel}. Допустимо: od, ox, ot, ol."),
        };
    }
}

internal static class ExampleInputResolver
{
    /// <summary>
    /// Разрешает путь к EXE: либо напрямую по файлу, либо через сборку примера из <c>QuickC/PROGRAMS</c>.
    /// </summary>
    public static string Resolve(string input, ExampleBuildParameters build, bool warnUnusedBuildOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (IsExePath(input))
        {
            if (warnUnusedBuildOptions)
            {
                Console.WriteLine(
                    "Параметры сборки (--model, --gs, --opt, --lib, …) игнорируются для готового EXE/COM.");
            }

            return Path.GetFullPath(input);
        }

        var exePath = ExeProvider.Get(
            input,
            build.MemoryModel,
            build.StackCheck,
            build.Optimization,
            build.Libraries);

        Console.WriteLine($"Пример: {Path.GetFileName(input)}");
        Console.WriteLine(
            $"Сборка: {MemoryModelDetector.GetCompilerFlag(build.MemoryModel)} " +
            $"{(build.StackCheck ? string.Empty : "/Gs ")}" +
            $"{OptimizationLevelDetector.GetCompilerFlag(build.Optimization)}".Trim());
        if (build.Libraries.Length > 0)
        {
            Console.WriteLine($"Библиотеки: {string.Join(' ', build.Libraries)}");
        }

        Console.WriteLine($"EXE: {exePath}");
        Console.WriteLine();

        return exePath;
    }

    private static bool IsExePath(string input)
    {
        var extension = Path.GetExtension(input);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".com", StringComparison.OrdinalIgnoreCase);
    }
}
