namespace UltraDecompiler.Decompilation;

/// <summary>
/// Нормализация 16-битной арифметики QuickC: <c>add x, 0FFFFh</c> ≡ <c>x - 1</c>.
/// </summary>
internal static class WordArithmeticHelper
{
    /// <summary>
    /// Возвращает true, если инструкция работает с 16-битным операндом (word).
    /// </summary>
    public static bool IsWordOperation(Instruction instr) =>
        instr.Bytes.Length > 0 && (instr.Bytes[0] & 1) == 1;

    /// <summary>
    /// Преобразует ADD/SUB с малым непосредственным операндом в знаковый шаг ±1.
    /// </summary>
    public static bool TryGetSignedDelta(ConstExpr immediate, bool isAdd, bool isWord, out int delta)
    {
        delta = 0;
        var value = immediate.Value;

        if (isWord)
        {
            if (isAdd)
            {
                if (value == 1)
                {
                    delta = 1;
                    return true;
                }

                if (value == 0xFFFF)
                {
                    delta = -1;
                    return true;
                }
            }
            else
            {
                if (value == 1)
                {
                    delta = -1;
                    return true;
                }

                if (value == 0xFFFF)
                {
                    delta = 1;
                    return true;
                }
            }
        }
        else
        {
            if (isAdd)
            {
                if (value == 1)
                {
                    delta = 1;
                    return true;
                }

                if (value == 0xFF)
                {
                    delta = -1;
                    return true;
                }
            }
            else
            {
                if (value == 1)
                {
                    delta = -1;
                    return true;
                }

                if (value == 0xFF)
                {
                    delta = 1;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Применяет знаковый шаг ±1 к выражению.
    /// </summary>
    public static Expr ApplySignedDelta(Expr expr, int delta) =>
        delta switch
        {
            1 => expr.Calculate(Math2Operation.Add, ConstExpr.One),
            -1 => expr.Calculate(Math2Operation.Sub, ConstExpr.One),
            _ => throw new ArgumentOutOfRangeException(nameof(delta), delta, "Ожидается ±1."),
        };

    /// <summary>
    /// QuickC для <c>a++</c>/<c>a--</c> иногда генерирует <c>add/sub [mem], 1</c> (не INC/DEC).
    /// </summary>
    public static bool IsMemoryIncDec(Instruction instr, ConstExpr immediate, bool isAdd, bool isWord, out bool isIncrement)
    {
        isIncrement = false;

        if (instr.Operand1.Type != OperandType.Memory || !isWord)
        {
            return false;
        }

        if (isAdd && immediate.Value == 1)
        {
            isIncrement = true;
            return true;
        }

        if (!isAdd && immediate.Value == 1)
        {
            isIncrement = false;
            return true;
        }

        return false;
    }
}
