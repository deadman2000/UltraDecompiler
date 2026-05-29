namespace UltraDecompiler.Parser;

/// <summary>
/// Таблица релокаций MZ EXE и применение fixup к загруженному образу.
/// </summary>
public sealed class RelocationTable
{
    private readonly RelocationEntry[] _entries;
    private HashSet<int>? _linearAddresses;

    public RelocationTable(RelocationEntry[] entries)
    {
        _entries = entries ?? [];
    }

    public static RelocationTable Empty { get; } = new([]);

    public int Count => _entries.Length;

    public IReadOnlyList<RelocationEntry> Entries => _entries;

    /// <summary>
    /// Линейные адреса (относительно начала загруженного образа) слов, подлежащих релокации.
    /// </summary>
    public IReadOnlySet<int> LinearAddresses => _linearAddresses ??= BuildLinearAddresses();

    public bool ContainsLinearAddress(int linearAddress) => LinearAddresses.Contains(linearAddress);

    private HashSet<int> BuildLinearAddresses()
    {
        var set = new HashSet<int>(_entries.Length);
        foreach (var rel in _entries)
            set.Add(rel.LinearAddress);
        return set;
    }
}
