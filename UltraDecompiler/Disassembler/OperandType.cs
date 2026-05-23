namespace UltraDecompiler.Disassembler;

public enum OperandType : byte
{
    None = 0,
    Register8,
    Register16,
    Immediate8,
    Immediate16,
    Memory,
    Relative8,
    Relative16,
    SegmentRegister
}
