using System.Runtime.InteropServices;

namespace UltraDecompiler.Parser;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RelocationEntry
{
    public ushort Offset;
    public ushort Segment;
}