namespace UltraDecompiler.Compilation;

/// <summary>Уровень оптимизации QuickC (<c>/Od</c>, <c>/Ot</c>, <c>/Ol</c>, <c>/Ox</c>).</summary>
public enum OptimizationLevel
{
    /// <summary>Оптимизация отключена (<c>/Od</c>).</summary>
    Disabled,

    /// <summary>Оптимизация по скорости (<c>/Ot</c>).</summary>
    Enabled,

    /// <summary>Оптимизация циклов (<c>/Ol</c>).</summary>
    EnableLoop,

    /// <summary>Максимальная оптимизация (<c>/Ox</c>).</summary>
    EnabledFull,
}

/// <summary>Преобразует <see cref="OptimizationLevel"/> в флаги и описания QuickC.</summary>
public static class OptimizationLevelDetector
{
    /// <summary>Возвращает флаг компилятора QuickC для уровня оптимизации.</summary>
    public static string GetCompilerFlag(OptimizationLevel level) =>
        level switch
        {
            OptimizationLevel.Disabled => "/Od",
            OptimizationLevel.Enabled => "/Ot",
            OptimizationLevel.EnableLoop => "/Ol",
            OptimizationLevel.EnabledFull => "/Ox",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };

    /// <summary>Краткое описание уровня оптимизации для вывода в консоль.</summary>
    public static string GetDisplayName(OptimizationLevel level) =>
        level switch
        {
            OptimizationLevel.Disabled => "отключена (/Od)",
            OptimizationLevel.Enabled => "по скорости (/Ot)",
            OptimizationLevel.EnableLoop => "циклы (/Ol)",
            OptimizationLevel.EnabledFull => "максимальная (/Ox)",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };
}
