namespace UltraDecompiler.Disassembler;

public readonly struct Operand
{
    public static readonly Operand AL = new(OperandType.Register8, 0);
    public static readonly Operand AX = new(OperandType.Register16, 0);
    public static readonly Operand CL = new(OperandType.Register8, 1);
    public static readonly Operand CX = new(OperandType.Register16, 1);
    public static readonly Operand DL = new(OperandType.Register8, 2);
    public static readonly Operand DX = new(OperandType.Register16, 2);
    public static readonly Operand ES = new(OperandType.SegmentRegister, 0);
    public static readonly Operand CS = new(OperandType.SegmentRegister, 1);
    public static readonly Operand SS = new(OperandType.SegmentRegister, 2);
    public static readonly Operand DS = new(OperandType.SegmentRegister, 3);

    public readonly OperandType Type;
    public readonly int Value;        // immediate, displacement, or register index
    public readonly AddressRegister BaseReg;     // for memory: BX, BP, SI, DI
    public readonly AddressRegister IndexReg;    // for memory: SI, DI

    public readonly string? Relocation;

    public Operand(
        OperandType type,
        int value = 0,
        AddressRegister baseReg = AddressRegister.None,
        AddressRegister indexReg = AddressRegister.None,
        string? relocation = null)
    {
        // TODO Подумать как правильно сделать
        if (value > ushort.MaxValue)
            value = (ushort)value;

        Type = type;
        Value = value;
        BaseReg = baseReg;
        IndexReg = indexReg;
        Relocation = relocation;
    }

    public bool IsSet => Type != OperandType.None;

    public override string ToString()
    {
        return this.ToAsm();
    }
}