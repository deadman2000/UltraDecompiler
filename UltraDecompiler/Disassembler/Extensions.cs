using System.Text;
using UltraDecompiler.Common;

namespace UltraDecompiler.Disassembler;

public static class Extensions
{
    extension(Instruction instruction)
    {
        public string GetPrefix()
        {
            if (instruction.Prefix == InstructionPrefix.None)
                return string.Empty;

            var sb = new StringBuilder();

            if (instruction.Prefix.HasFlag(InstructionPrefix.LOCK))
                sb.Append("lock ");

            if (instruction.Prefix.HasFlag(InstructionPrefix.REPZ))
                sb.Append("repz ");

            if (instruction.Prefix.HasFlag(InstructionPrefix.REPNZ))
                sb.Append("repnz ");

            return sb.ToString();
        }

        public string GetMnemonicString() => instruction.Mnemonic switch
        {
            Mnemonic.MOV => "mov",
            Mnemonic.PUSH => "push",
            Mnemonic.POP => "pop",
            Mnemonic.XCHG => "xchg",
            Mnemonic.LEA => "lea",
            Mnemonic.LDS => "lds",
            Mnemonic.LES => "les",

            Mnemonic.ADD => "add",
            Mnemonic.ADC => "adc",
            Mnemonic.SUB => "sub",
            Mnemonic.SBB => "sbb",
            Mnemonic.CMP => "cmp",
            Mnemonic.AND => "and",
            Mnemonic.OR => "or",
            Mnemonic.XOR => "xor",
            Mnemonic.NOT => "not",
            Mnemonic.NEG => "neg",
            Mnemonic.INC => "inc",
            Mnemonic.DEC => "dec",
            Mnemonic.MUL => "mul",
            Mnemonic.IMUL => "imul",
            Mnemonic.DIV => "div",
            Mnemonic.IDIV => "idiv",

            Mnemonic.TEST => "test",
            Mnemonic.SHR => "shr",
            Mnemonic.SAR => "sar",
            Mnemonic.ROL => "rol",
            Mnemonic.ROR => "ror",
            Mnemonic.RCL => "rcl",
            Mnemonic.RCR => "rcr",

            Mnemonic.JMP => "jmp",
            Mnemonic.JMP_FAR => "jmp far",
            Mnemonic.CALL => "call",
            Mnemonic.CALL_FAR => "call far",
            Mnemonic.RET => "ret",
            Mnemonic.RETF => "retf",
            Mnemonic.RETF_FAR => "retf",
            Mnemonic.IRET => "iret",

            Mnemonic.JO => "jo",
            Mnemonic.JNO => "jno",
            Mnemonic.JB => "jb",
            Mnemonic.JAE => "jae",
            Mnemonic.JE => "je",
            Mnemonic.JNE => "jne",
            Mnemonic.JBE => "jbe",
            Mnemonic.JA => "ja",
            Mnemonic.JS => "js",
            Mnemonic.JNS => "jns",
            Mnemonic.JP => "jp",
            Mnemonic.JNP => "jnp",
            Mnemonic.JL => "jl",
            Mnemonic.JGE => "jge",
            Mnemonic.JLE => "jle",
            Mnemonic.JG => "jg",
            Mnemonic.JCXZ => "jcxz",

            Mnemonic.LOOP => "loop",
            Mnemonic.LOOPE => "loope",
            Mnemonic.LOOPNE => "loopne",

            Mnemonic.MOVSB => "movsb",
            Mnemonic.MOVSW => "movsw",
            Mnemonic.CMPSB => "cmpsb",
            Mnemonic.CMPSW => "cmpsw",
            Mnemonic.STOSB => "stosb",
            Mnemonic.STOSW => "stosw",
            Mnemonic.LODSB => "lodsb",
            Mnemonic.LODSW => "lodsw",
            Mnemonic.SCASB => "scasb",
            Mnemonic.SCASW => "scasw",

            Mnemonic.PUSHF => "pushf",
            Mnemonic.POPF => "popf",
            Mnemonic.SAHF => "sahf",
            Mnemonic.LAHF => "lahf",
            Mnemonic.STI => "sti",
            Mnemonic.CLI => "cli",
            Mnemonic.STD => "std",
            Mnemonic.CLD => "cld",

            Mnemonic.NOP => "nop",
            Mnemonic.HLT => "hlt",
            Mnemonic.INT => "int",
            Mnemonic.ENTER => "enter",
            Mnemonic.LEAVE => "leave",
            Mnemonic.IN => "in",
            Mnemonic.OUT => "out",

            Mnemonic.DAA => "daa",
            Mnemonic.DAS => "das",
            Mnemonic.AAA => "aaa",
            Mnemonic.AAS => "aas",
            Mnemonic.AAM => "aam",
            Mnemonic.AAD => "aad",
            Mnemonic.CBW => "cbw",
            Mnemonic.CWD => "cwd",
            Mnemonic.XLAT => "xlat",

            Mnemonic.DB => "db",

            _ => instruction.Mnemonic.ToString().ToLower()
        };

        public string ToColoredString()
        {
            string bytesStr = string.Join(" ", instruction.Bytes.Select(b => $"{b:X2}"));

            const string RESET = "\u001b[0m";
            const string GRAY = "\u001b[90m";
            const string RED = "\u001b[91m";
            const string GREEN = "\u001b[92m";
            const string YELLOW = "\u001b[93m";

            var instructionColor = YELLOW;
            // TODO instruction.GetColoredOperands()
            var operands = instruction.Operands;

            if (operands == Instruction.UnknownOperand)
                instructionColor = RED;
            else if (operands.Contains("ES") || operands.Contains("CS") || operands.Contains("SS") || operands.Contains("DS"))
                operands = GREEN + operands + RESET;

            var result = $"{GRAY}{instruction.Offset:X6}:{RESET} {GRAY}{bytesStr,-20}{RESET} {instructionColor}{instruction.MnemonicString,-5}{RESET} {operands}";

            if (!string.IsNullOrEmpty(instruction.Commentary))
                result = result + GRAY + "; " + instruction.Commentary + RESET;

            return result;
        }
    }

    extension(in Operand operand)
    {
        public GpRegister16 AsGpRegister16() => (GpRegister16)operand.Value;

        public GpRegister8 AsGpRegister8() => (GpRegister8)operand.Value;

        public CpuSegmentRegister AsCpuSegmentRegister() => (CpuSegmentRegister)operand.Value;

        public string ToAsm() => operand.Type switch
        {
            OperandType.Register8 or OperandType.Register16 => operand.GetRegName(),
            OperandType.Immediate8 => operand.Value.ToHex(),
            OperandType.Immediate16 => operand.FormatImageOffset(operand.Value),
            OperandType.Memory => operand.GetMemoryString(),
            OperandType.Relative8 or OperandType.Relative16 => operand.Value.ToHex(),
            OperandType.SegmentRegister => operand.GetSegRegName(),
            _ => "?"
        };

        private string FormatImageOffset(int value) =>
            operand.Relocation is not null ? $"{operand.Relocation}+{value.ToHex()}" : value.ToHex();

        private string GetRegName() => operand.Type switch
        {
            OperandType.Register8 => operand.Value switch
            {
                0 => "AL",
                1 => "CL",
                2 => "DL",
                3 => "BL",
                4 => "AH",
                5 => "CH",
                6 => "DH",
                7 => "BH",
                _ => "?"
            },
            OperandType.Register16 => operand.Value switch
            {
                0 => "AX",
                1 => "CX",
                2 => "DX",
                3 => "BX",
                4 => "SP",
                5 => "BP",
                6 => "SI",
                7 => "DI",
                _ => "?"
            },
            _ => "?"
        };

        private string GetSegRegName() => operand.Value switch
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
            if (operand.BaseReg != AddressRegister.None)
            {
                string baseName = operand.BaseReg switch
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
            if (operand.IndexReg != AddressRegister.None && operand.IndexReg != operand.BaseReg)
            {
                string idxName = operand.IndexReg switch
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
            if (operand.Value != 0)
            {
                parts.Add(operand.FormatImageOffset(operand.Value));
            }

            if (parts.Count == 0)
                return "[0]";

            return $"[{string.Join("+", parts)}]";
        }
    }

    extension(Segment segment)
    {
        /// <summary>Индекс сегментного регистра (0=ES … 3=DS) для <see cref="RegisterExpressions.GetSegment"/>.</summary>
        public CpuSegmentRegister ToCpuSegmentRegister() => segment switch
        {
            Segment.ES => CpuSegmentRegister.ES,
            Segment.CS => CpuSegmentRegister.CS,
            Segment.SS => CpuSegmentRegister.SS,
            Segment.DS => CpuSegmentRegister.DS,
            _ => throw new ArgumentOutOfRangeException(nameof(segment), segment, null)
        };

        public string ToPrefixString()
        {
            return segment switch
            {
                Segment.ES => "ES:",
                Segment.CS => "CS:",
                Segment.SS => "SS:",
                Segment.DS => "DS:",
                _ => ""
            };
        }
    }
}
