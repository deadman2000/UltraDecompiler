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
    public Segment Segment { get; set; } = Segment.None;

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
}