namespace UltraDecompiler.Disassembler;

/// <summary>
/// Значения регистров. null - если значение неизвестно.
/// </summary>
public record struct RegisterState(
    byte? AH, byte? AL,
    byte? BH, byte? BL,
    byte? CH, byte? CL,
    byte? DH, byte? DL)
{
    public static readonly RegisterState Zeros = new(0, 0, 0, 0, 0, 0, 0, 0);
}
