namespace UltraDecompiler.Disassembler;

public class X86Disassembler
{
    private readonly byte[] _image;
    private int _pos;
    private byte _segmentOverride;
    private HashSet<int> _visited = new();

    public int DataSegmentBase { get; set; }

    public X86Disassembler(byte[] image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
    }

    public List<Instruction> Instructions { get; private set; } = [];

    public void Disassemble(int startOffset)
    {
        _visited.Clear();
        Instructions.Clear();

        var queue = new Queue<int>();
        queue.Enqueue(startOffset);

        while (queue.Count > 0)
        {
            int offset = queue.Dequeue();

            if (_visited.Contains(offset) || offset >= _image.Length)
                continue;

            DisassembleBlock(offset, queue);
        }

        Instructions = Instructions.OrderBy(i => i.Offset).ToList();
    }

    private void DisassembleBlock(int startOffset, Queue<int> queue)
    {
        _pos = startOffset;
        _segmentOverride = 0;

        while (_pos < _image.Length)
        {
            if (_visited.Contains(_pos))
                break;

            _visited.Add(_pos);

            int instrStart = _pos;
            var instr = DecodeOneInstruction();
            instr.Offset = instrStart;
            instr.Bytes = _image[instrStart.._pos].ToArray();
            Instructions.Add(instr);
            _segmentOverride = 0;

            string mnem = instr.MnemonicString.ToUpper();

            if (mnem is "RET" or "RETF" or "IRET")
                break;

            if (mnem == "JMP")
            {
                int target = GetEffectiveJumpTarget(instr);
                if (target != -1)
                    queue.Enqueue(target);
                break;
            }

            if (mnem.StartsWith("J") || mnem == "CALL")
            {
                int target = GetEffectiveJumpTarget(instr);
                if (target != -1)
                    queue.Enqueue(target);
            }
        }
    }

    public int GetEffectiveJumpTarget(Instruction instr)
    {
        int direct = instr.GetJumpTarget();
        if (direct != -1)
            return direct;

        if ((instr.Mnemonic == Mnemonic.CALL || instr.Mnemonic == Mnemonic.JMP) && instr.OperandsInfo.Length > 0)
        {
            var op = instr.OperandsInfo[0];
            if (op.Type == OperandType.Memory)
            {
                int realAddr = DataSegmentBase + op.Value;
                if (realAddr >= 0 && realAddr + 2 <= _image.Length)
                {
                    return (ushort)(_image[realAddr] | (_image[realAddr + 1] << 8));
                }
            }
        }

        return -1;
    }

    private Instruction DecodeOneInstruction()
    {
        byte opcode = ReadByte();

        if (opcode == 0xF0)
        {
            var instr = DecodeOneInstruction();
            instr.Mnemonic = Mnemonic.LOCK;
            return instr;
        }

        switch (opcode)
        {
            case 0x26: _segmentOverride = 0x26; return DecodeOneInstruction();
            case 0x2E: _segmentOverride = 0x2E; return DecodeOneInstruction();
            case 0x36: _segmentOverride = 0x36; return DecodeOneInstruction();
            case 0x3E: _segmentOverride = 0x3E; return DecodeOneInstruction();
        }

        switch (opcode)
        {
            case 0x00: case 0x01: case 0x02: case 0x03:
            case 0x08: case 0x09: case 0x0A: case 0x0B:
            case 0x20: case 0x21: case 0x22: case 0x23:
            case 0x28: case 0x29: case 0x2A: case 0x2B:
            case 0x30: case 0x31: case 0x32: case 0x33:
            case 0x38: case 0x39: case 0x3A: case 0x3B:
                return DecodeModRmAlu(opcode);

            case 0x04: case 0x05: case 0x0C: case 0x0D:
            case 0x14: case 0x15: case 0x1C: case 0x1D:
            case 0x24: case 0x25: case 0x2C: case 0x2D:
            case 0x34: case 0x35: case 0x3C: case 0x3D:
                return DecodeAluImmAx(opcode);

            case 0x80: case 0x81: case 0x82: case 0x83:
                return DecodeGroup80(opcode);

            case 0xF6: case 0xF7:
                return DecodeGroupF6(opcode);

            case 0xFE: case 0xFF:
                return DecodeGroupFEFF(opcode);

            case 0x88: case 0x89: case 0x8A: case 0x8B:
                return DecodeMovRegMem(opcode);

            case 0x8C: case 0x8E:
                return DecodeMovSreg(opcode);

            case 0xA0: case 0xA1: case 0xA2: case 0xA3:
                return DecodeMovAxMem(opcode);

            case 0xB0: case 0xB1: case 0xB2: case 0xB3:
            case 0xB4: case 0xB5: case 0xB6: case 0xB7:
            case 0xB8: case 0xB9: case 0xBA: case 0xBB:
            case 0xBC: case 0xBD: case 0xBE: case 0xBF:
                return DecodeMovRegImm(opcode);

            case 0xC6: case 0xC7:
                return DecodeMovMemImm(opcode);

            case 0x50: case 0x51: case 0x52: case 0x53:
            case 0x54: case 0x55: case 0x56: case 0x57:
                return new Instruction { Mnemonic = Mnemonic.PUSH, Operands = GetReg16Name(opcode - 0x50) };
            case 0x58: case 0x59: case 0x5A: case 0x5B:
            case 0x5C: case 0x5D: case 0x5E: case 0x5F:
                return new Instruction { Mnemonic = Mnemonic.POP, Operands = GetReg16Name(opcode - 0x58) };

            case 0x06: return new Instruction { Mnemonic = Mnemonic.PUSH, Operands = "ES" };
            case 0x0E: return new Instruction { Mnemonic = Mnemonic.PUSH, Operands = "CS" };
            case 0x16: return new Instruction { Mnemonic = Mnemonic.PUSH, Operands = "SS" };
            case 0x1E: return new Instruction { Mnemonic = Mnemonic.PUSH, Operands = "DS" };
            case 0x07: return new Instruction { Mnemonic = Mnemonic.POP, Operands = "ES" };
            case 0x17: return new Instruction { Mnemonic = Mnemonic.POP, Operands = "SS" };
            case 0x1F: return new Instruction { Mnemonic = Mnemonic.POP, Operands = "DS" };

            case 0x70: case 0x71: case 0x72: case 0x73:
            case 0x74: case 0x75: case 0x76: case 0x77:
            case 0x78: case 0x79: case 0x7A: case 0x7B:
            case 0x7C: case 0x7D: case 0x7E: case 0x7F:
            case 0xEB:
            case 0xE3:
                return DecodeShortJump(opcode);

            case 0xE9: return DecodeNearJump();

            case 0xE8:
                short rel = (short)ReadUInt16();
                var callInstr = new Instruction { Mnemonic = Mnemonic.CALL, Operands = $"0x{(_pos + rel):X4}" };
                callInstr.OperandsInfo = new[] { new Operand(OperandType.Relative16, _pos + rel) };
                return callInstr;

            case 0xC3: return new Instruction { Mnemonic = Mnemonic.RET };
            case 0xCB: return new Instruction { Mnemonic = Mnemonic.RETF };

            case 0xCD:
                byte intNum = ReadByte();
                return new Instruction { Mnemonic = Mnemonic.INT, Operands = $"0x{intNum:X2}" };

            case 0x90: return new Instruction { Mnemonic = Mnemonic.NOP };

            case 0x86: case 0x87:
                return DecodeXchg(opcode);
            case 0x91: case 0x92: case 0x93: case 0x94: case 0x95: case 0x96: case 0x97:
                return new Instruction { Mnemonic = Mnemonic.XCHG, Operands = $"AX, {GetReg16Name(opcode - 0x90)}" };

            case 0x40: case 0x41: case 0x42: case 0x43:
            case 0x44: case 0x45: case 0x46: case 0x47:
                return new Instruction { Mnemonic = Mnemonic.INC, Operands = GetReg16Name(opcode - 0x40) };
            case 0x48: case 0x49: case 0x4A: case 0x4B:
            case 0x4C: case 0x4D: case 0x4E: case 0x4F:
                return new Instruction { Mnemonic = Mnemonic.DEC, Operands = GetReg16Name(opcode - 0x48) };

            case 0x8D: return DecodeLea();

            case 0x84: case 0x85: return DecodeTestModRm(opcode);
            case 0xA8: case 0xA9: return DecodeTestAxImm(opcode);

            case 0xA4: return new Instruction { Mnemonic = Mnemonic.MOVSB };
            case 0xA5: return new Instruction { Mnemonic = Mnemonic.MOVSW };
            case 0xA6: return new Instruction { Mnemonic = Mnemonic.CMPSB };
            case 0xA7: return new Instruction { Mnemonic = Mnemonic.CMPSW };
            case 0xAA: return new Instruction { Mnemonic = Mnemonic.STOSB };
            case 0xAB: return new Instruction { Mnemonic = Mnemonic.STOSW };
            case 0xAC: return new Instruction { Mnemonic = Mnemonic.LODSB };
            case 0xAD: return new Instruction { Mnemonic = Mnemonic.LODSW };
            case 0xAE: return new Instruction { Mnemonic = Mnemonic.SCASB };
            case 0xAF: return new Instruction { Mnemonic = Mnemonic.SCASW };

            case 0xF2: return new Instruction { Mnemonic = Mnemonic.REPNZ };
            case 0xF3: return new Instruction { Mnemonic = Mnemonic.REPZ };

            case 0xD0: case 0xD1: case 0xD2: case 0xD3:
                return DecodeShift(opcode);

            case 0x98: return new Instruction { Mnemonic = Mnemonic.CBW };
            case 0x99: return new Instruction { Mnemonic = Mnemonic.CWD };

            case 0xE0: case 0xE1: case 0xE2:
                return DecodeLoop(opcode);

            case 0x27: return new Instruction { Mnemonic = Mnemonic.DAA };
            case 0x2F: return new Instruction { Mnemonic = Mnemonic.DAS };
            case 0x37: return new Instruction { Mnemonic = Mnemonic.AAA };
            case 0x3F: return new Instruction { Mnemonic = Mnemonic.AAS };

            case 0xD4: return DecodeAam();
            case 0xD5: return DecodeAad();

            case 0x9C: return new Instruction { Mnemonic = Mnemonic.PUSHF };
            case 0x9D: return new Instruction { Mnemonic = Mnemonic.POPF };
            case 0x9E: return new Instruction { Mnemonic = Mnemonic.SAHF };
            case 0x9F: return new Instruction { Mnemonic = Mnemonic.LAHF };

            case 0xFA: return new Instruction { Mnemonic = Mnemonic.CLI };
            case 0xFB: return new Instruction { Mnemonic = Mnemonic.STI };
            case 0xFC: return new Instruction { Mnemonic = Mnemonic.CLD };
            case 0xFD: return new Instruction { Mnemonic = Mnemonic.STD };

            case 0xD7: return new Instruction { Mnemonic = Mnemonic.XLAT };

            case 0xF4: return new Instruction { Mnemonic = Mnemonic.HLT };

            case 0xC5: return DecodeLds();

            case 0xC8: return DecodeEnter();
            case 0xC9: return new Instruction { Mnemonic = Mnemonic.LEAVE };

            case 0x69: case 0x6B: case 0x0F:
                if (opcode == 0x0F)
                {
                    byte next = ReadByte();
                    if (next == 0xAF) return DecodeImulTwoOperand();
                    return new Instruction { Mnemonic = Mnemonic.DB, Operands = Instruction.UnknownOperand };
                }
                return DecodeImulTwoOperand();

            default:
                return new Instruction { Mnemonic = Mnemonic.DB, Operands = Instruction.UnknownOperand };
        }
    }

    private Instruction DecodeLds()
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;

        string regName = GetReg16Name(reg);
        string mem = (mod == 3) ? GetReg16Name(rm) : GetMemoryOperand(rm, mod);

        return new Instruction { Mnemonic = Mnemonic.LDS, Operands = $"{regName}, {mem}" };
    }

    private Instruction DecodeAam()
    {
        byte baseVal = ReadByte();
        return baseVal == 0x0A ? new Instruction { Mnemonic = Mnemonic.AAM } : new Instruction { Mnemonic = Mnemonic.AAM, Operands = "; non-standard" };
    }

    private Instruction DecodeAad()
    {
        byte baseVal = ReadByte();
        return baseVal == 0x0A ? new Instruction { Mnemonic = Mnemonic.AAD } : new Instruction { Mnemonic = Mnemonic.AAD, Operands = "; non-standard" };
    }

    private Instruction DecodeEnter()
    {
        ushort alloc = ReadUInt16();
        byte level = ReadByte();
        return new Instruction { Mnemonic = Mnemonic.ENTER, Operands = $"0x{alloc:X4}, {level}" };
    }

    private Instruction DecodeImulTwoOperand()
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;

        string dst = GetReg16Name(reg);
        string src = (mod == 3) ? GetReg16Name(rm) : GetMemoryOperand(rm, mod);

        if (_pos > 0 && _image[_pos - 3] == 0x6B)
        {
            sbyte imm8 = (sbyte)ReadByte();
            return new Instruction { Mnemonic = Mnemonic.IMUL, Operands = $"{dst}, {src}, {imm8}" };
        }

        ushort imm16 = ReadUInt16();
        return new Instruction { Mnemonic = Mnemonic.IMUL, Operands = $"{dst}, {src}, 0x{imm16:X4}" };
    }

    private Instruction DecodeModRmAlu(byte opcode)
    {
        byte modrm = ReadByte();
        Mnemonic op = GetAluMnemonicEnum(opcode);
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        string dst = (mod == 3) ? GetReg8or16Name(rm, word) : GetMemoryOperand(rm, mod);
        string src = GetReg8or16Name(reg, word);

        if ((opcode & 2) != 0) (dst, src) = (src, dst);
        return new Instruction { Mnemonic = op, Operands = $"{dst}, {src}" };
    }

    private Instruction DecodeAluImmAx(byte opcode)
    {
        Mnemonic op = GetAluMnemonicEnum(opcode);
        bool word = (opcode & 1) == 1;
        ushort imm = word ? ReadUInt16() : ReadByte();
        return new Instruction { Mnemonic = op, Operands = $"{(word ? "AX" : "AL")}, 0x{imm:X4}" };
    }

    private Instruction DecodeGroup80(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int regField = (modrm >> 3) & 7;
        bool word = (opcode & 1) == 1;
        bool signExtend = (opcode == 0x83);

        Mnemonic op = regField switch
        {
            0 => Mnemonic.ADD, 1 => Mnemonic.OR, 2 => Mnemonic.ADC, 3 => Mnemonic.SBB,
            4 => Mnemonic.AND, 5 => Mnemonic.SUB, 6 => Mnemonic.XOR, 7 => Mnemonic.CMP, _ => Mnemonic.DB
        };

        string dst = (mod == 3) ? GetReg8or16Name(modrm & 7, word) : GetMemoryOperand(modrm & 7, mod);
        ushort imm = signExtend ? (ushort)(sbyte)ReadByte() : (word ? ReadUInt16() : ReadByte());

        return new Instruction { Mnemonic = op, Operands = $"{dst}, 0x{imm:X4}" };
    }

    private Instruction DecodeGroupF6(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int regField = (modrm >> 3) & 7;
        bool word = (opcode & 1) == 1;

        string dst = (mod == 3) ? GetReg8or16Name(modrm & 7, word) : GetMemoryOperand(modrm & 7, mod);

        Mnemonic op = regField switch
        {
            0 => Mnemonic.TEST, 2 => Mnemonic.NOT, 3 => Mnemonic.NEG,
            4 => Mnemonic.MUL, 5 => Mnemonic.IMUL, 6 => Mnemonic.DIV, 7 => Mnemonic.IDIV,
            _ => Mnemonic.DB
        };

        if (regField == 0)
        {
            ushort imm = word ? ReadUInt16() : ReadByte();
            return new Instruction { Mnemonic = Mnemonic.TEST, Operands = $"{dst}, 0x{imm:X4}" };
        }

        return new Instruction { Mnemonic = op, Operands = dst };
    }

    private Instruction DecodeGroupFEFF(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int regField = (modrm >> 3) & 7;
        bool word = (opcode & 1) == 1;

        ushort disp = 0;
        bool hasDisp = false;

        if (mod != 3)
        {
            if (mod == 1) { disp = ReadByte(); hasDisp = true; }
            else if (mod == 2) { disp = ReadUInt16(); hasDisp = true; }
            else if (mod == 0 && (modrm & 7) == 6) { disp = ReadUInt16(); hasDisp = true; }
        }

        string dst;
        if (mod == 3)
        {
            dst = GetReg8or16Name(modrm & 7, word);
        }
        else if (hasDisp)
        {
            string seg = _segmentOverride switch
            {
                0x26 => "ES:", 0x2E => "CS:", 0x36 => "SS:", 0x3E => "DS:", _ => "DS:"
            };

            string baseReg = (modrm & 7) switch
            {
                0 => "BX+SI", 1 => "BX+DI", 2 => "BP+SI", 3 => "BP+DI",
                4 => "SI",    5 => "DI",    6 => "BP",
                7 => "BX", _ => "?"
            };

            if (mod == 0 && (modrm & 7) == 6)
                dst = $"{seg}0x{disp:X4}";
            else if (mod == 1)
                dst = $"{seg}{baseReg}+{disp}";
            else
                dst = $"{seg}{baseReg}+0x{disp:X4}";
        }
        else
        {
            dst = GetMemoryOperand(modrm & 7, mod);
        }

        if (opcode == 0xFE)
        {
            Mnemonic op8 = regField switch { 0 => Mnemonic.INC, 1 => Mnemonic.DEC, _ => Mnemonic.DB };
            return new Instruction { Mnemonic = op8, Operands = dst };
        }

        if (regField == 2)
        {
            var instr = new Instruction { Mnemonic = Mnemonic.CALL, Operands = dst };
            if (hasDisp) instr.OperandsInfo = new[] { new Operand(OperandType.Memory, disp) };
            return instr;
        }

        if (regField == 4)
        {
            var instr = new Instruction { Mnemonic = Mnemonic.JMP, Operands = dst };
            if (hasDisp) instr.OperandsInfo = new[] { new Operand(OperandType.Memory, disp) };
            return instr;
        }

        Mnemonic op = regField switch
        {
            0 => Mnemonic.INC, 1 => Mnemonic.DEC, 2 => Mnemonic.CALL, 3 => Mnemonic.CALL,
            4 => Mnemonic.JMP, 5 => Mnemonic.JMP, 6 => Mnemonic.PUSH, _ => Mnemonic.DB
        };

        return new Instruction { Mnemonic = op, Operands = dst };
    }

    private Instruction DecodeMovRegMem(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        string regName = GetReg8or16Name(reg, word);
        string mem = (mod == 3) ? GetReg8or16Name(rm, word) : GetMemoryOperand(rm, mod);

        if ((opcode & 2) != 0)
            return new Instruction { Mnemonic = Mnemonic.MOV, Operands = $"{regName}, {mem}" };
        else
            return new Instruction { Mnemonic = Mnemonic.MOV, Operands = $"{mem}, {regName}" };
    }

    private Instruction DecodeMovRegImm(byte opcode)
    {
        bool word = opcode >= 0xB8;
        string reg = GetReg8or16Name(opcode - (word ? 0xB8 : 0xB0), word);
        ushort imm = word ? ReadUInt16() : ReadByte();
        return new Instruction { Mnemonic = Mnemonic.MOV, Operands = $"{reg}, 0x{imm:X4}" };
    }

    private Instruction DecodeMovMemImm(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        string dst = (mod == 3) ? GetReg8or16Name(rm, word) : GetMemoryOperand(rm, mod);
        ushort imm = word ? ReadUInt16() : ReadByte();

        return new Instruction { Mnemonic = Mnemonic.MOV, Operands = $"{dst}, 0x{imm:X4}" };
    }

    private Instruction DecodeMovAxMem(byte opcode)
    {
        ushort disp = ReadUInt16();
        string seg = _segmentOverride switch
        {
            0x26 => "ES:", 0x2E => "CS:", 0x36 => "SS:", 0x3E => "DS:", _ => "DS:"
        };

        string addr = $"{seg}0x{disp:X4}";

        if (opcode == 0xA0) return new Instruction { Mnemonic = Mnemonic.MOV, Operands = $"AL, {addr}" };
        if (opcode == 0xA1) return new Instruction { Mnemonic = Mnemonic.MOV, Operands = $"AX, {addr}" };
        if (opcode == 0xA2) return new Instruction { Mnemonic = Mnemonic.MOV, Operands = $"{addr}, AL" };
        if (opcode == 0xA3) return new Instruction { Mnemonic = Mnemonic.MOV, Operands = $"{addr}, AX" };

        return new Instruction { Mnemonic = Mnemonic.MOV, Operands = addr };
    }

    private Instruction DecodeMovSreg(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int sreg = (modrm >> 3) & 7;
        int rm = modrm & 7;

        string sregName = sreg switch
        {
            0 => "ES", 1 => "CS", 2 => "SS", 3 => "DS",
            _ => "?SREG"
        };

        string src = (mod == 3) ? GetReg16Name(rm) : GetMemoryOperand(rm, mod);

        if (opcode == 0x8C)
            return new Instruction { Mnemonic = Mnemonic.MOV, Operands = $"{src}, {sregName}" };
        else
            return new Instruction { Mnemonic = Mnemonic.MOV, Operands = $"{sregName}, {src}" };
    }

    private Instruction DecodeShortJump(byte opcode)
    {
        sbyte rel = (sbyte)ReadByte();
        int target = _pos + rel;

        Mnemonic mnem = opcode switch
        {
            0x70 => Mnemonic.JO, 0x71 => Mnemonic.JNO, 0x72 => Mnemonic.JB, 0x73 => Mnemonic.JAE,
            0x74 => Mnemonic.JE, 0x75 => Mnemonic.JNE, 0x76 => Mnemonic.JBE, 0x77 => Mnemonic.JA,
            0x78 => Mnemonic.JS, 0x79 => Mnemonic.JNS, 0x7A => Mnemonic.JP, 0x7B => Mnemonic.JNP,
            0x7C => Mnemonic.JL, 0x7D => Mnemonic.JGE, 0x7E => Mnemonic.JLE, 0x7F => Mnemonic.JG,
            0xE3 => Mnemonic.JCXZ,
            0xEB => Mnemonic.JMP,
            _ => Mnemonic.DB
        };

        var instr = new Instruction
        {
            Mnemonic = mnem,
            Operands = $"0x{target:X4}",
            OperandsInfo = new[] { new Operand(OperandType.Relative8, target) }
        };
        return instr;
    }

    private Instruction DecodeNearJump()
    {
        short rel = (short)ReadUInt16();
        int target = _pos + rel;

        var instr = new Instruction
        {
            Mnemonic = Mnemonic.JMP,
            Operands = $"0x{target:X4}",
            OperandsInfo = new[] { new Operand(OperandType.Relative16, target) }
        };
        return instr;
    }

    private Instruction DecodeXchg(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        string r1 = GetReg8or16Name(reg, word);
        string r2 = (mod == 3) ? GetReg8or16Name(rm, word) : GetMemoryOperand(rm, mod);
        return new Instruction { Mnemonic = Mnemonic.XCHG, Operands = $"{r1}, {r2}" };
    }

    private Instruction DecodeLea()
    {
        byte modrm = ReadByte();
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        int mod = (modrm >> 6) & 3;
        string mem = GetMemoryOperand(rm, mod);
        return new Instruction { Mnemonic = Mnemonic.LEA, Operands = $"{GetReg16Name(reg)}, {mem}" };
    }

    private Instruction DecodeTestModRm(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        string r1 = GetReg8or16Name(reg, word);
        string r2 = (mod == 3) ? GetReg8or16Name(rm, word) : GetMemoryOperand(rm, mod);
        return new Instruction { Mnemonic = Mnemonic.TEST, Operands = $"{r1}, {r2}" };
    }

    private Instruction DecodeTestAxImm(byte opcode)
    {
        bool word = (opcode & 1) == 1;
        ushort imm = word ? ReadUInt16() : ReadByte();
        return new Instruction { Mnemonic = Mnemonic.TEST, Operands = $"{(word ? "AX" : "AL")}, 0x{imm:X4}" };
    }

    private Instruction DecodeShift(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int rm = modrm & 7;
        int regField = (modrm >> 3) & 7;
        bool word = (opcode & 1) == 1;
        bool useCl = (opcode & 2) != 0;

        Mnemonic op = regField switch { 0 => Mnemonic.ROL, 1 => Mnemonic.ROR, 2 => Mnemonic.RCL, 3 => Mnemonic.RCR, 4 => Mnemonic.SHL, 5 => Mnemonic.SHR, 7 => Mnemonic.SAR, _ => Mnemonic.DB };
        string dst = (mod == 3) ? GetReg8or16Name(rm, word) : GetMemoryOperand(rm, mod);
        string count = useCl ? "CL" : "1";
        return new Instruction { Mnemonic = op, Operands = $"{dst}, {count}" };
    }

    private Instruction DecodeLoop(byte opcode)
    {
        sbyte rel = (sbyte)ReadByte();
        int target = _pos + rel;
        Mnemonic mnem = opcode switch { 0xE0 => Mnemonic.LOOPNE, 0xE1 => Mnemonic.LOOPE, 0xE2 => Mnemonic.LOOP, _ => Mnemonic.LOOP };

        var instr = new Instruction
        {
            Mnemonic = mnem,
            Operands = $"0x{target:X4}",
            OperandsInfo = new[] { new Operand(OperandType.Relative8, target) }
        };
        return instr;
    }

    private Mnemonic GetAluMnemonicEnum(byte opcode)
    {
        return (opcode >> 3) switch
        {
            0 => Mnemonic.ADD, 1 => Mnemonic.OR, 2 => Mnemonic.ADC, 3 => Mnemonic.SBB,
            4 => Mnemonic.AND, 5 => Mnemonic.SUB, 6 => Mnemonic.XOR, 7 => Mnemonic.CMP, _ => Mnemonic.DB
        };
    }

    private string GetMemoryOperand(int rm, int mod)
    {
        string seg = _segmentOverride switch
        {
            0x26 => "ES:", 0x2E => "CS:", 0x36 => "SS:", 0x3E => "DS:", _ => "DS:"
        };

        if (mod == 0 && rm == 6)
        {
            ushort disp = ReadUInt16();
            return $"{seg}0x{disp:X4}";
        }

        string baseReg = rm switch
        {
            0 => "BX+SI", 1 => "BX+DI", 2 => "BP+SI", 3 => "BP+DI",
            4 => "SI",    5 => "DI",    6 => "BP",
            7 => "BX", _ => "?"
        };

        if (mod == 1) return $"{seg}{baseReg}+{ReadByte()}";
        if (mod == 2) return $"{seg}{baseReg}+{ReadUInt16()}";

        return $"{seg}{baseReg}";
    }

    private string GetReg8or16Name(int reg, bool word)
    {
        if (word) return GetReg16Name(reg);
        return reg switch
        {
            0 => "AL", 1 => "CL", 2 => "DL", 3 => "BL",
            4 => "AH", 5 => "CH", 6 => "DH", 7 => "BH", _ => "?"
        };
    }

    private string GetReg16Name(int reg) => reg switch
    {
        0 => "AX", 1 => "CX", 2 => "DX", 3 => "BX",
        4 => "SP", 5 => "BP", 6 => "SI", 7 => "DI", _ => "?"
    };

    private byte ReadByte() => _image[_pos++];
    private ushort ReadUInt16() => (ushort)(_image[_pos++] | (_image[_pos++] << 8));
}