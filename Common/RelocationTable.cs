namespace Common;

/// <summary>
/// Таблица релокаций MZ EXE: помечает слова образа как смещения относительно именованных баз.
/// </summary>
public sealed class RelocationTable
{
    public static RelocationTable Empty { get; } = new("", []);

    private HashSet<int>? _linearAddresses;
    private Dictionary<int, string>? _offsetNamesByAddress;

    public RelocationTable(string defaultOffsetName, RelocationEntry[] entries)
    {
        DefaultOffsetName = defaultOffsetName ?? "";
        Entries = entries ?? [];
    }

    /// <summary>
    /// Имя переменной смещения по умолчанию для записей без собственного <see cref="RelocationEntry.OffsetName"/>.
    /// </summary>
    public string DefaultOffsetName { get; }

    public IReadOnlyList<RelocationEntry> Entries { get; }

    /// <summary>
    /// Линейные адреса (относительно начала загруженного образа) слов, подлежащих релокации.
    /// </summary>
    public IReadOnlySet<int> LinearAddresses => _linearAddresses ??= BuildLinearAddresses();

    public bool ContainsLinearAddress(int linearAddress) => LinearAddresses.Contains(linearAddress);

    /// <summary>
    /// Возвращает имя переменной смещения для слова по линейному адресу в образе.
    /// </summary>
    public bool TryGetOffsetName(int linearAddress, out string offsetName)
    {
        if (!ContainsLinearAddress(linearAddress))
        {
            offsetName = "";
            return false;
        }

        if (!OffsetNamesByAddress.TryGetValue(linearAddress, out offsetName!))
            offsetName = DefaultOffsetName;
        return !string.IsNullOrEmpty(offsetName);
    }

    private IReadOnlyDictionary<int, string> OffsetNamesByAddress =>
        _offsetNamesByAddress ??= BuildOffsetNamesByAddress();

    private HashSet<int> BuildLinearAddresses()
    {
        var set = new HashSet<int>(Entries.Count);
        foreach (var rel in Entries)
            set.Add(rel.LinearAddress);
        return set;
    }

    private Dictionary<int, string> BuildOffsetNamesByAddress()
    {
        var dict = new Dictionary<int, string>(Entries.Count);
        foreach (var rel in Entries)
        {
            var name = string.IsNullOrEmpty(rel.OffsetName) ? DefaultOffsetName : rel.OffsetName;
            if (string.IsNullOrEmpty(name))
                continue;
            dict[rel.LinearAddress] = name;
        }
        return dict;
    }
}
