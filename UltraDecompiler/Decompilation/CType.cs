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

    public bool IsVoid => Kind == CTypeKind.Void;

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
