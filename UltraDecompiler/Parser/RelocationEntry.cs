using System.Runtime.InteropServices;

namespace UltraDecompiler.Parser;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RelocationEntry
{
    public ushort Offset;
    public ushort Segment;

    /// <summary>
    /// Линейный адрес слова в образе по записи таблицы релокаций.
    /// </summary>
    public int LinearAddress => Segment * 16 + Offset;
}