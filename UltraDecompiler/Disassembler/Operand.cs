using System.Diagnostics;
using UltraDecompiler.Extensions;

namespace UltraDecompiler.Disassembler;

public readonly struct Operand
{
    public readonly OperandType Type;
    public readonly int Value;        // immediate, displacement, or register index
    public readonly AddressRegister BaseReg;     // for memory: BX, BP, SI, DI
    public readonly AddressRegister IndexReg;    // for memory: SI, DI
    public readonly byte Scale;       // usually 1

    public Operand(OperandType type, int value = 0, AddressRegister baseReg = AddressRegister.None, AddressRegister indexReg = AddressRegister.None, byte scale = 1)
    {
        Debug.Assert(value >= short.MinValue && value <= ushort.MaxValue);
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
            OperandType.Immediate8 or OperandType.Immediate16 => Value.ToHex(),
            OperandType.Memory => GetMemoryString(),
            OperandType.Relative8 or OperandType.Relative16 => Value.ToHex(),
            OperandType.SegmentRegister => GetSegRegName(),
            _ => "?"
        };
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
        if (BaseReg != AddressRegister.None)
        {
            string baseName = BaseReg switch
            {
                AddressRegister.BX => "BX",
                AddressRegister.BP => "BP",
                AddressRegister.SI => "SI",
                AddressRegister.DI => "DI",
                _ => "?"
            };
            parts.Add(baseName);
        }

        // Index register
        if (IndexReg != AddressRegister.None && IndexReg != BaseReg)
        {
            string idxName = IndexReg switch
            {
                AddressRegister.BX => "BX",
                AddressRegister.BP => "BP",
                AddressRegister.SI => "SI",
                AddressRegister.DI => "DI",
                _ => "?"
            };
            parts.Add(idxName);
        }

        // Displacement
        if (Value != 0)
        {
            parts.Add(Value.ToHex());
        }

        if (parts.Count == 0)
            return "[0]";

        return $"[{string.Join("+", parts)}]";
    }
}