namespace UltraDecompiler;

public class Instruction
{
    public const string UnknownOperand = "; unknown";

    public int Offset { get; set; }
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string Mnemonic { get; set; } = "";

    public string Operands { get; set; } = "";
    public Operand[] OperandsInfo { get; set; } = Array.Empty<Operand>();

    /// <summary>
    /// Возвращает целевой адрес прямого перехода
    /// </summary>
    public int GetJumpTarget()
    {
        foreach (var op in OperandsInfo)
        {
            if (op.Type is OperandType.Relative8 or OperandType.Relative16)
                return op.Value;
        }
        return -1;
    }

    /// <summary>
    /// Вычисляет эффективный адрес перехода (для косвенных вызовов FF /2 и FF /4)
    /// </summary>
    public int GetEffectiveJumpTarget(byte[] image)
    {
        // Прямой переход
        int direct = GetJumpTarget();
        if (direct != -1)
            return direct;

        // Косвенный переход (CALL/JMP через память)
        if ((Mnemonic == "CALL" || Mnemonic == "JMP") && OperandsInfo.Length > 0)
        {
            var op = OperandsInfo[0];
            if (op.Type == OperandType.Memory && op.Value >= 0 && op.Value + 2 <= image.Length)
            {
                ushort target = (ushort)(image[op.Value] | (image[op.Value + 1] << 8));
                return target;
            }
        }

        return -1;
    }

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