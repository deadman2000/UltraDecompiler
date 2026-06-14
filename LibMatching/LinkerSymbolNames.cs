namespace UltraDecompiler.LibMatching;

/// <summary>Преобразование имён символов линкера QuickC/MSC в имена C из заголовков.</summary>
public static class LinkerSymbolNames
{
    /// <summary>
    /// Убирает ведущее подчёркивание линкера: <c>_main</c> → <c>main</c>, <c>_printf</c> → <c>printf</c>,
    /// <c>__chkstk</c> → <c>_chkstk</c>.
    /// </summary>
    public static string ToCName(string linkerSymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linkerSymbol);
        return linkerSymbol.StartsWith('_') ? linkerSymbol[1..] : linkerSymbol;
    }
}
