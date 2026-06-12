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
public sealed record CType(CTypeKind Kind, CType? Pointee = null, string? StructName = null)
{
    public static CType Void { get; } = new(CTypeKind.Void);

    public static CType Int { get; } = new(CTypeKind.Int);

    /// <summary>Беззнаковый 16-битный тип (<c>unsigned</c> / <c>unsigned int</c> в QuickC).</summary>
    public static CType UnsignedInt { get; } = new(CTypeKind.Unsigned);

    /// <summary>Базовый тип char (8-битный).</summary>
    public static CType Char { get; } = new(CTypeKind.Char);

    /// <summary>Указатель на char (char*), используется для форматных строк printf и т.п.</summary>
    public static CType CharPtr { get; } = new(CTypeKind.Pointer, new CType(CTypeKind.Char));

    /// <summary>Указатель на char* (<c>char **</c>, параметры <c>argv</c>/<c>envp</c> в main).</summary>
    public static CType CharPtrPtr { get; } = new(CTypeKind.Pointer, CharPtr);

    /// <summary>Структура из заголовка QuickC (<c>struct name</c>).</summary>
    public static CType StructType(string name) => new(CTypeKind.Struct, StructName: name);

    /// <summary>Указатель на структуру (<c>struct name*</c>).</summary>
    public static CType StructPointer(string name) => new(CTypeKind.Pointer, StructType(name));

    public bool IsVoid => Kind == CTypeKind.Void;

    public bool IsStruct => Kind == CTypeKind.Struct;

    public bool IsStructPtr =>
        Kind == CTypeKind.Pointer && Pointee?.Kind == CTypeKind.Struct;

    /// <summary>Является ли тип char* (в т.ч. const char* из заголовков).</summary>
    public bool IsCharPtr =>
        (Kind == CTypeKind.Char && Pointee != null) ||
        (Kind == CTypeKind.Pointer && Pointee?.Kind == CTypeKind.Char);

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
        CTypeKind.Struct => $"struct {StructName}",
        CTypeKind.Pointer when Pointee?.Kind == CTypeKind.Struct => $"struct {Pointee.StructName}*",
        CTypeKind.Pointer => $"{Pointee ?? new CType(CTypeKind.Unknown)}*",
        CTypeKind.Unknown => "unknown",
        _ => Kind.ToString().ToLowerInvariant(),
    };
}
