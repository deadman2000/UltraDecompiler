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
    /// Размер инструкции.
    /// </summary>
    public int Size => Bytes.Length;

    /// <summary>
    /// Префиксы
    /// </summary>
    public InstructionPrefix Prefix { get; set; }

    /// <summary>
    /// Мнемоника инструкции
    /// </summary>
    public Mnemonic Mnemonic { get; set; } = Mnemonic.DB;

    /// <summary>
    /// Известные значения регистров
    /// </summary>
    public RegisterState Registers { get; set; }

    /// <summary>
    /// Строковое представление мнемоники для вывода
    /// </summary>
    public string MnemonicString => this.GetPrefix() + this.GetMnemonicString();

    /// <summary>
    /// Сегментный префикс инструкции (если есть)
    /// </summary>
    public Segment Segment { get; set; } = Segment.None;

    /// <summary>
    /// Первый операнд (dst)
    /// </summary>
    public Operand Operand1 { get; set; }

    /// <summary>
    /// Второй операнд (src)
    /// </summary>
    public Operand Operand2 { get; set; }

    /// <summary>
    /// Строковое представление параметров (вычисляемое)
    /// </summary>
    public string Operands
    {
        get
        {
            var ops = new List<string>();
            if (Operand1.Type != OperandType.None) ops.Add(Operand1.ToString() ?? UnknownOperand);
            if (Operand2.Type != OperandType.None) ops.Add(Operand2.ToString() ?? UnknownOperand);

            string result = ops.Count > 0 ? string.Join(", ", ops) : "";

            // Добавляем сегментный префикс если есть
            if (Segment != Segment.None && !string.IsNullOrEmpty(result))
            {
                string seg = Segment switch
                {
                    Segment.ES => "ES:",
                    Segment.CS => "CS:",
                    Segment.SS => "SS:",
                    Segment.DS => "DS:",
                    _ => ""
                };
                result = seg + result;
            }

            return result;
        }
    }

    public string? Commentary { get; set; }

    /// <summary>
    /// Инструкция является условным переходом
    /// </summary>
    public bool IsConditionalJump => Mnemonic
        is Mnemonic.JO
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
        or Mnemonic.JCXZ
        or Mnemonic.LOOP
        or Mnemonic.LOOPE
        or Mnemonic.LOOPNE;

    /// <summary>
    /// Инструкция является безусловным переходом
    /// </summary>
    public bool IsUnconditionalJump => Mnemonic is Mnemonic.JMP or Mnemonic.JMP_FAR;

    /// <summary>
    /// Инструкция является переходом
    /// </summary>
    public bool IsReturn => Mnemonic is Mnemonic.RET or Mnemonic.RETF or Mnemonic.IRET;

    /// <summary>
    /// Инструкция является вызовом с возвратом
    /// </summary>
    public bool IsCall => Mnemonic is Mnemonic.CALL or Mnemonic.CALL_FAR;

    /// <summary>
    /// Инструкция является выходом из приложения
    /// </summary>
    public bool IsExit
    {
        get
        {
            if (Mnemonic is Mnemonic.INT)
            {
                if (Operand1.Value == 0x20 || Operand1.Value == 0x27)
                    return true;

                if (Operand1.Value == 0x21)
                {
                    if (Registers.AH == 0 || Registers.AH == 0x4C || Registers.AH == 0x31)
                        return true;
                }
            }

            return false;
        }
    }

    public RegisterState ApplyRegisters(RegisterState state)
    {
        Registers = ModifyRegisters(state);
        return Registers;
    }

    private RegisterState ModifyRegisters(RegisterState state)
        => Mnemonic switch
        {
            Mnemonic.MOV => ModifyRegistersMov(state),
            // TODO остальные кейсы
            _ => state,
        };

    private RegisterState ModifyRegistersMov(RegisterState state)
    {
        // TODO остальные кейсы
        if (Operand1.Type == OperandType.Register8 && Operand2.Type == OperandType.Immediate8)
        {
            return Operand1.Value switch
            {
                0 => state with { AL = (byte)Operand2.Value },
                1 => state with { CL = (byte)Operand2.Value },
                2 => state with { DL = (byte)Operand2.Value },
                3 => state with { BL = (byte)Operand2.Value },
                4 => state with { AH = (byte)Operand2.Value },
                5 => state with { CH = (byte)Operand2.Value },
                6 => state with { DH = (byte)Operand2.Value },
                7 => state with { BH = (byte)Operand2.Value },
                _ => state
            };
        }
        if (Operand1.Type == OperandType.Register16 && Operand2.Type == OperandType.Immediate16)
        {
            ushort val = (ushort)Operand2.Value;
            byte low = (byte)val;
            byte high = (byte)(val >> 8);
            return Operand1.Value switch
            {
                0 => state with { AL = low, AH = high }, // AX
                1 => state with { CL = low, CH = high }, // CX
                2 => state with { DL = low, DH = high }, // DX
                3 => state with { BL = low, BH = high }, // BX
                4 => state with { SP = val }, // SP
                5 => state with { BP = val }, // BP
                6 => state with { SI = val }, // SI
                7 => state with { DI = val }, // DI
                _ => state
            };
        }

        // Поддержка MOV reg, reg (копирование известных значений)
        if (Operand1.Type == OperandType.Register8 && Operand2.Type == OperandType.Register8)
        {
            byte? srcVal = Operand2.Value switch
            {
                0 => state.AL,
                1 => state.CL,
                2 => state.DL,
                3 => state.BL,
                4 => state.AH,
                5 => state.CH,
                6 => state.DH,
                7 => state.BH,
                _ => null
            };

            return Operand1.Value switch
            {
                0 => state with { AL = srcVal },
                1 => state with { CL = srcVal },
                2 => state with { DL = srcVal },
                3 => state with { BL = srcVal },
                4 => state with { AH = srcVal },
                5 => state with { CH = srcVal },
                6 => state with { DH = srcVal },
                7 => state with { BH = srcVal },
                _ => state
            };
        }

        if (Operand1.Type == OperandType.Register16 && Operand2.Type == OperandType.Register16)
        {
            ushort? srcVal = Operand2.Value switch
            {
                0 => state.AX,
                1 => state.CX,
                2 => state.DX,
                3 => state.BX,
                4 => state.SP,
                5 => state.BP,
                6 => state.SI,
                7 => state.DI,
                _ => null
            };
            if (srcVal.HasValue)
            {
                return Operand1.Value switch
                {
                    0 => state with { AL = (byte)srcVal.Value, AH = (byte)(srcVal.Value >> 8) },
                    1 => state with { CL = (byte)srcVal.Value, CH = (byte)(srcVal.Value >> 8) },
                    2 => state with { DL = (byte)srcVal.Value, DH = (byte)(srcVal.Value >> 8) },
                    3 => state with { BL = (byte)srcVal.Value, BH = (byte)(srcVal.Value >> 8) },
                    4 => state with { SP = srcVal.Value },
                    5 => state with { BP = srcVal.Value },
                    6 => state with { SI = srcVal.Value },
                    7 => state with { DI = srcVal.Value },
                    _ => state
                };
            }
            else
            {
                // При присвоении неизвестного значения, результат становится неизвестным
                return Operand1.Value switch
                {
                    0 => state with { AL = null, AH = null },
                    1 => state with { CL = null, CH = null },
                    2 => state with { DL = null, DH = null },
                    3 => state with { BL = null, BH = null },
                    4 => state with { SP = null },
                    5 => state with { BP = null },
                    6 => state with { SI = null },
                    7 => state with { DI = null },
                    _ => state
                };
            }
        }

        return state;
    }

    /// <summary>
    /// Возвращает целевой адрес прямого перехода
    /// </summary>
    public int GetJumpTarget()
    {
        if (Operand1.Type is OperandType.Relative8 or OperandType.Relative16)
            return Operand1.Value;
        if (Operand2.Type is OperandType.Relative8 or OperandType.Relative16)
            return Operand2.Value;
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

        var op = Operand1.IsSet ? Operand1 : Operand2;
        if ((Mnemonic == Mnemonic.CALL || Mnemonic == Mnemonic.JMP) && op.Type == OperandType.Memory)
        {
            int val = op.Value;
            if (val >= 0 && val + 2 <= image.Length)
            {
                return (ushort)(image[val] | (image[val + 1] << 8));
            }
        }

        return -1;
    }

    public override string ToString()
    {
        string bytesStr = string.Join(" ", Bytes.Select(b => $"{b:X2}"));
        var result = $"{Offset:X6}: {bytesStr,-20} {MnemonicString,-7} {Operands}";
        if (!string.IsNullOrEmpty(Commentary))
            result = result + "; " + Commentary;

        return result;
    }
}