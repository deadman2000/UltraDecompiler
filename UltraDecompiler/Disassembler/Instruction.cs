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
    /// Префиксы
    /// </summary>
    public InstructionPrefix Prefix { get; set; }

    /// <summary>
    /// Мнемоника инструкции
    /// </summary>
    public Mnemonic Mnemonic { get; set; } = Mnemonic.DB;

    /// <summary>
    /// Строковое представление мнемоники для вывода
    /// </summary>
    public string MnemonicString => this.GetPrefix() + this.GetMnemonicString();

    private string _operands = "";

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

            string result = ops.Count > 0 ? string.Join(", ", ops) : _operands;

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
                if (!result.StartsWith(seg))
                    result = seg + result;
            }

            return result;
        }
        set => _operands = value;
    }

    /// <summary>
    /// Первый операнд (dst)
    /// </summary>
    public Operand Operand1 { get; set; }

    /// <summary>
    /// Второй операнд (src)
    /// </summary>
    public Operand Operand2 { get; set; }

    /// <summary>
    /// Сегментный префикс инструкции (если есть)
    /// </summary>
    public Segment Segment { get; set; } = Segment.None;

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
            // TODO в каждой инструкции надо запоминать значения регистров, если они установлены константами
            // return Mnemonic is Mnemonic.INT && Operand1.Value == 0x21 && AH == 4c;
            return true;
        }
    }

    /// <summary>
    /// Размер инструкции.
    /// </summary>
    public int Size => Bytes.Length;

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
        return $"{Offset:X6}: {bytesStr,-20} {MnemonicString,-7} {Operands}";
    }
}