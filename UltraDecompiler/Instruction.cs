namespace UltraDecompiler;

public class Instruction
{
    public const string UnknownOperand = "; unknown";

    public int Offset { get; set; }
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string Mnemonic { get; set; } = "";
    public string Operands { get; set; } = "";

    public override string ToString()
    {
        string bytesStr = string.Join(" ", Bytes.Select(b => $"{b:X2}"));
        return $"0x{Offset:X6}: {bytesStr,-20} {Mnemonic,-7} {Operands}";
    }

    public string ToColoredString()
    {
        string bytesStr = string.Join(" ", Bytes.Select(b => $"{b:X2}"));

        const string RESET = "\u001b[0m";
        const string GRAY = "\u001b[90m";
        const string RED = "\u001b[91m";
        const string GREEN = "\u001b[92m";
        const string YELLOW = "\u001b[93m";

        string coloredOperands = Operands;

        if (Operands.Contains("ES") || Operands.Contains("CS") || Operands.Contains("SS") || Operands.Contains("DS"))
            coloredOperands = GREEN + Operands + RESET;

        var instructionColor = YELLOW;
        if (Operands == UnknownOperand)
            instructionColor = RED;

        return $"{GRAY}0x{Offset:X6}:{RESET} {GRAY}{bytesStr,-20}{RESET} {instructionColor}{Mnemonic,-5}{RESET} {coloredOperands}";
    }
}
