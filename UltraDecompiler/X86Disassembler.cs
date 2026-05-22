using System;
using System.Collections.Generic;
using System.Text;

namespace UltraDecompiler;

/// <summary>
/// Базовый дизассемблер 8086/80286 (16-bit real mode)
/// Поддерживает линейный свип + остановку на RET
/// </summary>
public class X86Disassembler
{
    private readonly byte[] _image;
    private int _pos;
    private byte _segmentOverride; // 0 = none, 0x26=ES, 0x2E=CS, 0x36=SS, 0x3E=DS

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
            result.Add(instr);

            // Останавливаемся на возврате для простоты
            if (instr.Mnemonic is "RET" or "RETF" or "IRET")
                break;
        }

        return result;
    }

    private Instruction DecodeOneInstruction()
    {
        byte opcode = ReadByte();

        // Обработка префиксов сегментов
        switch (opcode)
        {
            case 0x26: _segmentOverride = 0x26; return DecodeOneInstruction(); // ES:
            case 0x2E: _segmentOverride = 0x2E; return DecodeOneInstruction(); // CS:
            case 0x36: _segmentOverride = 0x36; return DecodeOneInstruction(); // SS:
            case 0x3E: _segmentOverride = 0x3E; return DecodeOneInstruction(); // DS:
        }

        string mnemonic;
        string operands;

        switch (opcode)
        {
            // === ALU r/m8, r8 / r8, r/m8 ===
            case 0x00: case 0x01: case 0x02: case 0x03:
            case 0x08: case 0x09: case 0x0A: case 0x0B:
            case 0x20: case 0x21: case 0x22: case 0x23:
            case 0x28: case 0x29: case 0x2A: case 0x2B:
            case 0x30: case 0x31: case 0x32: case 0x33:
                return DecodeModRmAlu(opcode);

            // MOV r/m, reg / reg, r/m
            case 0x88: case 0x89: case 0x8A: case 0x8B:
                return DecodeMovRegMem(opcode);

            // MOV reg, imm8/16
            case 0xB0: case 0xB1: case 0xB2: case 0xB3:
            case 0xB4: case 0xB5: case 0xB6: case 0xB7:
            case 0xB8: case 0xB9: case 0xBA: case 0xBB:
            case 0xBC: case 0xBD: case 0xBE: case 0xBF:
                return DecodeMovRegImm(opcode);

            // PUSH reg
            case 0x50: case 0x51: case 0x52: case 0x53:
            case 0x54: case 0x55: case 0x56: case 0x57:
                mnemonic = "PUSH";
                operands = GetReg16Name(opcode - 0x50);
                return new Instruction { Mnemonic = mnemonic, Operands = operands };

            // POP reg
            case 0x58: case 0x59: case 0x5A: case 0x5B:
            case 0x5C: case 0x5D: case 0x5E: case 0x5F:
                mnemonic = "POP";
                operands = GetReg16Name(opcode - 0x58);
                return new Instruction { Mnemonic = mnemonic, Operands = operands };

            // Conditional jumps (Jcc rel8)
            case 0x70: case 0x71: case 0x72: case 0x73:
            case 0x74: case 0x75: case 0x76: case 0x77:
            case 0x78: case 0x79: case 0x7A: case 0x7B:
            case 0x7C: case 0x7D: case 0x7E: case 0x7F:
                return DecodeShortJump(opcode);

            // JMP rel8 / JMP rel16
            case 0xEB: return DecodeShortJump(0xEB);
            case 0xE9: return DecodeNearJump();

            // CALL rel16
            case 0xE8:
                short rel = (short)ReadUInt16();
                return new Instruction { Mnemonic = "CALL", Operands = $"0x{(_pos + rel):X4}" };

            // RET / RETF
            case 0xC3: return new Instruction { Mnemonic = "RET" };
            case 0xCB: return new Instruction { Mnemonic = "RETF" };

            // INT xx
            case 0xCD:
                byte intNum = ReadByte();
                return new Instruction { Mnemonic = "INT", Operands = $"0x{intNum:X2}" };

            // NOP
            case 0x90:
                return new Instruction { Mnemonic = "NOP" };

            // INC/DEC reg16
            case 0x40: case 0x41: case 0x42: case 0x43:
            case 0x44: case 0x45: case 0x46: case 0x47:
                return new Instruction { Mnemonic = "INC", Operands = GetReg16Name(opcode - 0x40) };
            case 0x48: case 0x49: case 0x4A: case 0x4B:
            case 0x4C: case 0x4D: case 0x4E: case 0x4F:
                return new Instruction { Mnemonic = "DEC", Operands = GetReg16Name(opcode - 0x48) };

            default:
                return new Instruction 
                { 
                    Mnemonic = $"DB 0x{opcode:X2}", 
                    Operands = "; unknown opcode" 
                };
        }
    }

    private Instruction DecodeModRmAlu(byte opcode)
    {
        byte modrm = ReadByte();
        string op = GetAluMnemonic(opcode);
        string dst, src;

        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm  = modrm & 7;

        if (mod == 3) // reg, reg
        {
            dst = GetReg8or16Name(rm, (opcode & 1) == 1);
            src = GetReg8or16Name(reg, (opcode & 1) == 1);
        }
        else
        {
            dst = $"[{(mod == 0 && rm == 6 ? "0x" + ReadUInt16().ToString("X4") : GetEffectiveAddress(rm, mod))}]";
            src = GetReg8or16Name(reg, (opcode & 1) == 1);
        }

        if ((opcode & 2) != 0) // direction reg, r/m
            (dst, src) = (src, dst);

        return new Instruction { Mnemonic = op, Operands = $"{dst}, {src}" };
    }

    private Instruction DecodeMovRegMem(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        string regName = GetReg8or16Name(reg, word);
        string mem;

        if (mod == 3)
            mem = GetReg8or16Name(rm, word);
        else
            mem = $"[{(mod == 0 && rm == 6 ? "0x" + ReadUInt16().ToString("X4") : GetEffectiveAddress(rm, mod))}]";

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

    private string GetAluMnemonic(byte opcode)
    {
        return (opcode >> 3) switch
        {
            0 => "ADD", 1 => "OR",  2 => "ADC", 3 => "SBB",
            4 => "AND", 5 => "SUB", 6 => "XOR", 7 => "CMP",
            _ => "ALU"
        };
    }

    private string GetReg8or16Name(int reg, bool word)
    {
        if (word)
            return GetReg16Name(reg);
        return reg switch
        {
            0 => "AL", 1 => "CL", 2 => "DL", 3 => "BL",
            4 => "AH", 5 => "CH", 6 => "DH", 7 => "BH",
            _ => "?"
        };
    }

    private string GetReg16Name(int reg) => reg switch
    {
        0 => "AX", 1 => "CX", 2 => "DX", 3 => "BX",
        4 => "SP", 5 => "BP", 6 => "SI", 7 => "DI",
        _ => "?"
    };

    private string GetEffectiveAddress(int rm, int mod)
    {
        string seg = _segmentOverride switch
        {
            0x26 => "ES:", 0x2E => "CS:", 0x36 => "SS:", 0x3E => "DS:",
            _ => ""
        };

        string baseReg = rm switch
        {
            0 => "BX+SI", 1 => "BX+DI", 2 => "BP+SI", 3 => "BP+DI",
            4 => "SI",    5 => "DI",    6 => (mod == 0 ? "" : "BP"),
            7 => "BX",
            _ => "?"
        };

        if (mod == 1)
            return $"{seg}{baseReg}+{ReadByte()}";
        if (mod == 2)
            return $"{seg}{baseReg}+{ReadUInt16()}";

        return $"{seg}{baseReg}";
    }

    private byte ReadByte() => _image[_pos++];
    private ushort ReadUInt16() => (ushort)(_image[_pos++] | (_image[_pos++] << 8));

    public class Instruction
    {
        public int Offset { get; set; }
        public string Mnemonic { get; set; } = "";
        public string Operands { get; set; } = "";

        public override string ToString() => $"0x{Offset:X6}: {Mnemonic,-6} {Operands}";
    }
}