namespace UltraDecompiler.Disassembler;

/// <summary>
/// Значения регистров. null - если значение неизвестно.
/// </summary>
public record struct RegisterState(
    byte? AH, byte? AL,
    byte? BH, byte? BL,
    byte? CH, byte? CL,
    byte? DH, byte? DL,
    ushort? SP, ushort? BP, ushort? SI, ushort? DI,
    ushort? ES, ushort? CS, ushort? SS, ushort? DS,
    bool? DF)   // Direction Flag: false = вперёд (CLD), true = назад (STD), null = неизвестно
{
    public static readonly RegisterState Zeros = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);

    public static readonly RegisterState InitExe = Zeros with { SP = null, CS = null, DS = null, SS = null, DF = false };

    public static readonly RegisterState InitCom = Zeros with { SP = 0xfffe, CS = null, DS = null, ES = null, SS = null, DF = false };

    public static readonly RegisterState Unknown = new();

    public ushort? AX => AH.HasValue && AL.HasValue ? (ushort)((AH.Value << 8) | AL.Value) : null;
    public ushort? BX => BH.HasValue && BL.HasValue ? (ushort)((BH.Value << 8) | BL.Value) : null;
    public ushort? CX => CH.HasValue && CL.HasValue ? (ushort)((CH.Value << 8) | CL.Value) : null;
    public ushort? DX => DH.HasValue && DL.HasValue ? (ushort)((DH.Value << 8) | DL.Value) : null;
}