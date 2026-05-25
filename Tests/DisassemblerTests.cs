using UltraDecompiler.Disassembler;

namespace Tests;

public class DisassemblerTests : BaseTests
{
    [Fact]
    public void DisassembleNearIndirectCall()
    {
        var instructions = Disassemble("FF 16 46 00"); // CALL WORD PTR [0x46]
        Assert.Equal(Mnemonic.CALL, instructions[0].Mnemonic);
        Assert.Equal("[46h]", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x0046, instructions[0].Operand1.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
    }

    [Fact]
    public void DisassembleDirectNearJump()
    {
        var instructions = Disassemble("E9 05 00"); // JMP +5
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
        Assert.Equal("8", instructions[0].Operands);
        Assert.Equal(OperandType.Relative16, instructions[0].Operand1.Type);
        Assert.Equal(8, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleDirectShortJump()
    {
        var instructions = Disassemble("EB 05"); // JMP SHORT +5
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleIndirectJumpWithBaseIndex()
    {
        var instructions = Disassemble("FF 27"); // JMP WORD PTR [BX]
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg); // BX
    }

    [Fact]
    public void DisassembleComplexIndirectCall()
    {
        // CALL WORD PTR [BX+SI+1234]
        var instructions = Disassemble("FF 90 34 12");
        Assert.Equal(Mnemonic.CALL, instructions[0].Mnemonic);
        Assert.Contains("BX+SI", instructions[0].Operands);
        Assert.Contains("1234", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg); // BX
        Assert.Equal(AddressRegister.SI, instructions[0].Operand1.IndexReg); // SI
    }

    [Fact]
    public void DisassembleFarIndirectJump()
    {
        // JMP DWORD PTR [1234] (far jump through memory)
        var instructions = Disassemble("FF 2E 34 12");
        Assert.Equal(Mnemonic.JMP_FAR, instructions[0].Mnemonic);
        Assert.Contains("1234", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
    }

    [Fact]
    public void DisassembleMovWithComplexMemory()
    {
        // MOV AX, [BP+DI+5]
        var instructions = Disassemble("8B 43 05");
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AX, [BP+DI+5]", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(5, instructions[0].Operand2.Value);
        Assert.Equal(AddressRegister.BP, instructions[0].Operand2.BaseReg); // BP
        Assert.Equal(AddressRegister.DI, instructions[0].Operand2.IndexReg); // DI
    }

    [Fact]
    public void DisassemblePushMemory()
    {
        // PUSH WORD PTR [1234]
        var instructions = Disassemble("FF 36 34 12");
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Contains("1234", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
    }

    [Fact]
    public void DisassembleWithSegmentOverride()
    {
        // ES: MOV AX, [1234]
        var instructions = Disassemble("26 8B 06 34 12");
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Contains("ES:", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleLockPrefix()
    {
        // LOCK INC WORD PTR [BX]
        var instructions = Disassemble("F0 FF 07");
        Assert.Equal(InstructionPrefix.LOCK, instructions[0].Prefix);
        Assert.Equal(Mnemonic.INC, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Single(instructions);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg); // BX
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
    }

    [Fact]
    public void DisassembleRepzPrefix()
    {
        // REPZ MOVSB
        var instructions = Disassemble("F3 A4");
        Assert.Equal(InstructionPrefix.REPZ, instructions[0].Prefix);
        Assert.Equal(Mnemonic.MOVSB, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleRepnzPrefix()
    {
        // REPNZ CMPSB
        var instructions = Disassemble("F2 A6");
        Assert.Equal(InstructionPrefix.REPNZ, instructions[0].Prefix);
        Assert.Equal(Mnemonic.CMPSB, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleLockWithSegmentOverride()
    {
        // LOCK ES: INC WORD PTR [BX]
        var instructions = Disassemble("F0 26 FF 07");
        Assert.Equal(InstructionPrefix.LOCK, instructions[0].Prefix);
        Assert.Equal(Segment.ES, instructions[0].Segment);
        Assert.Equal(Mnemonic.INC, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
    }

    [Fact]
    public void DisassembleRepzWithSegmentOverride()
    {
        // REPZ SS: MOVSW
        var instructions = Disassemble("F3 36 A5");
        Assert.Equal(InstructionPrefix.REPZ, instructions[0].Prefix);
        Assert.Equal(Segment.SS, instructions[0].Segment);
        Assert.Equal(Mnemonic.MOVSW, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleMultiplePrefixes()
    {
        // LOCK REPNZ SCASW (редкий, но валидный случай)
        var instructions = Disassemble("F0 F2 AE");
        Assert.Equal(InstructionPrefix.LOCK | InstructionPrefix.REPNZ, instructions[0].Prefix);
        Assert.Equal(Mnemonic.SCASB, instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembSegmentChange()
    {
        var instructions = Disassemble("""
            36 FF 36 DA 00;  push word ptr ss:[0xda]
            FF 36 DA 00;     push word ptr ds:[0xda]
            """);
        Assert.Equal(2, instructions.Count);

        Assert.Equal(Segment.SS, instructions[0].Segment);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x00DA, instructions[0].Operand1.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.BaseReg);

        Assert.Equal(Segment.None, instructions[1].Segment);
        Assert.Equal(OperandType.Memory, instructions[1].Operand1.Type);
        Assert.Equal(0x00DA, instructions[1].Operand1.Value);
        Assert.Equal(AddressRegister.None, instructions[1].Operand1.BaseReg);

        Assert.StartsWith("SS:", instructions[0].Operands);
        Assert.Equal("[DAh]", instructions[1].Operands);
    }

    [Fact]
    public void DisassembleMovImmediate()
    {
        var instructions = Disassemble("B8 34 12"); // MOV AX, 1234h
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AX, 1234h", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleAddRegImm8()
    {
        var instructions = Disassemble("04 05"); // ADD AL, 5
        Assert.Equal(Mnemonic.ADD, instructions[0].Mnemonic);
        Assert.Equal("AL, 5", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(5, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleSubRegReg()
    {
        var instructions = Disassemble("29 CB"); // SUB BX, CX
        Assert.Equal(Mnemonic.SUB, instructions[0].Mnemonic);
        Assert.Equal("BX, CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMulReg()
    {
        var instructions = Disassemble("F7 E1"); // MUL CX
        Assert.Equal(Mnemonic.MUL, instructions[0].Mnemonic);
        Assert.Equal("CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(1, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleIncBytePtr()
    {
        var instructions = Disassemble("FE 07"); // INC BYTE PTR [BX]
        Assert.Equal(Mnemonic.INC, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand1.IndexReg);
    }

    [Fact]
    public void DisassembleCmpRegImm()
    {
        var instructions = Disassemble("3C 00"); // CMP AL, 0
        Assert.Equal(Mnemonic.CMP, instructions[0].Mnemonic);
        Assert.Equal("AL, 0", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleAndRegImm()
    {
        var instructions = Disassemble("25 FF 00"); // AND AX, 00FFh
        Assert.Equal(Mnemonic.AND, instructions[0].Mnemonic);
        Assert.Equal("AX, FFh", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0x00FF, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleLea()
    {
        var instructions = Disassemble("8D 5C 0A"); // LEA BX, [SI+0Ah]
        Assert.Equal(Mnemonic.LEA, instructions[0].Mnemonic);
        Assert.Equal("BX, [SI+0Ah]", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x0A, instructions[0].Operand2.Value);
        Assert.Equal(AddressRegister.SI, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleRetNear()
    {
        var instructions = Disassemble("C3"); // RET
        Assert.Equal(Mnemonic.RET, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassemblePushReg16()
    {
        var instructions = Disassemble("50"); // PUSH AX
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX = 0
    }

    [Fact]
    public void DisassemblePushReg16_BX()
    {
        var instructions = Disassemble("53"); // PUSH BX
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("BX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value); // BX = 3
    }

    [Fact]
    public void DisassemblePopReg16()
    {
        var instructions = Disassemble("58"); // POP AX
        Assert.Equal(Mnemonic.POP, instructions[0].Mnemonic);
        Assert.Equal("AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX = 0
    }

    [Fact]
    public void DisassemblePopReg16_BP()
    {
        var instructions = Disassemble("5D"); // POP BP
        Assert.Equal(Mnemonic.POP, instructions[0].Mnemonic);
        Assert.Equal("BP", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(5, instructions[0].Operand1.Value); // BP = 5
    }

    [Fact]
    public void DisassemblePushSegment()
    {
        var instructions = Disassemble("06"); // PUSH ES
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("ES", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // ES = 0
    }

    [Fact]
    public void DisassemblePopSegment()
    {
        var instructions = Disassemble("07"); // POP ES
        Assert.Equal(Mnemonic.POP, instructions[0].Mnemonic);
        Assert.Equal("ES", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // ES = 0
    }

    [Fact]
    public void DisassembleInt()
    {
        var instructions = Disassemble("CD 21"); // INT 21h
        Assert.Equal(Mnemonic.INT, instructions[0].Mnemonic);
        Assert.Equal("21h", instructions[0].Operands);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand1.Type);
        Assert.Equal(0x21, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleXchgAxReg()
    {
        var instructions = Disassemble("92"); // XCHG AX, DX
        Assert.Equal(Mnemonic.XCHG, instructions[0].Mnemonic);
        Assert.Equal("AX, DX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(2, instructions[0].Operand2.Value); // DX
    }

    [Fact]
    public void DisassembleXchgRegMem()
    {
        var instructions = Disassemble("87 07"); // XCHG AX, [BX]
        Assert.Equal(Mnemonic.XCHG, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand2.BaseReg);
    }

    [Fact]
    public void DisassembleIncReg()
    {
        var instructions = Disassemble("40"); // INC AX
        Assert.Equal(Mnemonic.INC, instructions[0].Mnemonic);
        Assert.Equal("AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
    }

    [Fact]
    public void DisassembleDecReg()
    {
        var instructions = Disassemble("4B"); // DEC BX
        Assert.Equal(Mnemonic.DEC, instructions[0].Mnemonic);
        Assert.Equal("BX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value); // BX
    }

    [Fact]
    public void DisassembleEnter()
    {
        var instructions = Disassemble("C8 10 00 02"); // ENTER 0010h, 02
        Assert.Equal(Mnemonic.ENTER, instructions[0].Mnemonic);
        Assert.Equal("10h, 2", instructions[0].Operands);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand1.Type);
        Assert.Equal(0x0010, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(2, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleAdcAlImm()
    {
        var instructions = Disassemble("14 05"); // ADC AL, 5
        Assert.Equal(Mnemonic.ADC, instructions[0].Mnemonic);
        Assert.Equal("AL, 5", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(5, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleSbbBxCx()
    {
        var instructions = Disassemble("19 CB"); // SBB BX, CX
        Assert.Equal(Mnemonic.SBB, instructions[0].Mnemonic);
        Assert.Equal("BX, CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleOrAxImm()
    {
        var instructions = Disassemble("0D FF 00"); // OR AX, 00FFh
        Assert.Equal(Mnemonic.OR, instructions[0].Mnemonic);
        Assert.Equal("AX, FFh", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0x00FF, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleXorBxAx()
    {
        var instructions = Disassemble("31 C3"); // XOR BX, AX
        Assert.Equal(Mnemonic.XOR, instructions[0].Mnemonic);
        Assert.Equal("BX, AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleTestAlImm()
    {
        var instructions = Disassemble("A8 01"); // TEST AL, 1
        Assert.Equal(Mnemonic.TEST, instructions[0].Mnemonic);
        Assert.Equal("AL, 1", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleJeShort()
    {
        var instructions = Disassemble("74 05"); // JE +5
        Assert.Equal(Mnemonic.JE, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleLoop()
    {
        var instructions = Disassemble("E2 05"); // LOOP +5
        Assert.Equal(Mnemonic.LOOP, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleMovsb()
    {
        var instructions = Disassemble("A4"); // MOVSB
        Assert.Equal(Mnemonic.MOVSB, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassemblePushf()
    {
        var instructions = Disassemble("9C"); // PUSHF
        Assert.Equal(Mnemonic.PUSHF, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleDaa()
    {
        var instructions = Disassemble("27"); // DAA
        Assert.Equal(Mnemonic.DAA, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCbw()
    {
        var instructions = Disassemble("98"); // CBW
        Assert.Equal(Mnemonic.CBW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleInAlImm()
    {
        var instructions = Disassemble("E4 21"); // IN AL, 21h
        Assert.Equal(Mnemonic.IN, instructions[0].Mnemonic);
        Assert.Equal("AL, 21h", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(0x21, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleOutImmAl()
    {
        var instructions = Disassemble("E6 21"); // OUT 21h, AL
        Assert.Equal(Mnemonic.OUT, instructions[0].Mnemonic);
        Assert.Contains("21h", instructions[0].Operands);
        Assert.Contains("AL", instructions[0].Operands);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand1.Type);
        Assert.Equal(0x21, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleLds()
    {
        var instructions = Disassemble("C5 1E 34 12"); // LDS BX, [1234]
        Assert.Equal(Mnemonic.LDS, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Contains("1234", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleXlat()
    {
        var instructions = Disassemble("D7"); // XLAT
        Assert.Equal(Mnemonic.XLAT, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleAaa()
    {
        var instructions = Disassemble("37"); // AAA
        Assert.Equal(Mnemonic.AAA, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleAad()
    {
        var instructions = Disassemble("D5 0A"); // AAD
        Assert.Equal(Mnemonic.AAD, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleAam()
    {
        var instructions = Disassemble("D4 0A"); // AAM
        Assert.Equal(Mnemonic.AAM, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleAas()
    {
        var instructions = Disassemble("3F"); // AAS
        Assert.Equal(Mnemonic.AAS, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleDas()
    {
        var instructions = Disassemble("2F"); // DAS
        Assert.Equal(Mnemonic.DAS, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCwd()
    {
        var instructions = Disassemble("99"); // CWD
        Assert.Equal(Mnemonic.CWD, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleDivReg()
    {
        var instructions = Disassemble("F7 F1"); // DIV CX
        Assert.Equal(Mnemonic.DIV, instructions[0].Mnemonic);
        Assert.Equal("CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(1, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleIdivReg()
    {
        var instructions = Disassemble("F7 F9"); // IDIV CX
        Assert.Equal(Mnemonic.IDIV, instructions[0].Mnemonic);
        Assert.Equal("CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(1, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleImulReg()
    {
        var instructions = Disassemble("F7 E9"); // IMUL CX
        Assert.Equal(Mnemonic.IMUL, instructions[0].Mnemonic);
        Assert.Equal("CX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(1, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleNegReg()
    {
        var instructions = Disassemble("F7 D8"); // NEG AX
        Assert.Equal(Mnemonic.NEG, instructions[0].Mnemonic);
        Assert.Equal("AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleNotReg()
    {
        var instructions = Disassemble("F7 D0"); // NOT AX
        Assert.Equal(Mnemonic.NOT, instructions[0].Mnemonic);
        Assert.Equal("AX", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleHlt()
    {
        var instructions = Disassemble("F4"); // HLT
        Assert.Equal(Mnemonic.HLT, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleInto()
    {
        var instructions = Disassemble("CE"); // INTO
        Assert.Equal(Mnemonic.INTO, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleIret()
    {
        var instructions = Disassemble("CF"); // IRET
        Assert.Equal(Mnemonic.IRET, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleRetf()
    {
        var instructions = Disassemble("CB"); // RETF
        Assert.Equal(Mnemonic.RETF, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleNop()
    {
        var instructions = Disassemble("90"); // NOP
        Assert.Equal(Mnemonic.NOP, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleLahf()
    {
        var instructions = Disassemble("9F"); // LAHF
        Assert.Equal(Mnemonic.LAHF, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleSahf()
    {
        var instructions = Disassemble("9E"); // SAHF
        Assert.Equal(Mnemonic.SAHF, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleClc()
    {
        var instructions = Disassemble("F8"); // CLC
        Assert.Equal(Mnemonic.CLC, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCld()
    {
        var instructions = Disassemble("FC"); // CLD
        Assert.Equal(Mnemonic.CLD, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCli()
    {
        var instructions = Disassemble("FA"); // CLI
        Assert.Equal(Mnemonic.CLI, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCmc()
    {
        var instructions = Disassemble("F5"); // CMC
        Assert.Equal(Mnemonic.CMC, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleStc()
    {
        var instructions = Disassemble("F9"); // STC
        Assert.Equal(Mnemonic.STC, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleStd()
    {
        var instructions = Disassemble("FD"); // STD
        Assert.Equal(Mnemonic.STD, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleSti()
    {
        var instructions = Disassemble("FB"); // STI
        Assert.Equal(Mnemonic.STI, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleCmpsw()
    {
        var instructions = Disassemble("A7"); // CMPSW
        Assert.Equal(Mnemonic.CMPSW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleLodsb()
    {
        var instructions = Disassemble("AC"); // LODSB
        Assert.Equal(Mnemonic.LODSB, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleStosb()
    {
        var instructions = Disassemble("AA"); // STOSB
        Assert.Equal(Mnemonic.STOSB, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleScasw()
    {
        var instructions = Disassemble("AF"); // SCASW
        Assert.Equal(Mnemonic.SCASW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
        Assert.Equal(OperandType.None, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.None, instructions[0].Operand2.Type);
    }

    [Fact]
    public void DisassembleRolRegCl()
    {
        var instructions = Disassemble("D3 C0"); // ROL AX, CL
        Assert.Equal(Mnemonic.ROL, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleRorRegCl()
    {
        var instructions = Disassemble("D3 C8"); // ROR AX, CL
        Assert.Equal(Mnemonic.ROR, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleRclRegCl()
    {
        var instructions = Disassemble("D3 D0"); // RCL AX, CL
        Assert.Equal(Mnemonic.RCL, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleRcrRegCl()
    {
        var instructions = Disassemble("D3 D8"); // RCR AX, CL
        Assert.Equal(Mnemonic.RCR, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleShrAxCl()
    {
        var instructions = Disassemble("D3 E8"); // SHR AX, CL
        Assert.Equal(Mnemonic.SHR, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleSalAxCl()
    {
        var instructions = Disassemble("D3 E0"); // SAL AX, CL (same as SHL)
        Assert.Equal(Mnemonic.SAL, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleSarAxCl()
    {
        var instructions = Disassemble("D3 F8"); // SAR AX, CL
        Assert.Equal(Mnemonic.SAR, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value); // AX
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value); // CL
    }

    [Fact]
    public void DisassembleJaShort()
    {
        var instructions = Disassemble("77 05"); // JA +5
        Assert.Equal(Mnemonic.JA, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleJbShort()
    {
        var instructions = Disassemble("72 05"); // JB +5
        Assert.Equal(Mnemonic.JB, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleJgShort()
    {
        var instructions = Disassemble("7F 05"); // JG +5
        Assert.Equal(Mnemonic.JG, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleJleShort()
    {
        var instructions = Disassemble("7E 05"); // JLE +5
        Assert.Equal(Mnemonic.JLE, instructions[0].Mnemonic);
        Assert.Equal("7", instructions[0].Operands);
        Assert.Equal(OperandType.Relative8, instructions[0].Operand1.Type);
        Assert.Equal(7, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleLes()
    {
        var instructions = Disassemble("C4 1E 34 12"); // LES BX, [1234]
        Assert.Equal(Mnemonic.LES, instructions[0].Mnemonic);
        Assert.Contains("BX", instructions[0].Operands);
        Assert.Contains("1234", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleGroup80AddBytePtrImm()
    {
        var instructions = Disassemble("80 07 05"); // ADD BYTE PTR [BX], 5
        Assert.Equal(Mnemonic.ADD, instructions[0].Mnemonic);
        Assert.Equal("[BX], 5", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(5, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleGroup81AddWordPtrImm()
    {
        var instructions = Disassemble("81 07 34 12"); // ADD WORD PTR [BX], 1234h
        Assert.Equal(Mnemonic.ADD, instructions[0].Mnemonic);
        Assert.Equal("[BX], 1234h", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleGroup83AddBytePtrImmSignExtend()
    {
        var instructions = Disassemble("83 07 FF"); // ADD WORD PTR [BX], -1 (sign extend)
        Assert.Equal(Mnemonic.ADD, instructions[0].Mnemonic);
        Assert.Equal("[BX], FFFFh", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0xFFFF, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovMemImmByte()
    {
        var instructions = Disassemble("C6 06 34 12 55"); // MOV BYTE PTR [1234h], 55h
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("[1234h], 55h", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(0x55, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovMemImmWord()
    {
        var instructions = Disassemble("C7 06 34 12 78 56"); // MOV WORD PTR [1234h], 5678h
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("[1234h], 5678h", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate16, instructions[0].Operand2.Type);
        Assert.Equal(0x5678, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovAxMemByte()
    {
        var instructions = Disassemble("A0 34 12"); // MOV AL, [1234h]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AL, [1234h]", instructions[0].Operands);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovAxMemWord()
    {
        var instructions = Disassemble("A1 34 12"); // MOV AX, [1234h]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AX, [1234h]", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(0x1234, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovMemAxByte()
    {
        var instructions = Disassemble("A2 34 12"); // MOV [1234h], AL
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("[1234h], AL", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovMemAxWord()
    {
        var instructions = Disassemble("A3 34 12"); // MOV [1234h], AX
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("[1234h], AX", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(0x1234, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovSregToReg()
    {
        var instructions = Disassemble("8C D8"); // MOV AX, DS
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("AX, DS", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand2.Type);
        Assert.Equal(3, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleMovRegToSreg()
    {
        var instructions = Disassemble("8E D8"); // MOV DS, AX
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal("DS, AX", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleTestModRmByte()
    {
        var instructions = Disassemble("84 07"); // TEST [BX], AL
        Assert.Equal(Mnemonic.TEST, instructions[0].Mnemonic);
        Assert.Equal("[BX], AL", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleTestModRmWord()
    {
        var instructions = Disassemble("85 07"); // TEST [BX], AX
        Assert.Equal(Mnemonic.TEST, instructions[0].Mnemonic);
        Assert.Equal("[BX], AX", instructions[0].Operands);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand1.BaseReg);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleCallNear()
    {
        var instructions = Disassemble("E8 05 00"); // CALL near +5 (target = 3 + 5 = 8)
        Assert.Equal(Mnemonic.CALL, instructions[0].Mnemonic);
        Assert.Equal("8", instructions[0].Operands);
        Assert.Equal(OperandType.Relative16, instructions[0].Operand1.Type);
        Assert.Equal(8, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleRetfFar()
    {
        var instructions = Disassemble("CA"); // RETF_FAR (CA)
        Assert.Equal(Mnemonic.RETF_FAR, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleStosw()
    {
        var instructions = Disassemble("AB"); // STOSW
        Assert.Equal(Mnemonic.STOSW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleLodsw()
    {
        var instructions = Disassemble("AD"); // LODSW
        Assert.Equal(Mnemonic.LODSW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleScasb()
    {
        var instructions = Disassemble("AE"); // SCASB
        Assert.Equal(Mnemonic.SCASB, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    [Fact]
    public void DisassemblePushCs()
    {
        var instructions = Disassemble("0E"); // PUSH CS
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("CS", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(1, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassemblePushSs()
    {
        var instructions = Disassemble("16"); // PUSH SS
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("SS", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(2, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassemblePushDs()
    {
        var instructions = Disassemble("1E"); // PUSH DS
        Assert.Equal(Mnemonic.PUSH, instructions[0].Mnemonic);
        Assert.Equal("DS", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassemblePopSs()
    {
        var instructions = Disassemble("17"); // POP SS
        Assert.Equal(Mnemonic.POP, instructions[0].Mnemonic);
        Assert.Equal("SS", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(2, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassemblePopDs()
    {
        var instructions = Disassemble("1F"); // POP DS
        Assert.Equal(Mnemonic.POP, instructions[0].Mnemonic);
        Assert.Equal("DS", instructions[0].Operands);
        Assert.Equal(OperandType.SegmentRegister, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
    }

    [Fact]
    public void DisassembleWithCsSegmentOverride()
    {
        var instructions = Disassemble("2E 8B 06 34 12"); // CS: MOV AX, [1234]
        Assert.Equal(Segment.CS, instructions[0].Segment);
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Contains("CS:", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleWithDsSegmentOverride()
    {
        var instructions = Disassemble("3E 8B 06 34 12"); // DS: MOV AX, [1234]
        Assert.Equal(Segment.DS, instructions[0].Segment);
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Contains("DS:", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleMemoryBxDi()
    {
        var instructions = Disassemble("8B 01"); // MOV AX, [BX+DI]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(AddressRegister.BX, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.DI, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleMemoryBpSi()
    {
        var instructions = Disassemble("8B 0A"); // MOV CX, [BP+SI]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(AddressRegister.BP, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.SI, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleMemoryDi()
    {
        var instructions = Disassemble("8B 0D"); // MOV CX, [DI]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(AddressRegister.DI, instructions[0].Operand2.BaseReg);
        Assert.Equal(AddressRegister.None, instructions[0].Operand2.IndexReg);
    }

    [Fact]
    public void DisassembleMemoryBpDisp()
    {
        var instructions = Disassemble("8B 4E 05"); // MOV CX, [BP+5]
        Assert.Equal(Mnemonic.MOV, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand2.Type);
        Assert.Equal(AddressRegister.BP, instructions[0].Operand2.BaseReg);
        Assert.Equal(5, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleGroupF6TestImm()
    {
        var instructions = Disassemble("F6 07 05"); // TEST BYTE PTR [BX], 5
        Assert.Equal(Mnemonic.TEST, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Memory, instructions[0].Operand1.Type);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(5, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleTestModRmReg()
    {
        var instructions = Disassemble("84 C3"); // TEST BL, AL
        Assert.Equal(Mnemonic.TEST, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value); // BL
        Assert.Equal(OperandType.Register8, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value); // AL
    }

    [Fact]
    public void DisassembleShiftImm1()
    {
        var instructions = Disassemble("D0 C0"); // ROL AL, 1
        Assert.Equal(Mnemonic.ROL, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Register8, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Immediate8, instructions[0].Operand2.Type);
        Assert.Equal(1, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleLeaRegReg()
    {
        var instructions = Disassemble("8D D8"); // LEA BX, AX
        Assert.Equal(Mnemonic.LEA, instructions[0].Mnemonic);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(3, instructions[0].Operand1.Value);
        Assert.Equal(OperandType.Register16, instructions[0].Operand2.Type);
        Assert.Equal(0, instructions[0].Operand2.Value);
    }

    [Fact]
    public void DisassembleSimpleDisassembleOverload()
    {
        var disassembler = new X86Disassembler("B8 34 12".FromHex());
        disassembler.Disassemble(0); // call the 1-param version to cover uncovered overload
        Assert.Single(disassembler.Instructions);
        Assert.Equal(Mnemonic.MOV, disassembler.Instructions[0].Mnemonic);
    }

    [Fact]
    public void DisassembleBranchTest()
    {
        var disassembler = new X86Disassembler("B8 34 12 C3".FromHex());
        var branch = disassembler.DisassembleBranch(0).ToList();
        Assert.Equal(2, branch.Count);
        Assert.Equal(Mnemonic.MOV, branch[0].Mnemonic);
        Assert.Equal(Mnemonic.RET, branch[1].Mnemonic);
    }

    [Fact]
    public void DisassembleIndirectJumpMemoryTarget()
    {
        // JMP WORD PTR [0004] where at [0004] is 0005 (target offset 5)
        var disassembler = new X86Disassembler("FF 26 04 00 90 90 05 00".FromHex());
        disassembler.DataSegmentBase = 0;
        disassembler.Disassemble(0);
        Assert.Equal(Mnemonic.JMP, disassembler.Instructions[0].Mnemonic);
        // Covers the memory indirect jump target resolution in GetEffectiveJumpTarget
    }
}