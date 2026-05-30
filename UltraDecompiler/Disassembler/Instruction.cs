namespace UltraDecompiler.Disassembler;

using System;

public partial class Instruction
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
    /// Прямой far-переход/вызов с непосредственным указателем seg:off (9Ah/EAh).
    /// </summary>
    public bool IsDirectFarPointer =>
        Mnemonic is Mnemonic.CALL_FAR or Mnemonic.JMP_FAR
        && Operand1.Type == OperandType.Immediate16
        && Operand2.Type == OperandType.Immediate16;

    /// <summary>
    /// Far ptr из .LIB (Pointer32 FIXUPP): нулевой seg:off, имя на IP-половине указателя.
    /// </summary>
    internal static bool IsSymbolicFarPointer(in Operand segment, in Operand offset) =>
        offset.Relocation is not null
        && offset.Value == 0
        && segment.Relocation is null
        && segment.Value == 0;

    internal static string FormatDirectFarPointerOperands(in Operand segment, in Operand offset) =>
        IsSymbolicFarPointer(segment, offset)
            ? offset.Relocation!
            : $"{segment}:{offset}";

    /// <summary>
    /// Строковое представление параметров (вычисляемое)
    /// </summary>
    public string Operands
    {
        get
        {
            if (IsDirectFarPointer)
                return FormatDirectFarPointerOperands(Operand2, Operand1);

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
    public bool IsCall => Mnemonic is Mnemonic.CALL or Mnemonic.CALL_FAR or Mnemonic.INT;

    /// <summary>
    /// Инструкция имеет REP/REPE/REPZ или REPNE/REPNZ префикс
    /// </summary>
    public bool HasRepPrefix => Prefix.HasFlag(InstructionPrefix.REPZ) ||
                                Prefix.HasFlag(InstructionPrefix.REPNZ);

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