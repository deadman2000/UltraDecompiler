using Common;

namespace UltraDecompiler.Disassembler;

public class X86Disassembler
{
    private int _pos;

    /// <summary>
    /// Таблица релокаций MZ EXE (пустая для .COM и сырого бинарника).
    /// </summary>
    public RelocationTable Relocations { get; }

    public byte[] Image { get; }

    public X86Disassembler(byte[] image)
        : this(image, RelocationTable.Empty)
    {
    }

    public X86Disassembler(byte[] image, RelocationEntry[] relocations)
        : this(image, new RelocationTable("offset", relocations))
    {
    }

    public X86Disassembler(byte[] image, RelocationTable relocations)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(relocations);

        Relocations = relocations;

        Image = image;
    }

    public static List<Instruction> Disassemble(byte[] image,
        RelocationTable relocations,
        int startOffset,
        RegisterState initRegisters) =>
        Disassemble(image, relocations, startOffset, initRegisters, minJumpTarget: null);

    /// <summary>
    /// Линейное извлечение тела функции с обходом веток переходов.
    /// </summary>
    /// <param name="minJumpTarget">
    /// Смещение входа в функцию: безусловные JMP/JMP FAR на адреса ниже не обходятся
    /// (хвостовой переход в crt0/runtime). Условные переходы (циклы) по-прежнему обходятся.
    /// По умолчанию — <paramref name="startOffset"/>.
    /// </param>
    public static List<Instruction> Disassemble(byte[] image,
        RelocationTable relocations,
        int startOffset,
        RegisterState initRegisters,
        int? minJumpTarget)
    {
        var disassembler = new X86Disassembler(image, relocations);
        disassembler.Disassemble(startOffset, initRegisters, minJumpTarget);
        return disassembler.Instructions;
    }

    public List<Instruction> Instructions { get; private set; } = [];

    public void Disassemble(int startOffset)
    {
        Disassemble(startOffset, RegisterState.Unknown);
    }

    public void Disassemble(int startOffset, RegisterState initRegisters) =>
        Disassemble(startOffset, initRegisters, minJumpTarget: null);

    /// <inheritdoc cref="Disassemble(byte[], RelocationTable, int, RegisterState, int?)"/>
    public void Disassemble(int startOffset, RegisterState initRegisters, int? minJumpTarget)
    {
        var jumpFloor = minJumpTarget ?? startOffset;
        HashSet<int> visited = [];
        Instructions.Clear();

        var queue = new Queue<(int, RegisterState)>();
        queue.Enqueue((startOffset, initRegisters));

        while (queue.Count > 0)
        {
            var (offset, registers) = queue.Dequeue();

            if (visited.Contains(offset) || offset >= Image.Length)
                continue;

            DisassembleBlock(offset, queue, visited, registers, jumpFloor);
        }

        Instructions = Instructions.OrderBy(i => i.Offset).ToList();
    }

    private void DisassembleBlock(
        int startOffset,
        Queue<(int, RegisterState)> queue,
        HashSet<int> visited,
        RegisterState registers,
        int minJumpTarget)
    {
        _pos = startOffset;

        while (_pos < Image.Length)
        {
            if (visited.Contains(_pos))
                break;

            visited.Add(_pos);

            int instrStart = _pos;
            var instr = DecodeOneInstruction();
            instr.Offset = instrStart;
            instr.Bytes = Image[instrStart.._pos].ToArray();
            registers = instr.ApplyRegisters(registers);
            Instructions.Add(instr);

            if (instr.IsReturn || instr.IsExit)
                break;

            if (instr.IsConditionalJump || instr.IsUnconditionalJump)
            {
                int target = instr.GetEffectiveJumpTarget(Image);
                if (target != -1 && ShouldFollowJump(instr, target, minJumpTarget))
                {
                    queue.Enqueue((target, registers));
                }

                if (instr.IsUnconditionalJump)
                    break;
            }
        }
    }

    /// <summary>
    /// Решает, нужно ли обходить цель перехода при извлечении тела функции.
    /// </summary>
    private static bool ShouldFollowJump(Instruction instr, int target, int functionEntryOffset) =>
        target >= functionEntryOffset || instr.IsConditionalJump;

    /// <summary>
    /// Дизассемблирует инструкции до первого перехода.
    /// </summary>
    /// <param name="startOffset">Адрес первой инструкции</param>
    public IEnumerable<Instruction> DisassembleBranch(int startOffset)
    {
        return DisassembleBranch(startOffset, RegisterState.Unknown);
    }

    /// <summary>
    /// Дизассемблирует инструкции до первого перехода.
    /// </summary>
    /// <param name="startOffset">Адрес первой инструкции</param>
    public IEnumerable<Instruction> DisassembleBranch(int startOffset, RegisterState registers)
    {
        _pos = startOffset;

        while (_pos < Image.Length)
        {
            int instrStart = _pos;
            var instr = DecodeOneInstruction();
            instr.Offset = instrStart;
            instr.Bytes = Image[instrStart.._pos].ToArray();
            registers = instr.ApplyRegisters(registers);
            yield return instr;

            if (instr.IsConditionalJump || instr.IsUnconditionalJump || instr.IsReturn || instr.IsExit)
                break;
        }
    }

    private Instruction DecodeOneInstruction()
    {
        byte opcode = ReadByte();

        switch (opcode)
        {
            // Префиксы
            case 0xF0:
                {
                    var instr = DecodeOneInstruction();
                    instr.Prefix |= InstructionPrefix.LOCK;
                    return instr;
                }

            case 0xF2:
                {
                    var instr = DecodeOneInstruction();
                    instr.Prefix |= InstructionPrefix.REPNZ;
                    return instr;
                }
            case 0xF3:
                {
                    var instr = DecodeOneInstruction();
                    instr.Prefix |= InstructionPrefix.REPZ;
                    return instr;
                }

            case 0x26:
                {
                    var instr = DecodeOneInstruction();
                    instr.Segment = Segment.ES;
                    return instr;
                }
            case 0x2E:
                {
                    var instr = DecodeOneInstruction();
                    instr.Segment = Segment.CS;
                    return instr;
                }
            case 0x36:
                {
                    var instr = DecodeOneInstruction();
                    instr.Segment = Segment.SS;
                    return instr;
                }
            case 0x3E:
                {
                    var instr = DecodeOneInstruction();
                    instr.Segment = Segment.DS;
                    return instr;
                }

            case 0x00:
            case 0x01:
            case 0x02:
            case 0x03:
            case 0x08:
            case 0x09:
            case 0x0A:
            case 0x0B:
            case 0x10:
            case 0x11:
            case 0x12:
            case 0x13:
            case 0x18:
            case 0x19:
            case 0x1A:
            case 0x1B:
            case 0x20:
            case 0x21:
            case 0x22:
            case 0x23:
            case 0x28:
            case 0x29:
            case 0x2A:
            case 0x2B:
            case 0x30:
            case 0x31:
            case 0x32:
            case 0x33:
            case 0x38:
            case 0x39:
            case 0x3A:
            case 0x3B:
                return DecodeModRmAlu(opcode);

            case 0x04:
            case 0x05:
            case 0x0C:
            case 0x0D:
            case 0x14:
            case 0x15:
            case 0x1C:
            case 0x1D:
            case 0x24:
            case 0x25:
            case 0x2C:
            case 0x2D:
            case 0x34:
            case 0x35:
            case 0x3C:
            case 0x3D:
                return DecodeAluImmAx(opcode);

            case 0x80:
            case 0x81:
            case 0x82:
            case 0x83:
                return DecodeGroup80(opcode);

            case 0xF6:
            case 0xF7:
                return DecodeGroupF6(opcode);

            case 0xFE:
            case 0xFF:
                return DecodeGroupFEFF(opcode);

            case 0x88:
            case 0x89:
            case 0x8A:
            case 0x8B:
                return DecodeMovRegMem(opcode);

            case 0x8C:
            case 0x8E:
                return DecodeMovSreg(opcode);

            case 0x8F:
                return DecodeGroup8F();

            case 0xA0:
            case 0xA1:
            case 0xA2:
            case 0xA3:
                return DecodeMovAxMem(opcode);

            case 0xB0:
            case 0xB1:
            case 0xB2:
            case 0xB3:
            case 0xB4:
            case 0xB5:
            case 0xB6:
            case 0xB7:
            case 0xB8:
            case 0xB9:
            case 0xBA:
            case 0xBB:
            case 0xBC:
            case 0xBD:
            case 0xBE:
            case 0xBF:
                return DecodeMovRegImm(opcode);

            case 0xC6:
            case 0xC7:
                return DecodeMovMemImm(opcode);

            case 0x68:
                return new Instruction
                {
                    Mnemonic = Mnemonic.PUSH,
                    Operand1 = Imm16(ReadUInt16()),
                };

            case 0x6A:
                {
                    // PUSH imm8 (знак-расширяется до 16 бит)
                    sbyte imm = (sbyte)ReadByte();
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.PUSH,
                        Operand1 = Imm16((ushort)imm)
                    };
                }

            case 0x50:
            case 0x51:
            case 0x52:
            case 0x53:
            case 0x54:
            case 0x55:
            case 0x56:
            case 0x57:
                {
                    int reg = opcode - 0x50;
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.PUSH,
                        Operand1 = new Operand(OperandType.Register16, reg)
                    };
                }
            case 0x58:
            case 0x59:
            case 0x5A:
            case 0x5B:
            case 0x5C:
            case 0x5D:
            case 0x5E:
            case 0x5F:
                {
                    int reg = opcode - 0x58;
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.POP,
                        Operand1 = new Operand(OperandType.Register16, reg)
                    };
                }

            case 0x06: return new Instruction { Mnemonic = Mnemonic.PUSH, Operand1 = Operand.ES };
            case 0x0E: return new Instruction { Mnemonic = Mnemonic.PUSH, Operand1 = Operand.CS };
            case 0x16: return new Instruction { Mnemonic = Mnemonic.PUSH, Operand1 = Operand.SS };
            case 0x1E: return new Instruction { Mnemonic = Mnemonic.PUSH, Operand1 = Operand.DS };
            case 0x07: return new Instruction { Mnemonic = Mnemonic.POP, Operand1 = Operand.ES };
            case 0x17: return new Instruction { Mnemonic = Mnemonic.POP, Operand1 = Operand.SS };
            case 0x1F: return new Instruction { Mnemonic = Mnemonic.POP, Operand1 = Operand.DS };

            case 0x70:
            case 0x71:
            case 0x72:
            case 0x73:
            case 0x74:
            case 0x75:
            case 0x76:
            case 0x77:
            case 0x78:
            case 0x79:
            case 0x7A:
            case 0x7B:
            case 0x7C:
            case 0x7D:
            case 0x7E:
            case 0x7F:
            case 0xEB:
            case 0xE3:
                return DecodeShortJump(opcode);

            case 0xE9: return DecodeNearJump();

            case 0xE8:
                short rel = (short)ReadUInt16();
                return new Instruction
                {
                    Mnemonic = Mnemonic.CALL,
                    Operand1 = new Operand(OperandType.Relative16, _pos + rel, relocation: _relocation)
                };

            case 0x9A:
                {
                    // CALL FAR ptr16:16 — сначала IP, затем CS (little-endian).
                    ushort offset = ReadUInt16();
                    var offsetRelocation = _relocation;
                    ushort segment = ReadUInt16();
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.CALL_FAR,
                        Operand1 = new Operand(OperandType.Immediate16, offset, relocation: offsetRelocation),
                        Operand2 = new Operand(OperandType.Immediate16, segment, relocation: _relocation),
                    };
                }

            case 0xC2:
                {
                    // RET imm16 (near) — снимает imm16 байт параметров со стека после возврата
                    ushort imm = ReadUInt16();
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.RET_IMM,
                        Operand1 = new Operand(OperandType.Immediate16, imm)
                    };
                }
            case 0xC3: return new Instruction { Mnemonic = Mnemonic.RET };
            case 0xCA:
                {
                    // RETF imm16 (far) — снимает imm16 байт параметров со стека после far return
                    ushort imm = ReadUInt16();
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.RETF_IMM,
                        Operand1 = new Operand(OperandType.Immediate16, imm)
                    };
                }
            case 0xCB: return new Instruction { Mnemonic = Mnemonic.RETF };
            case 0xCE: return new Instruction { Mnemonic = Mnemonic.INTO };
            case 0xCF: return new Instruction { Mnemonic = Mnemonic.IRET };

            case 0xCD:
                {
                    byte intNum = ReadByte();
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.INT,
                        Operand1 = new Operand(OperandType.Immediate8, intNum)
                    };
                }

            case 0x90: return new Instruction { Mnemonic = Mnemonic.NOP };
            case 0x9B: return new Instruction { Mnemonic = Mnemonic.FWAIT };

            case 0x86:
            case 0x87:
                return DecodeXchg(opcode);
            case 0x91:
            case 0x92:
            case 0x93:
            case 0x94:
            case 0x95:
            case 0x96:
            case 0x97:
                {
                    int reg = opcode - 0x90;
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.XCHG,
                        Operand1 = Operand.AX,
                        Operand2 = new Operand(OperandType.Register16, reg)
                    };
                }

            case 0x40:
            case 0x41:
            case 0x42:
            case 0x43:
            case 0x44:
            case 0x45:
            case 0x46:
            case 0x47:
                {
                    int reg = opcode - 0x40;
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.INC,
                        Operand1 = new Operand(OperandType.Register16, reg)
                    };
                }
            case 0x48:
            case 0x49:
            case 0x4A:
            case 0x4B:
            case 0x4C:
            case 0x4D:
            case 0x4E:
            case 0x4F:
                {
                    int reg = opcode - 0x48;
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.DEC,
                        Operand1 = new Operand(OperandType.Register16, reg)
                    };
                }

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

            case 0xD0:
            case 0xD1:
            case 0xD2:
            case 0xD3:
                return DecodeShift(opcode);

            case 0x98: return new Instruction { Mnemonic = Mnemonic.CBW };
            case 0x99: return new Instruction { Mnemonic = Mnemonic.CWD };

            case 0xE0:
            case 0xE1:
            case 0xE2:
                return DecodeLoop(opcode);

            case 0x27: return new Instruction { Mnemonic = Mnemonic.DAA };
            case 0x2F: return new Instruction { Mnemonic = Mnemonic.DAS };
            case 0x37: return new Instruction { Mnemonic = Mnemonic.AAA };
            case 0x3F: return new Instruction { Mnemonic = Mnemonic.AAS };

            case 0xD4: return DecodeAam();
            case 0xD5: return DecodeAad();

            case 0xD8:
            case 0xD9:
            case 0xDA:
            case 0xDB:
            case 0xDC:
            case 0xDD:
            case 0xDE:
            case 0xDF:
                return DecodeFpu(opcode);

            case 0x9C: return new Instruction { Mnemonic = Mnemonic.PUSHF };
            case 0x9D: return new Instruction { Mnemonic = Mnemonic.POPF };
            case 0x9E: return new Instruction { Mnemonic = Mnemonic.SAHF };
            case 0x9F: return new Instruction { Mnemonic = Mnemonic.LAHF };

            case 0xFA: return new Instruction { Mnemonic = Mnemonic.CLI };
            case 0xFB: return new Instruction { Mnemonic = Mnemonic.STI };
            case 0xFC: return new Instruction { Mnemonic = Mnemonic.CLD };
            case 0xFD: return new Instruction { Mnemonic = Mnemonic.STD };
            case 0xF5: return new Instruction { Mnemonic = Mnemonic.CMC };
            case 0xF8: return new Instruction { Mnemonic = Mnemonic.CLC };
            case 0xF9: return new Instruction { Mnemonic = Mnemonic.STC };

            case 0xD7: return new Instruction { Mnemonic = Mnemonic.XLAT };

            case 0xF4: return new Instruction { Mnemonic = Mnemonic.HLT };

            case 0xC4: return DecodeLes();
            case 0xC5: return DecodeLds();

            case 0xC8: return DecodeEnter();
            case 0xC9: return new Instruction { Mnemonic = Mnemonic.LEAVE };

            case 0xE4: // IN AL, imm8
                {
                    byte port = ReadByte();
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.IN,
                        Operand1 = Operand.AL,
                        Operand2 = new Operand(OperandType.Immediate8, port)
                    };
                }
            case 0xE5: // IN AX, imm8
                {
                    byte port = ReadByte();
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.IN,
                        Operand1 = Operand.AX,
                        Operand2 = new Operand(OperandType.Immediate8, port)
                    };
                }
            case 0xE6: // OUT imm8, AL
                {
                    byte port = ReadByte();
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.OUT,
                        Operand1 = new Operand(OperandType.Immediate8, port),
                        Operand2 = Operand.AL
                    };
                }
            case 0xE7: // OUT imm8, AX
                {
                    byte port = ReadByte();
                    return new Instruction
                    {
                        Mnemonic = Mnemonic.OUT,
                        Operand1 = new Operand(OperandType.Immediate8, port),
                        Operand2 = Operand.AX
                    };
                }
            case 0xEC: // IN AL, DX
                return new Instruction
                {
                    Mnemonic = Mnemonic.IN,
                    Operand1 = Operand.AL,
                    Operand2 = Operand.DX
                };
            case 0xED: // IN AX, DX
                return new Instruction
                {
                    Mnemonic = Mnemonic.IN,
                    Operand1 = Operand.AX,
                    Operand2 = Operand.DX
                };
            case 0xEE: // OUT DX, AL
                return new Instruction
                {
                    Mnemonic = Mnemonic.OUT,
                    Operand1 = Operand.DX,
                    Operand2 = Operand.AL
                };
            case 0xEF: // OUT DX, AX
                return new Instruction
                {
                    Mnemonic = Mnemonic.OUT,
                    Operand1 = Operand.DX,
                    Operand2 = Operand.AX
                };

            default:
                return new Instruction
                {
                    Mnemonic = Mnemonic.DB,
                    Operand1 = new(OperandType.Immediate8, opcode)
                };
        }
    }

    private Instruction DecodeLds()
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;

        var instr = new Instruction
        {
            Mnemonic = Mnemonic.LDS,
            Operand1 = new Operand(OperandType.Register16, reg)
        };
        if (mod != 3)
            instr.Operand2 = ParseMemoryOperand(rm, mod);
        else
            instr.Operand2 = new Operand(OperandType.Register16, rm);
        return instr;
    }

    private Instruction DecodeLes()
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;

        var instr = new Instruction
        {
            Mnemonic = Mnemonic.LES,
            Operand1 = new Operand(OperandType.Register16, reg)
        };
        if (mod != 3)
            instr.Operand2 = ParseMemoryOperand(rm, mod);
        else
            instr.Operand2 = new Operand(OperandType.Register16, rm);
        return instr;
    }

    private Instruction DecodeAam()
    {
        byte baseVal = ReadByte();
        return baseVal == 0x0A ? new Instruction { Mnemonic = Mnemonic.AAM } : new Instruction { Mnemonic = Mnemonic.AAM, Commentary = "non-standard" };
    }

    private Instruction DecodeAad()
    {
        byte baseVal = ReadByte();
        return baseVal == 0x0A ? new Instruction { Mnemonic = Mnemonic.AAD } : new Instruction { Mnemonic = Mnemonic.AAD, Commentary = "non-standard" };
    }

    /// <summary>
    /// Декодирование FPU escape-инструкций 8087 (опкоды D8-DF).
    /// Для библиотек QuickC (эмуляция floating point) это в основном thunk'и
    /// вида "FWAIT; &lt;fpu-op&gt;; NOP; RET" с FIXUPP на FIDRQQ/FIWRQQ и т.п.
    /// Полная поддержка всех FPU-мнемоник не требуется; важно корректно
    /// потреблять байты (modrm + disp) и не превращать в DB.
    /// </summary>
    private Instruction DecodeFpu(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int regField = (modrm >> 3) & 7;
        int rm = modrm & 7;

        // Пытаемся дать более осмысленную "псевдо-мнемонику" для часто встречающихся thunk'ов QuickC.
        // Полноценная таблица 8087 не нужна — эти инструкции почти никогда не дойдут до ExpressionBuilder.
        // Ключ — правильный размер инструкции + отсутствие DB.
        string fpuName = (opcode, regField, mod, modrm) switch
        {
            // D8
            (0xD8, 0, _, _) => "fadd",
            (0xD8, 1, _, _) => "fmul",
            (0xD8, 2, _, _) => "fcom",
            (0xD8, 3, _, _) => "fcomp",
            (0xD8, 4, _, _) => "fsub",
            (0xD8, 5, _, _) => "fsubr",
            (0xD8, 6, _, _) => "fdiv",
            (0xD8, 7, _, _) => "fdivr",

            // D9 — сначала конкретные 2-байтные (mod==3) по полному modrm (часто в thunk'ах)
            (0xD9, _, 3, 0xE0) => "fchs",
            (0xD9, _, 3, 0xE1) => "fld1",
            (0xD9, _, 3, 0xE4) => "ftst",
            (0xD9, _, 3, 0xE5) => "fxam",

            // D9 mem (mod != 3)
            (0xD9, 0, _, _) when mod != 3 => "fld",
            (0xD9, 2, _, _) when mod != 3 => "fst",
            (0xD9, 3, _, _) when mod != 3 => "fstp",
            (0xD9, 4, _, _) when mod != 3 => "fldenv",
            (0xD9, 5, _, _) when mod != 3 => "fldcw",
            (0xD9, 6, _, _) when mod != 3 => "fstenv",
            (0xD9, 7, _, _) when mod != 3 => "fstcw",

            // D9 /r mod==3 по regField (общие)
            (0xD9, 1, 3, _) => "fxch",
            (0xD9, 0, 3, _) => "fld",
            (0xD9, 3, 3, _) => "fstp",
            (0xD9, _, 3, _) => "fpu",

            (0xDA, _, _, _) => "fpu",
            (0xDB, _, _, _) => "fpu",

            // DC
            (0xDC, 0, _, _) => "fadd",
            (0xDC, 1, _, _) => "fmul",
            (0xDC, 4, _, _) => "fsub",
            (0xDC, 6, _, _) => "fdiv",

            // DD
            (0xDD, 0, _, _) when mod != 3 => "fld",
            (0xDD, 2, _, _) when mod != 3 => "fst",
            (0xDD, 3, _, _) when mod != 3 => "fstp",
            (0xDD, _, 3, _) => "fpu",

            // DE — классика для thunk'ов (FADDP и т.д.)
            (0xDE, 0, 3, _) => "faddp",
            (0xDE, 1, 3, _) => "fmulp",
            (0xDE, 2, 3, _) => "fcompp",
            (0xDE, 4, 3, _) => "fsubp",
            (0xDE, 5, 3, _) => "fsubrp",
            (0xDE, 6, 3, _) => "fdivp",
            (0xDE, 7, 3, _) => "fdivrp",
            (0xDE, _, _, _) => "fpu",

            (0xDF, _, _, _) => "fpu",

            _ => "fpu"
        };

        Mnemonic mnem = Mnemonic.FPU;

        var instr = new Instruction { Mnemonic = mnem };

        // Потребляем displacement при необходимости (mod != 3)
        if (mod != 3)
        {
            if (mod == 1)
                ReadByte();
            else if (mod == 2)
                ReadUInt16Core();
            else if (mod == 0 && rm == 6)
                ReadUInt16Core();
        }

        // Для CLI `lib -s` делаем вывод осмысленным: "faddp", "fld" и т.д. в Commentary.
        // Это не влияет на байты и LibMatching.
        instr.Commentary = fpuName;

        return instr;
    }

    private Instruction DecodeEnter()
    {
        return new Instruction
        {
            Mnemonic = Mnemonic.ENTER,
            Operand1 = Imm16(ReadUInt16()),
            Operand2 = Imm8(ReadByte())
        };
    }

    private Instruction DecodeModRmAlu(byte opcode)
    {
        byte modrm = ReadByte();
        Mnemonic mnem = GetAluMnemonicEnum(opcode);
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        var instr = new Instruction { Mnemonic = mnem };

        if ((opcode & 2) != 0)
        {
            instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
            if (mod == 3)
                instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
            else
                instr.Operand2 = ParseMemoryOperand(rm, mod);
        }
        else
        {
            if (mod == 3)
                instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
            else
                instr.Operand1 = ParseMemoryOperand(rm, mod);
            instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
        }

        return instr;
    }

    private Instruction DecodeAluImmAx(byte opcode)
    {
        Mnemonic mnem = GetAluMnemonicEnum(opcode);
        bool word = (opcode & 1) == 1;
        var instr = new Instruction
        {
            Mnemonic = mnem,
            Operand1 = word ? Operand.AX : Operand.AL,
            Operand2 = word ? Imm16(ReadUInt16()) : Imm8(ReadByte())
        };
        return instr;
    }

    private Instruction DecodeGroup80(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int regField = (modrm >> 3) & 7;
        bool word = (opcode & 1) == 1;
        bool signExtend = opcode == 0x83;

        Mnemonic mnem = regField switch
        {
            0 => Mnemonic.ADD,
            1 => Mnemonic.OR,
            2 => Mnemonic.ADC,
            3 => Mnemonic.SBB,
            4 => Mnemonic.AND,
            5 => Mnemonic.SUB,
            6 => Mnemonic.XOR,
            7 => Mnemonic.CMP,
            _ => Mnemonic.DB
        };

        var instr = new Instruction { Mnemonic = mnem };

        if (mod == 3)
            instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, modrm & 7);
        else
            instr.Operand1 = ParseMemoryOperand(modrm & 7, mod);
        if (signExtend)
            instr.Operand2 = Imm16((ushort)(sbyte)ReadByte());
        else if (word)
            instr.Operand2 = Imm16(ReadUInt16());
        else
            instr.Operand2 = Imm8(ReadByte());

        return instr;
    }

    private Instruction DecodeGroupF6(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int regField = (modrm >> 3) & 7;
        bool word = (opcode & 1) == 1;

        var instr = new Instruction
        {
            Mnemonic = regField switch
            {
                0 => Mnemonic.TEST,
                2 => Mnemonic.NOT,
                3 => Mnemonic.NEG,
                4 => Mnemonic.MUL,
                5 => Mnemonic.IMUL,
                6 => Mnemonic.DIV,
                7 => Mnemonic.IDIV,
                _ => Mnemonic.DB
            }
        };

        if (mod == 3)
            instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, modrm & 7);
        else
            instr.Operand1 = ParseMemoryOperand(modrm & 7, mod);

        if (regField == 0)
        {
            instr.Operand2 = word ? Imm16(ReadUInt16()) : Imm8(ReadByte());
        }

        return instr;
    }

    private Instruction DecodeGroupFEFF(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int regField = (modrm >> 3) & 7;
        bool word = (opcode & 1) == 1;

        var instr = new Instruction();

        if (mod == 3)
            instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, modrm & 7);
        else
            instr.Operand1 = ParseMemoryOperand(modrm & 7, mod);

        if (opcode == 0xFE)
        {
            instr.Mnemonic = regField switch { 0 => Mnemonic.INC, 1 => Mnemonic.DEC, _ => Mnemonic.DB };
            return instr;
        }

        instr.Mnemonic = regField switch
        {
            0 => Mnemonic.INC,
            1 => Mnemonic.DEC,
            2 => Mnemonic.CALL,
            3 => Mnemonic.CALL_FAR,
            4 => Mnemonic.JMP,
            5 => Mnemonic.JMP_FAR,
            6 => Mnemonic.PUSH,
            _ => Mnemonic.DB
        };
        return instr;
    }

    private Instruction DecodeGroup8F()
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int regField = (modrm >> 3) & 7;

        var instr = new Instruction();

        if (mod == 3)
            instr.Operand1 = new Operand(OperandType.Register16, modrm & 7);
        else
            instr.Operand1 = ParseMemoryOperand(modrm & 7, mod);

        // 8F /0 = POP r/m16. Остальные regField на 8086 — недопустимы.
        instr.Mnemonic = regField == 0 ? Mnemonic.POP : Mnemonic.DB;
        return instr;
    }

    private Instruction DecodeMovRegMem(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        var instr = new Instruction() { Mnemonic = Mnemonic.MOV };

        if ((opcode & 2) != 0)
        {
            instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
            if (mod == 3)
                instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
            else
                instr.Operand2 = ParseMemoryOperand(rm, mod);
        }
        else
        {
            if (mod == 3)
                instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
            else
                instr.Operand1 = ParseMemoryOperand(rm, mod);
            instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
        }

        return instr;
    }

    private Instruction DecodeMovRegImm(byte opcode)
    {
        bool word = opcode >= 0xB8;
        int regIndex = opcode - (word ? 0xB8 : 0xB0);
        return new Instruction
        {
            Mnemonic = Mnemonic.MOV,
            Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, regIndex),
            Operand2 = word ? Imm16(ReadUInt16()) : Imm8(ReadByte())
        };
    }

    private Instruction DecodeMovMemImm(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        var instr = new Instruction { Mnemonic = Mnemonic.MOV };
        if (mod == 3)
            instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
        else
            instr.Operand1 = ParseMemoryOperand(rm, mod);
        instr.Operand2 = word ? Imm16(ReadUInt16()) : Imm8(ReadByte());
        return instr;
    }

    private Instruction DecodeMovAxMem(byte opcode)
    {
        var instr = new Instruction { Mnemonic = Mnemonic.MOV };
        if (opcode == 0xA0)
        {
            instr.Operand1 = Operand.AL;
            instr.Operand2 = Memory(ReadUInt16());
        }
        if (opcode == 0xA1)
        {
            instr.Operand1 = Operand.AX;
            instr.Operand2 = Memory(ReadUInt16());
        }
        if (opcode == 0xA2)
        {
            instr.Operand1 = Memory(ReadUInt16());
            instr.Operand2 = Operand.AL;
        }
        if (opcode == 0xA3)
        {
            instr.Operand1 = Memory(ReadUInt16());
            instr.Operand2 = Operand.AX;
        }
        return instr;
    }

    private Instruction DecodeMovSreg(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int sreg = (modrm >> 3) & 7;
        int rm = modrm & 7;

        var instr = new Instruction { Mnemonic = Mnemonic.MOV };
        if ((opcode & 2) != 0)
        {
            instr.Operand1 = new Operand(OperandType.SegmentRegister, sreg);
            if (mod == 3)
                instr.Operand2 = new Operand(OperandType.Register16, rm);
            else
                instr.Operand2 = ParseMemoryOperand(rm, mod);
        }
        else
        {
            if (mod == 3)
                instr.Operand1 = new Operand(OperandType.Register16, rm);
            else
                instr.Operand1 = ParseMemoryOperand(rm, mod);
            instr.Operand2 = new Operand(OperandType.SegmentRegister, sreg);
        }
        return instr;
    }

    private Instruction DecodeShortJump(byte opcode)
    {
        sbyte rel = (sbyte)ReadByte();
        int target = _pos + rel;

        Mnemonic mnem = opcode switch
        {
            0x70 => Mnemonic.JO,
            0x71 => Mnemonic.JNO,
            0x72 => Mnemonic.JB,
            0x73 => Mnemonic.JAE,
            0x74 => Mnemonic.JE,
            0x75 => Mnemonic.JNE,
            0x76 => Mnemonic.JBE,
            0x77 => Mnemonic.JA,
            0x78 => Mnemonic.JS,
            0x79 => Mnemonic.JNS,
            0x7A => Mnemonic.JP,
            0x7B => Mnemonic.JNP,
            0x7C => Mnemonic.JL,
            0x7D => Mnemonic.JGE,
            0x7E => Mnemonic.JLE,
            0x7F => Mnemonic.JG,
            0xE3 => Mnemonic.JCXZ,
            0xEB => Mnemonic.JMP,
            _ => Mnemonic.DB
        };

        var instr = new Instruction
        {
            Mnemonic = mnem,
            Operand1 = new Operand(OperandType.Relative8, target)
        };
        return instr;
    }

    private Instruction DecodeNearJump()
    {
        short rel = (short)ReadUInt16Core();
        int target = _pos + rel;

        var instr = new Instruction
        {
            Mnemonic = Mnemonic.JMP,
            Operand1 = new Operand(OperandType.Relative16, target)
        };
        return instr;
    }

    private Instruction DecodeLea()
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;

        var instr = new Instruction
        {
            Mnemonic = Mnemonic.LEA,
            Operand1 = new Operand(OperandType.Register16, reg)
        };
        if (mod == 3)
            instr.Operand2 = new Operand(OperandType.Register16, rm);
        else
            instr.Operand2 = ParseMemoryOperand(rm, mod);
        return instr;
    }

    private Instruction DecodeXchg(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        var instr = new Instruction { Mnemonic = Mnemonic.XCHG };
        instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
        if (mod == 3)
            instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
        else
            instr.Operand2 = ParseMemoryOperand(rm, mod);
        return instr;
    }

    private Instruction DecodeTestModRm(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int reg = (modrm >> 3) & 7;
        int rm = modrm & 7;
        bool word = (opcode & 1) == 1;

        var instr = new Instruction { Mnemonic = Mnemonic.TEST };
        if (mod == 3)
            instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, rm);
        else
            instr.Operand1 = ParseMemoryOperand(rm, mod);
        instr.Operand2 = new Operand(word ? OperandType.Register16 : OperandType.Register8, reg);
        return instr;
    }

    private Instruction DecodeTestAxImm(byte opcode)
    {
        bool word = (opcode & 1) == 1;
        return new Instruction
        {
            Mnemonic = Mnemonic.TEST,
            Operand1 = word ? Operand.AX : Operand.AL,
            Operand2 = word ? Imm16(ReadUInt16()) : Imm8(ReadByte())
        };
    }

    private Instruction DecodeShift(byte opcode)
    {
        byte modrm = ReadByte();
        int mod = (modrm >> 6) & 3;
        int regField = (modrm >> 3) & 7;
        bool word = (opcode & 1) == 1;

        Mnemonic mnem = regField switch
        {
            0 => Mnemonic.ROL,
            1 => Mnemonic.ROR,
            2 => Mnemonic.RCL,
            3 => Mnemonic.RCR,
            4 => Mnemonic.SAL,
            5 => Mnemonic.SHR,
            6 => Mnemonic.SAL,
            7 => Mnemonic.SAR,
            _ => Mnemonic.DB
        };

        var instr = new Instruction { Mnemonic = mnem };
        if (mod == 3)
            instr.Operand1 = new Operand(word ? OperandType.Register16 : OperandType.Register8, modrm & 7);
        else
            instr.Operand1 = ParseMemoryOperand(modrm & 7, mod);
        // CL or 1
        if ((opcode & 2) != 0)   // D2 и D3
            instr.Operand2 = Operand.CL;
        else                     // D0 и D1
            instr.Operand2 = new Operand(OperandType.Immediate8, 1);
        return instr;
    }

    private Instruction DecodeLoop(byte opcode)
    {
        sbyte rel = (sbyte)ReadByte();
        int target = _pos + rel;
        Mnemonic mnem = opcode switch
        {
            0xE0 => Mnemonic.LOOPNE,
            0xE1 => Mnemonic.LOOPE,
            0xE2 => Mnemonic.LOOP,
            _ => Mnemonic.LOOP
        };

        return new Instruction
        {
            Mnemonic = mnem,
            Operand1 = new Operand(OperandType.Relative8, target)
        };
    }

    private Operand ParseMemoryOperand(int rm, int mod)
    {
        int disp = 0;
        if (mod == 1)
            disp = (sbyte)ReadByte();
        else if (mod == 2)
            disp = (short)ReadUInt16();

        AddressRegister baseReg = AddressRegister.None;
        AddressRegister indexReg = AddressRegister.None;

        switch (rm)
        {
            case 0: baseReg = AddressRegister.BX; indexReg = AddressRegister.SI; break; // [BX+SI]
            case 1: baseReg = AddressRegister.BX; indexReg = AddressRegister.DI; break; // [BX+DI]
            case 2: baseReg = AddressRegister.BP; indexReg = AddressRegister.SI; break; // [BP+SI]
            case 3: baseReg = AddressRegister.BP; indexReg = AddressRegister.DI; break; // [BP+DI]
            case 4: baseReg = AddressRegister.SI; break; // [SI]
            case 5: baseReg = AddressRegister.DI; break; // [DI]
            case 6:
                if (mod == 0)
                {
                    disp = ReadUInt16();
                    baseReg = AddressRegister.None; // direct
                }
                else
                    baseReg = AddressRegister.BP; // [BP+disp]
                break;
            case 7: baseReg = AddressRegister.BX; break; // [BX]
        }

        return Memory(disp, baseReg, indexReg);
    }

    private static Mnemonic GetAluMnemonicEnum(byte opcode)
    {
        return ((opcode >> 3) & 7) switch
        {
            0 => Mnemonic.ADD,
            1 => Mnemonic.OR,
            2 => Mnemonic.ADC,
            3 => Mnemonic.SBB,
            4 => Mnemonic.AND,
            5 => Mnemonic.SUB,
            6 => Mnemonic.XOR,
            7 => Mnemonic.CMP,
            _ => Mnemonic.DB
        };
    }

    private string? _relocation;

    private byte ReadByte()
    {
        _relocation = null;
        return Image[_pos++];
    }

    private ushort ReadUInt16Core()
    {
        ushort val = (ushort)(Image[_pos] | (Image[_pos + 1] << 8));
        _pos += 2;
        return val;
    }

    private ushort ReadUInt16()
    {
        _relocation = Relocations.TryGetOffsetName(_pos, out var name) ? name : null;
        return ReadUInt16Core();
    }

    private Operand Imm16(int value) =>
        new(OperandType.Immediate16, value, relocation: _relocation);

    private Operand Imm8(int value) =>
        new(OperandType.Immediate8, value);

    private Operand Memory(int disp, AddressRegister baseReg = AddressRegister.None, AddressRegister indexReg = AddressRegister.None) =>
        new(OperandType.Memory, disp, baseReg, indexReg, relocation: _relocation);
}