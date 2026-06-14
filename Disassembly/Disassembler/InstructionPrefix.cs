namespace UltraDecompiler.Disassembly.Disassembler;

[Flags]
public enum InstructionPrefix
{
    None = 0,
    LOCK = 1,
    REPZ = 2,
    REPNZ = 4
}
