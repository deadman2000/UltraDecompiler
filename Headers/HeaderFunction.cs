namespace UltraDecompiler.Headers;

/// <summary>Параметр функции из объявления в заголовке QuickC (<c>*.H</c>).</summary>
public sealed record HeaderFunctionParameter(CType Type);

/// <summary>Сигнатура функции, извлечённая из заголовка (без привязки к стеку/регистрам IR).</summary>
public sealed class HeaderFunction
{
    public CType ReturnType { get; }

    public IReadOnlyList<HeaderFunctionParameter> Parameters { get; }

    /// <summary>Функция объявлена с <c>...</c>.</summary>
    public bool IsVariadic { get; }

    public HeaderFunction(
        CType returnType,
        IReadOnlyList<HeaderFunctionParameter> parameters,
        bool isVariadic = false)
    {
        ReturnType = returnType;
        Parameters = parameters;
        IsVariadic = isVariadic;
    }
}
