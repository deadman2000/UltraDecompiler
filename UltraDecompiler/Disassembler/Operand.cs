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

public readonly struct Operand
{
    public readonly OperandType Type;
    public readonly int Value;        // immediate, displacement, or register index
    public readonly byte BaseReg;     // for memory: BX, BP, SI, DI
    public readonly byte IndexReg;    // for memory: SI, DI
    public readonly byte Scale;       // usually 1

    public Operand(OperandType type, int value = 0, byte baseReg = 0, byte indexReg = 0, byte scale = 1)
    {
        Type = type;
        Value = value;
        BaseReg = baseReg;
        IndexReg = indexReg;
        Scale = scale;
    }

    public bool IsSet => Type != OperandType.None;

    public override string ToString()
    {
        return Type switch
        {
            OperandType.Register8 or OperandType.Register16 => GetRegName(),
            OperandType.Immediate8 or OperandType.Immediate16 => GetValueHex(),
            OperandType.Memory => GetMemoryString(),
            OperandType.Relative8 or OperandType.Relative16 => GetValueHex(),
            OperandType.SegmentRegister => GetSegRegName(),
            _ => "?"
        };
    }

    private string GetValueHex()
    {
        if (Value > -10 && Value < 10)
            return Value.ToString();

        return $"{Value:X4}h";
    }

    private string GetRegName() => Value switch
    {
        0 => Type == OperandType.Register8 ? "AL" : "AX",
        1 => Type == OperandType.Register8 ? "CL" : "CX",
        2 => Type == OperandType.Register8 ? "DL" : "DX",
        3 => Type == OperandType.Register8 ? "BL" : "BX",
        4 => Type == OperandType.Register8 ? "AH" : "SP",
        5 => Type == OperandType.Register8 ? "CH" : "BP",
        6 => Type == OperandType.Register8 ? "DH" : "SI",
        7 => Type == OperandType.Register8 ? "BH" : "DI",
        _ => "?"
    };

    private string GetSegRegName() => Value switch
    {
        0 => "ES",
        1 => "CS",
        2 => "SS",
        3 => "DS",
        _ => "?SREG"
    };

    private string GetMemoryString()
    {
        var parts = new List<string>();

        // Base register
        if (BaseReg != 0)
        {
            string baseName = BaseReg switch
            {
                3 => "BX",
                5 => "BP",
                6 => "SI",
                7 => "DI",
                _ => "?"
            };
            parts.Add(baseName);
        }

        // Index register
        if (IndexReg != 0 && IndexReg != BaseReg)
        {
            string idxName = IndexReg switch
            {
                3 => "BX",
                5 => "BP",
                6 => "SI",
                7 => "DI",
                _ => "?"
            };
            parts.Add(idxName);
        }

        // Displacement
        if (Value != 0)
        {
            if (Value < 10)
                parts.Add(Value.ToString());
            else
                parts.Add($"{Value:X4}h");
        }

        if (parts.Count == 0)
            return "[0]";

        return $"[{string.Join("+", parts)}]";
    }
}