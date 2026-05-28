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
