namespace UltraDecompiler.Disassembly.Disassembler;

/// <summary>
/// 16-битный регистр общего назначения (индекс совпадает с <see cref="Operand.Value"/>).
/// </summary>
public enum GpRegister16 : byte
{
    AX = 0,
    CX = 1,
    DX = 2,
    BX = 3,
    SP = 4,
    BP = 5,
    SI = 6,
    DI = 7,
}

/// <summary>
/// 8-битный регистр (индекс совпадает с <see cref="Operand.Value"/>).
/// </summary>
public enum GpRegister8 : byte
{
    AL = 0,
    CL = 1,
    DL = 2,
    BL = 3,
    AH = 4,
    CH = 5,
    DH = 6,
    BH = 7,
}

/// <summary>
/// Сегментный регистр (индекс совпадает с <see cref="Operand.Value"/> для <see cref="OperandType.SegmentRegister"/>).
/// </summary>
public enum CpuSegmentRegister : byte
{
    ES = 0,
    CS = 1,
    SS = 2,
    DS = 3,
}
