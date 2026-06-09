namespace UltraDecompiler.Decompilation;

/// <summary>
/// Модель памяти MS-DOS, использованная при сборке программы QuickC.
/// Определяется по префиксу имени CRT-библиотеки: SLIB*, CLIB*, MLIB*, LLIB*.
/// </summary>
public enum MemoryModel
{
    /// <summary>Не удалось определить по имени библиотеки.</summary>
    Unknown,

    /// <summary>Small model — код и данные в одном сегменте (флаг <c>/AS</c>).</summary>
    Small,

    /// <summary>Compact model — один сегмент кода, данные в отдельных сегментах (<c>/AC</c>).</summary>
    Compact,

    /// <summary>Medium model — несколько сегментов кода, один сегмент данных (<c>/AM</c>).</summary>
    Medium,

    /// <summary>Large model — несколько сегментов кода и данных (<c>/AL</c>).</summary>
    Large,
}

/// <summary>
/// Определяет модель памяти по имени OMF-библиотеки QuickC.
/// </summary>
public static class MemoryModelDetector
{
    /// <summary>
    /// Возвращает модель памяти по имени файла библиотеки
    /// (<c>SLIBCE.LIB</c> → <see cref="MemoryModel.Small"/>, <c>CLIBC.LIB</c> → <see cref="MemoryModel.Compact"/> и т.д.).
    /// </summary>
    public static MemoryModel DetectFromLibraryFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        if (name.StartsWith("SLIB", StringComparison.Ordinal))
        {
            return MemoryModel.Small;
        }

        if (name.StartsWith("CLIB", StringComparison.Ordinal))
        {
            return MemoryModel.Compact;
        }

        if (name.StartsWith("MLIB", StringComparison.Ordinal))
        {
            return MemoryModel.Medium;
        }

        if (name.StartsWith("LLIB", StringComparison.Ordinal))
        {
            return MemoryModel.Large;
        }

        return MemoryModel.Unknown;
    }

    /// <summary>Возвращает флаг компилятора QuickC для модели памяти (<c>/AS</c>, <c>/AC</c>, …).</summary>
    public static string GetCompilerFlag(MemoryModel model) =>
        model switch
        {
            MemoryModel.Small => "/AS",
            MemoryModel.Compact => "/AC",
            MemoryModel.Medium => "/AM",
            MemoryModel.Large => "/AL",
            _ => string.Empty,
        };

    /// <summary>Краткое русское описание модели для вывода в консоль.</summary>
    public static string GetDisplayName(MemoryModel model) =>
        model switch
        {
            MemoryModel.Small => "Small",
            MemoryModel.Compact => "Compact",
            MemoryModel.Medium => "Medium",
            MemoryModel.Large => "Large",
            _ => "неизвестна",
        };
}
