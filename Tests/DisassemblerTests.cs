using UltraDecompiler.Disassembler;

namespace Tests;

public class DisassemblerTests
{
    [Fact]
    public void DisassembleNearInderectCall()
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
        Assert.Equal(Mnemonic.JMP, instructions[0].Mnemonic);
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
    public void SegmentChange()
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
    }

    // Новые тесты на PUSH и POP с проверкой операндов
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

    // Новые тесты на INT, XCHG, INC, DEC
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

    // Новые тесты на недостающие инструкции 8086 (ADC, SBB, OR, XOR, TEST, SHL, условные переходы, LOOP, строковые, PUSHF, DAA, CBW, IN/OUT, LDS, XLAT)
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
    public void DisassembleShlAxCl()
    {
        var instructions = Disassemble("D3 E0"); // SHL AX, CL
        Assert.Equal(Mnemonic.SHL, instructions[0].Mnemonic);
        Assert.Equal("AX, CL", instructions[0].Operands);
        Assert.Equal(OperandType.Register16, instructions[0].Operand1.Type);
        Assert.Equal(0, instructions[0].Operand1.Value);
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
    }

    [Fact]
    public void DisassemblePushf()
    {
        var instructions = Disassemble("9C"); // PUSHF
        Assert.Equal(Mnemonic.PUSHF, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleDaa()
    {
        var instructions = Disassemble("27"); // DAA
        Assert.Equal(Mnemonic.DAA, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    [Fact]
    public void DisassembleCbw()
    {
        var instructions = Disassemble("98"); // CBW
        Assert.Equal(Mnemonic.CBW, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
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
    }

    [Fact]
    public void DisassembleXlat()
    {
        var instructions = Disassemble("D7"); // XLAT
        Assert.Equal(Mnemonic.XLAT, instructions[0].Mnemonic);
        Assert.Equal("", instructions[0].Operands);
    }

    private static List<Instruction> Disassemble(string hex)
    {
        var disassembler = new X86Disassembler(hex.FromHex());
        disassembler.Disassemble(0);
        return disassembler.Instructions;
    }
}