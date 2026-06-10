namespace UltraDecompiler.Decompilation;

/// <summary>Упрощённое представление типа C (QuickC / MSC near).</summary>
public enum CTypeKind
{
    Void,
    Char,
    Int,
    Long,
    Unsigned,
    SizeT,
    Pointer,
    Float,
    Double,
    Unknown,
}

/// <summary>Тип C для сигнатуры процедуры.</summary>
public sealed record CType(CTypeKind Kind, CType? Pointee = null)
{
    public static CType Void { get; } = new(CTypeKind.Void);

    public static CType Int { get; } = new(CTypeKind.Int);

    /// <summary>Беззнаковый 16-битный тип (<c>unsigned</c> / <c>unsigned int</c> в QuickC).</summary>
    public static CType UnsignedInt { get; } = new(CTypeKind.Unsigned);

    /// <summary>Базовый тип char (8-битный).</summary>
    public static CType Char { get; } = new(CTypeKind.Char);

    /// <summary>Указатель на char (char*), используется для форматных строк printf и т.п.</summary>
    public static CType CharPtr { get; } = new(CTypeKind.Pointer, new CType(CTypeKind.Char));

    public bool IsVoid => Kind == CTypeKind.Void;

    /// <summary>Является ли тип char* (в т.ч. const char* из заголовков).</summary>
    public bool IsCharPtr =>
        (Kind == CTypeKind.Char && Pointee != null) ||
        (Kind == CTypeKind.Pointer && Pointee?.Kind == CTypeKind.Char);

    /// <summary>Является ли тип void*.</summary>
    public bool IsVoidPtr =>
        Kind == CTypeKind.Pointer && Pointee?.IsVoid == true;

    public override string ToString() => Kind switch
    {
        CTypeKind.Void => "void",
        CTypeKind.Char => Pointee is null ? "char" : "char*",
        CTypeKind.Int => "int",
        CTypeKind.Long => "long",
        CTypeKind.Unsigned => "unsigned",
        CTypeKind.SizeT => "size_t",
        CTypeKind.Float => "float",
        CTypeKind.Double => "double",
        CTypeKind.Pointer => $"{Pointee ?? new CType(CTypeKind.Unknown)}*",
        CTypeKind.Unknown => "unknown",
        _ => Kind.ToString().ToLowerInvariant(),
    };
}
