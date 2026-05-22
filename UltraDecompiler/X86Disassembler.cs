using System;
using System.Collections.Generic;
using System.Linq;

namespace UltraDecompiler;

public class X86Disassembler
{
    private readonly byte[] _image;
    private int _pos;
    private byte _segmentOverride;

    public X86Disassembler(byte[] image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
    }

    public List<Instruction> Disassemble(int startOffset, int maxInstructions = 300)
    {
        var result = new List<Instruction>();
        _pos = startOffset;
        _segmentOverride = 0;

        for (int i = 0; i < maxInstructions && _pos < _image.Length; i++)
        {
            int instrStart = _pos;
            var instr = DecodeOneInstruction();
            instr.Offset = instrStart;
            instr.Bytes = _image[instrStart.._pos].ToArray();
            result.Add(instr);

            if (instr.Mnemonic is "RET" or "RETF" or "IRET")
                break;
        }
        return result;
    }

    private Instruction DecodeOneInstruction()
    {
        byte opcode = ReadByte();

        if (opcode == 0xF0)
        {
            var instr = DecodeOneInstruction();
            instr.Mnemonic = "LOCK " + instr.Mnemonic;
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
                return new Instruction { Mnemonic = "PUSH", Operands = GetReg16Name(opcode - 0x50) };
            case 0x58: case 0x59: case 0x5A: case 0x5B:
            case 0x5C: case 0x5D: case 0x5E: case 0x5F:
                return new Instruction { Mnemonic = "POP", Operands = GetReg16Name(opcode - 0x58) };

            case 0x06: return new Instruction { Mnemonic = "PUSH", Operands = "ES" };
            case 0x0E: return new Instruction { Mnemonic = "PUSH", Operands = "CS" };
            case 0x16: return new Instruction { Mnemonic = "PUSH", Operands = "SS" };
            case 0x1E: return new Instruction { Mnemonic = "PUSH", Operands = "DS" };
            case 0x07: return new Instruction { Mnemonic = "POP", Operands = "ES" };
            case 0x17: return new Instruction { Mnemonic = "POP", Operands = "SS" };
            case 0x1F: return new Instruction { Mnemonic = "POP", Operands = "DS" };

            case 0x70: case 0x71: case 0x72: case 0x73:
            case 0x74: case 0x75: case 0x76: case 0x77:
            case 0x78: case 0x79: case 0x7A: case 0x7B:
            case 0x7C: case 0x7D: case 0x7E: case 0x7F:
            case 0xEB:
                return DecodeShortJump(opcode);

            case 0xE9: return DecodeNearJump();

            case 0xE8:
                short rel = (short)ReadUInt16();
                return new Instruction { Mnemonic = "CALL", Operands = $"0x{(_pos + rel):X4}" };

            case 0xC3: return new Instruction { Mnemonic = "RET" };
            case 0xCB: return new Instruction { Mnemonic = "RETF" };

            case 0xCD:
                byte intNum = ReadByte();
                return new Instruction { Mnemonic = "INT", Operands = $"0x{intNum:X2}" };

            case 0x90: return new Instruction { Mnemonic = "NOP" };

            case 0x86: case 0x87:
                return DecodeXchg(opcode);
            case 0x91: case 0x92: case 0x93: case 0x94: case 0x95: case 0x96: case 0x97:
                return new Instruction { Mnemonic = "XCHG", Operands = $"AX, {GetReg16Name(opcode - 0x90)}" };

            case 0x40: case 0x41: case 0x42: case 0x43:
            case 0x44: case 0x45: case 0x46: case 0x47:
                return new Instruction { Mnemonic = "INC", Operands = GetReg16Name(opcode - 0x40) };
            case 0x48: case 0x49: case 0x4A: case 0x4B:
            case 0x4C: case 0x4D: case 0x4E: case 0x4F:
                return new Instruction { Mnemonic = "DEC", Operands = GetReg16Name(opcode - 0x48) };

            case 0x8D: return DecodeLea();

            case 0x84: case 0x85: return DecodeTestModRm(opcode);
            case 0xA8: case 0xA9: return DecodeTestAxImm(opcode);

            case 0xA4: return new Instruction { Mnemonic = "MOVSB" };
            case 0xA5: return new Instruction { Mnemonic = "MOVSW" };
            case 0xA6: return new Instruction { Mnemonic = "CMPSB" };
            case 0xA7: return new Instruction { Mnemonic = "CMPSW" };
            case 0xAA: return new Instruction { Mnemonic = "STOSB" };
            case 0xAB: return new Instruction { Mnemonic = "STOSW" };
            case 0xAC: return new Instruction { Mnemonic = "LODSB" };
            case 0xAD: return new Instruction { Mnemonic = "LODSW" };
            case 0xAE: return new Instruction { Mnemonic = "SCASB" };
            case 0xAF: return new Instruction { Mnemonic = "SCASW" };

            case 0xF2: return new Instruction { Mnemonic = "REPNZ" };
            case 0xF3: return new Instruction { Mnemonic = "REPZ" };

            case 0xD0: case 0xD1: case 0xD2: case 0xD3:
                return DecodeShift(opcode);

            case 0x98: return new Instruction { Mnemonic = "CBW" };
            case 0x99: return new Instruction { Mnemonic = "CWD" };

            case 0xE0: case 0xE1: case 0xE2:
                return DecodeLoop(opcode);

            case 0x27: return new Instruction { Mnemonic = "DAA" };
            case 0x2F: return new Instruction { Mnemonic = "DAS" };
            case 0x37: return new Instruction { Mnemonic = "AAA" };
            case 0x3F: return new Instruction { Mnemonic = "AAS" };

            case 0xD4: return DecodeAam();
            case 0xD5: return DecodeAad();

            case 0x9C: return new Instruction { Mnemonic = "PUSHF" };
            case 0x9D: return new Instruction { Mnemonic = "POPF" };
            case 0x9E: return new Instruction { Mnemonic = "SAHF" };
            case 0x9F: return new Instruction { Mnemonic = "LAHF" };

            case 0xFA: return new Instruction { Mnemonic = "CLI" };
            case 0xFB: return new Instruction { Mnemonic = "STI" };
            case 0xFC: return new Instruction { Mnemonic = "CLD" };
            case 0xFD: return new Instruction { Mnemonic = "STD" };

            case 0xD7: return new Instruction { Mnemonic = "XLAT" };

            case 0xF4: return new Instruction { Mnemonic = "HLT" };

            // 80286 instructions
            case 0xC8: return DecodeEnter();
            case 0xC9: return new Instruction { Mnemonic = "LEAVE" };

            // Two/three operand IMUL (80286+)
            case 0x69: case 0x6B: case 0x0F:
                if (opcode == 0x0F)
                {
                    byte next = ReadByte();
                    if (next == 0xAF) return DecodeImulTwoOperand();
                    return new Instruction { Mnemonic = $"DB 0F {next:X2}", Operands = "; unknown" };
                }
                return DecodeImulTwoOperand();

            default:
                return new Instruction { Mnemonic = $"DB 0x{opcode:X2}", Operands = "; unknown" };
        }
    }

    private Instruction DecodeAam()
    {
        byte baseVal = ReadByte();
        return baseVal == 0x0A ? new Instruction { Mnemonic = "AAM" } : new Instruction { Mnemonic = $"AAM {baseVal}", Operands = "; non-standard" };
    }

    private Instruction DecodeAad()
    {
        byte baseVal = ReadByte();
        return baseVal == 0x0A ? new Instruction { Mnemonic = "AAD" } : new Instruction { Mnemonic = $"AAD {baseVal}", Operands = "; non-standard" };
    }

    private Instruction DecodeEnter()
    {
        ushort alloc = ReadUInt16();
        byte level = ReadByte();
        return new Instruction { Mnemonic = "ENTER", Operands = $"0x{alloc:X4}, {level}" };
    }

    private Instruction DecodeImulTwoOperand()
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;

        string dst = GetReg16Name(reg);
        string src = (mod == 3) ? GetReg16Name(rm) : GetMemoryOperand(rm, mod);

        // For 0x6B (sign-extended imm8)
        if (_pos > 0 && _image[_pos - 3] == 0x6B)
        {
            sbyte imm = (sbyte)ReadByte();
            return new Instruction { Mnemonic = "IMUL", Operands = $"{dst}, {src}, {imm}" };
        }
        else
        {
            // For 0x69 (imm16)
            ushort imm = ReadUInt16();
            return new Instruction { Mnemonic = "IMUL", Operands = $"{dst}, {src}, 0x{imm:X4}" };
        }
    }

    private Instruction DecodeModRmAlu(byte opcode)
    {
        byte modrm = ReadByte();
        string op = GetAluMnemonic(opcode);
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
        string op = GetAluMnemonic(opcode);
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

        string op = regField switch
        {
            0 => "ADD", 1 => "OR", 2 => "ADC", 3 => "SBB",
            4 => "AND", 5 => "SUB", 6 => "XOR", 7 => "CMP", _ => "ALU"
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

        string op = regField switch
        {
            0 => "TEST", 2 => "NOT", 3 => "NEG",
            4 => "MUL", 5 => "IMUL", 6 => "DIV", 7 => "IDIV",
            _ => "F6/F7"
        };

        if (regField == 0)
        {
            ushort imm = word ? ReadUInt16() : ReadByte();
            return new Instruction { Mnemonic = "TEST", Operands = $"{dst}, 0x{imm:X4}" };
        }

        return new Instruction { Mnemonic = op, Operands = dst };
    }

    private Instruction DecodeGroupFEFF(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int regField = (modrm >> 3) & 7;
        bool word = (opcode & 1) == 1;

        string dst = (mod == 3) ? GetReg8or16Name(modrm & 7, word) : GetMemoryOperand(rm: modrm & 7, mod);

        if (opcode == 0xFE)
        {
            string op8 = regField switch { 0 => "INC", 1 => "DEC", _ => "FE" };
            return new Instruction { Mnemonic = op8, Operands = dst };
        }

        string op = regField switch
        {
            0 => "INC", 1 => "DEC", 2 => "CALL", 3 => "CALL FAR",
            4 => "JMP", 5 => "JMP FAR", 6 => "PUSH", _ => "FF"
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
            return new Instruction { Mnemonic = "MOV", Operands = $"{regName}, {mem}" };
        else
            return new Instruction { Mnemonic = "MOV", Operands = $"{mem}, {regName}" };
    }

    private Instruction DecodeMovRegImm(byte opcode)
    {
        bool word = opcode >= 0xB8;
        string reg = GetReg8or16Name(opcode - (word ? 0xB8 : 0xB0), word);
        ushort imm = word ? ReadUInt16() : ReadByte();
        return new Instruction { Mnemonic = "MOV", Operands = $"{reg}, 0x{imm:X4}" };
    }

    private Instruction DecodeMovMemImm(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        string dst = (mod == 3) ? GetReg8or16Name(rm, word) : GetMemoryOperand(rm, mod);
        ushort imm = word ? ReadUInt16() : ReadByte();

        return new Instruction { Mnemonic = "MOV", Operands = $"{dst}, 0x{imm:X4}" };
    }

    private Instruction DecodeMovAxMem(byte opcode)
    {
        ushort disp = ReadUInt16();
        string seg = _segmentOverride switch
        {
            0x26 => "ES:", 0x2E => "CS:", 0x36 => "SS:", 0x3E => "DS:", _ => "DS:"
        };

        string addr = $"{seg}0x{disp:X4}";

        if (opcode == 0xA0) return new Instruction { Mnemonic = "MOV", Operands = $"AL, {addr}" };
        if (opcode == 0xA1) return new Instruction { Mnemonic = "MOV", Operands = $"AX, {addr}" };
        if (opcode == 0xA2) return new Instruction { Mnemonic = "MOV", Operands = $"{addr}, AL" };
        if (opcode == 0xA3) return new Instruction { Mnemonic = "MOV", Operands = $"{addr}, AX" };

        return new Instruction { Mnemonic = "MOV", Operands = addr };
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
            return new Instruction { Mnemonic = "MOV", Operands = $"{src}, {sregName}" };
        else
            return new Instruction { Mnemonic = "MOV", Operands = $"{sregName}, {src}" };
    }

    private Instruction DecodeShortJump(byte opcode)
    {
        sbyte rel = (sbyte)ReadByte();
        string mnem = opcode switch
        {
            0x70 => "JO", 0x71 => "JNO", 0x72 => "JB", 0x73 => "JAE",
            0x74 => "JE", 0x75 => "JNE", 0x76 => "JBE", 0x77 => "JA",
            0x78 => "JS", 0x79 => "JNS", 0x7A => "JP", 0x7B => "JNP",
            0x7C => "JL", 0x7D => "JGE", 0x7E => "JLE", 0x7F => "JG",
            0xEB => "JMP",
            _ => "Jcc"
        };
        return new Instruction { Mnemonic = mnem, Operands = $"0x{(_pos + rel):X4}" };
    }

    private Instruction DecodeNearJump()
    {
        short rel = (short)ReadUInt16();
        return new Instruction { Mnemonic = "JMP", Operands = $"0x{(_pos + rel):X4}" };
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
        return new Instruction { Mnemonic = "XCHG", Operands = $"{r1}, {r2}" };
    }

    private Instruction DecodeLea()
    {
        byte modrm = ReadByte();
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        int mod = (modrm >> 6) & 3;
        string mem = GetMemoryOperand(rm, mod);
        return new Instruction { Mnemonic = "LEA", Operands = $"{GetReg16Name(reg)}, {mem}" };
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
        return new Instruction { Mnemonic = "TEST", Operands = $"{r1}, {r2}" };
    }

    private Instruction DecodeTestAxImm(byte opcode)
    {
        bool word = (opcode & 1) == 1;
        ushort imm = word ? ReadUInt16() : ReadByte();
        return new Instruction { Mnemonic = "TEST", Operands = $"{(word ? "AX" : "AL")}, 0x{imm:X4}" };
    }

    private Instruction DecodeShift(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int rm = modrm & 7;
        int regField = (modrm >> 3) & 7;
        bool word = (opcode & 1) == 1;
        bool useCl = (opcode & 2) != 0;

        string op = regField switch { 0 => "ROL", 1 => "ROR", 2 => "RCL", 3 => "RCR", 4 => "SHL", 5 => "SHR", 7 => "SAR", _ => "SHIFT" };
        string dst = (mod == 3) ? GetReg8or16Name(rm, word) : GetMemoryOperand(rm, mod);
        string count = useCl ? "CL" : "1";
        return new Instruction { Mnemonic = op, Operands = $"{dst}, {count}" };
    }

    private Instruction DecodeLoop(byte opcode)
    {
        sbyte rel = (sbyte)ReadByte();
        string mnem = opcode switch { 0xE0 => "LOOPNE", 0xE1 => "LOOPE", 0xE2 => "LOOP", _ => "LOOP" };
        return new Instruction { Mnemonic = mnem, Operands = $"0x{(_pos + rel):X4}" };
    }

    private string GetAluMnemonic(byte opcode)
    {
        return (opcode >> 3) switch
        {
            0 => "ADD", 1 => "OR", 2 => "ADC", 3 => "SBB",
            4 => "AND", 5 => "SUB", 6 => "XOR", 7 => "CMP", _ => "ALU"
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

    public class Instruction
    {
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
            const string YELLOW = "\u001b[93m";
            const string GREEN = "\u001b[92m";

            string coloredMnemonic = YELLOW + Mnemonic + RESET;
            string coloredOperands = Operands;

            if (Operands.Contains("ES") || Operands.Contains("CS") || Operands.Contains("SS") || Operands.Contains("DS"))
                coloredOperands = GREEN + Operands + RESET;

            return $"{GRAY}0x{Offset:X6}:{RESET} {GRAY}{bytesStr,-20}{RESET} {coloredMnemonic} {coloredOperands}";
        }
    }
}