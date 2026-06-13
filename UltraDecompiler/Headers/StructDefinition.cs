using UltraDecompiler.Decompilation;

namespace UltraDecompiler.Headers;

/// <summary>Поле структуры из заголовка QuickC.</summary>
/// <param name="Name">Имя поля.</param>
/// <param name="Type">Тип поля.</param>
/// <param name="Offset">Смещение от начала структуры (байты).</param>
/// <param name="Size">Размер поля в байтах.</param>
public sealed record StructField(string Name, CType Type, int Offset, int Size);

/// <summary>Описание <c>struct</c> из INCLUDE/*.H (раскладка полей как у QuickC).</summary>
public sealed class StructDefinition
{
    public StructDefinition(
        string name,
        string headerFile,
        IReadOnlyList<StructField> fields,
        bool isUnion = false,
        int? sizeOverride = null)
    {
        Name = name;
        HeaderFile = headerFile;
        Fields = fields;
        IsUnion = isUnion;
        Size = sizeOverride ?? ComputeSize(fields);
        _fieldByOffset = fields.ToDictionary(static f => f.Offset);
    }

    /// <summary>Имя типа (<c>dosdate_t</c>).</summary>
    public string Name { get; }

    /// <summary>true для <c>union</c> из заголовка QuickC (<c>REGS</c> и т.п.).</summary>
    public bool IsUnion { get; }

    /// <summary>Файл заголовка (<c>DOS.H</c>).</summary>
    public string HeaderFile { get; }

    /// <summary>Поля в порядке объявления.</summary>
    public IReadOnlyList<StructField> Fields { get; }

    /// <summary>Размер структуры в байтах (с выравниванием под 16-битную модель).</summary>
    public int Size { get; }

    private readonly Dictionary<int, StructField> _fieldByOffset;

    /// <summary>Тип C для объявления переменной.</summary>
    public CType CType => IsUnion ? CType.UnionType(Name) : CType.StructType(Name);

    /// <summary>Указатель на структуру или union.</summary>
    public CType PointerType => IsUnion ? CType.UnionPointer(Name) : CType.StructPointer(Name);

    public bool TryGetFieldAtOffset(int offset, out StructField? field) =>
        _fieldByOffset.TryGetValue(offset, out field);

    /// <summary>
    /// Ищет поле, начинающееся по смещению <paramref name="offset"/> от базы структуры.
    /// Для байтовых полей смещение должно совпадать точно; для слов — только чётные.
    /// </summary>
    public bool TryResolveField(int offset, out StructField? field)
    {
        if (_fieldByOffset.TryGetValue(offset, out field))
        {
            return true;
        }

        foreach (var candidate in Fields)
        {
            if (offset >= candidate.Offset && offset < candidate.Offset + candidate.Size)
            {
                field = candidate;
                return true;
            }
        }

        field = null;
        return false;
    }

    private static int ComputeSize(IReadOnlyList<StructField> fields)
    {
        if (fields.Count == 0)
        {
            return 0;
        }

        var maxEnd = fields.Max(static f => f.Offset + f.Size);
        var alignment = fields.Max(static f => FieldAlignment(f.Type));
        return AlignUp(maxEnd, alignment);
    }

    private static int FieldAlignment(CType type) =>
        type.Kind switch
        {
            CTypeKind.Char => 1,
            CTypeKind.Int or CTypeKind.Unsigned => 2,
            CTypeKind.Long => 4,
            CTypeKind.Struct => 2,
            CTypeKind.Pointer => 2,
            _ => 2,
        };

    private static int FieldSize(CType type) =>
        type.Kind switch
        {
            CTypeKind.Char => 1,
            CTypeKind.Int or CTypeKind.Unsigned => 2,
            CTypeKind.Long => 4,
            CTypeKind.Pointer => 2,
            _ => 2,
        };

    /// <summary>Строит поле с учётом выравнивания QuickC/MSC.</summary>
    public static StructField CreateField(string name, CType type, int currentOffset)
    {
        var alignment = FieldAlignment(type);
        var offset = AlignUp(currentOffset, alignment);
        var size = FieldSize(type);
        return new StructField(name, type, offset, size);
    }

    private static int AlignUp(int value, int alignment) =>
        alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;
}
