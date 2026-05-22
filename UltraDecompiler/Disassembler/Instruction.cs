namespace UltraDecompiler.Disassembler;

public class Instruction
{
    public const string UnknownOperand = "; unknown";

    /// <summary>
    /// Адрес инструкции
    /// </summary>
    public int Offset { get; set; }

    public byte[] Bytes { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Мнемоника инструкции
    /// </summary>
    public Mnemonic Mnemonic { get; set; } = Mnemonic.DB;

    /// <summary>
    /// Строковое представление мнемоники для вывода
    /// </summary>
    public string MnemonicString => GetMnemonicString();

    /// <summary>
    /// Строковое представление параметров
    /// </summary>
    public string Operands { get; set; } = "";

    /// <summary>
    /// Параметры инструкции
    /// </summary>
    public Operand[] OperandsInfo { get; set; } = Array.Empty<Operand>();

    /// <summary>
    /// Сегментный префикс инструкции (если есть)
    /// </summary>
    public Segment SegmentOverride { get; set; } = Segment.None;

    /// <summary>
    /// Инструкция является переходом
    /// </summary>
    public bool IsJump => Mnemonic is Mnemonic.JO
        or Mnemonic.JNO
        or Mnemonic.JB
        or Mnemonic.JAE
        or Mnemonic.JE
        or Mnemonic.JNE
        or Mnemonic.JBE
        or Mnemonic.JA
        or Mnemonic.JS
        or Mnemonic.JNS
        or Mnemonic.JP
        or Mnemonic.JNP
        or Mnemonic.JL
        or Mnemonic.JGE
        or Mnemonic.JLE
        or Mnemonic.JG
        or Mnemonic.JCXZ;

    private string GetMnemonicString()
    {
        return Mnemonic switch
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
            Mnemonic.SHL => "shl",
            Mnemonic.SHR => "shr",
            Mnemonic.SAR => "sar",
            Mnemonic.ROL => "rol",
            Mnemonic.ROR => "ror",
            Mnemonic.RCL => "rcl",
            Mnemonic.RCR => "rcr",

            Mnemonic.JMP => "jmp",
            Mnemonic.CALL => "call",
            Mnemonic.RET => "ret",
            Mnemonic.RETF => "retf",
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

            Mnemonic.LOCK => "lock",
            Mnemonic.REPZ => "repz",
            Mnemonic.REPNZ => "repnz",

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

            _ => Mnemonic.ToString().ToLower()
        };
    }

    /// <summary>
    /// Возвращает целевой адрес прямого перехода
    /// </summary>
    public int GetJumpTarget()
    {
        foreach (var op in OperandsInfo)
        {
            if (op.Type is OperandType.Relative8 or OperandType.Relative16)
                return op.Value;
        }
        return -1;
    }

    /// <summary>
    /// Вычисляет эффективный адрес перехода (для косвенных вызовов FF /2 и FF /4)
    /// </summary>
    public int GetEffectiveJumpTarget(byte[] image)
    {
        int direct = GetJumpTarget();
        if (direct != -1)
            return direct;

        if ((Mnemonic == Mnemonic.CALL || Mnemonic == Mnemonic.JMP) && OperandsInfo.Length > 0)
        {
            var op = OperandsInfo[0];
            if (op.Type == OperandType.Memory && op.Value >= 0 && op.Value + 2 <= image.Length)
            {
                return (ushort)(image[op.Value] | (image[op.Value + 1] << 8));
            }
        }

        return -1;
    }

    public override string ToString()
    {
        string bytesStr = string.Join(" ", Bytes.Select(b => $"{b:X2}"));
        return $"0x{Offset:X6}: {bytesStr,-20} {MnemonicString,-7} {Operands}";
    }

    public string ToColoredString()
    {
        string bytesStr = string.Join(" ", Bytes.Select(b => $"{b:X2}"));

        const string RESET = "\u001b[0m";
        const string GRAY = "\u001b[90m";
        const string RED = "\u001b[91m";
        const string GREEN = "\u001b[92m";
        const string YELLOW = "\u001b[93m";

        string coloredOperands = Operands;

        if (Operands.Contains("ES") || Operands.Contains("CS") || Operands.Contains("SS") || Operands.Contains("DS"))
            coloredOperands = GREEN + Operands + RESET;

        var instructionColor = YELLOW;
        if (Operands == UnknownOperand)
            instructionColor = RED;

        return $"{GRAY}0x{Offset:X6}:{RESET} {GRAY}{bytesStr,-20}{RESET} {instructionColor}{MnemonicString,-5}{RESET} {coloredOperands}";
    }
}