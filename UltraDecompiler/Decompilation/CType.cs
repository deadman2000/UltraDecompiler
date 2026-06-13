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
    Struct,
    Float,
    Double,
    Unknown,
}

/// <summary>Тип C для сигнатуры процедуры.</summary>
public sealed record CType(CTypeKind Kind, CType? Pointee = null, string? StructName = null, bool IsFar = false, bool IsUnion = false)
{
    public static CType Void { get; } = new(CTypeKind.Void);

    public static CType Int { get; } = new(CTypeKind.Int);

    /// <summary>32-битный знаковый тип <c>long</c> (QuickC / MSC).</summary>
    public static CType Long { get; } = new(CTypeKind.Long);

    /// <summary>Беззнаковый 16-битный тип (<c>unsigned</c> / <c>unsigned int</c> в QuickC).</summary>
    public static CType UnsignedInt { get; } = new(CTypeKind.Unsigned);

    /// <summary>Базовый тип char (8-битный).</summary>
    public static CType Char { get; } = new(CTypeKind.Char);

    /// <summary>Указатель на char (char*), используется для форматных строк printf и т.п.</summary>
    public static CType CharPtr { get; } = new(CTypeKind.Pointer, new CType(CTypeKind.Char));

    /// <summary>Дальний указатель на char (<c>char far *</c>, видеопамять и сегментные буферы DOS).</summary>
    public static CType CharFarPtr { get; } = new(CTypeKind.Pointer, Char, IsFar: true);

    /// <summary>Указатель на char* (<c>char **</c>, параметры <c>argv</c>/<c>envp</c> в main).</summary>
    public static CType CharPtrPtr { get; } = new(CTypeKind.Pointer, CharPtr);

    /// <summary>Структура из заголовка QuickC (<c>struct name</c>).</summary>
    public static CType StructType(string name) => new(CTypeKind.Struct, StructName: name);

    /// <summary>Объединение из заголовка QuickC (<c>union name</c>).</summary>
    public static CType UnionType(string name) => new(CTypeKind.Struct, StructName: name, IsUnion: true);

    /// <summary>Указатель на структуру (<c>struct name*</c>).</summary>
    public static CType StructPointer(string name) => new(CTypeKind.Pointer, StructType(name));

    /// <summary>Указатель на union (<c>union name*</c>).</summary>
    public static CType UnionPointer(string name) => new(CTypeKind.Pointer, UnionType(name));

    public bool IsVoid => Kind == CTypeKind.Void;

    public bool IsStruct => Kind == CTypeKind.Struct;

    public bool IsStructPtr =>
        Kind == CTypeKind.Pointer && Pointee?.Kind == CTypeKind.Struct;

    /// <summary>Является ли тип char* (в т.ч. const char* из заголовков).</summary>
    public bool IsCharPtr =>
        !IsFar &&
        ((Kind == CTypeKind.Char && Pointee != null) ||
        (Kind == CTypeKind.Pointer && Pointee?.Kind == CTypeKind.Char));

    /// <summary>Является ли тип <c>char far *</c>.</summary>
    public bool IsCharFarPtr =>
        IsFar && Kind == CTypeKind.Pointer && Pointee?.Kind == CTypeKind.Char;

    /// <summary>Является ли тип char** (<c>char *argv[]</c> / <c>char *envp[]</c>).</summary>
    public bool IsCharPtrPtr =>
        Kind == CTypeKind.Pointer && Pointee?.IsCharPtr == true;

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
        CTypeKind.Struct when IsUnion => $"union {StructName}",
        CTypeKind.Struct => $"struct {StructName}",
        CTypeKind.Pointer when IsFar && Pointee?.Kind == CTypeKind.Char => "char far *",
        CTypeKind.Pointer when IsFar => $"{Pointee ?? new CType(CTypeKind.Unknown)} far *",
        CTypeKind.Pointer when Pointee is { Kind: CTypeKind.Struct, IsUnion: true } => $"union {Pointee.StructName}*",
        CTypeKind.Pointer when Pointee?.Kind == CTypeKind.Struct => $"struct {Pointee.StructName}*",
        CTypeKind.Pointer => $"{Pointee ?? new CType(CTypeKind.Unknown)}*",
        CTypeKind.Unknown => "unknown",
        _ => Kind.ToString().ToLowerInvariant(),
    };
}
