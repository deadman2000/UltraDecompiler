namespace UltraDecompiler.Disassembler;

/// <summary>
/// Сегментные регистры 8086
/// </summary>
public enum Segment
{
    None = 0,
    ES = 0x26,
    CS = 0x2E,
    SS = 0x36,
    DS = 0x3E
}

public static class SegmentExtensions
{
    public static string ToPrefixString(this Segment segment)
    {
        return segment switch
        {
            Segment.ES => "ES:",
            Segment.CS => "CS:",
            Segment.SS => "SS:",
            Segment.DS => "DS:",
            _ => ""
        };
    }
}