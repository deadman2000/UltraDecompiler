namespace UltraDecompiler.Decompilation;

/// <summary>
/// 32-битное значение <c>long</c> (QuickC / MSC: младшее слово + старшее слово).
/// </summary>
/// <param name="Low">Младшие 16 бит (AX / первое слово на стеке).</param>
/// <param name="High">Старшие 16 бит (DX / второе слово на стеке).</param>
public sealed record LongExpr(Expr Low, Expr High) : Expr
{
    /// <summary>Собирает <see cref="LongExpr"/> из пары 16-битных слов.</summary>
    public static LongExpr FromWords(Expr low, Expr high) => new(low, high);

    /// <summary>Собирает <see cref="LongExpr"/> из одной переменной типа <c>long</c>.</summary>
    public static LongExpr FromVariable(Variable variable) => new(variable, ConstExpr.Zero);

    public override int GetPrecedence() => Prec.Atom;

    public override string ToString() => ToString(0);

    public override string ToString(int parentPrec)
    {
        if (TryFormatLiteral(out var literal))
        {
            return literal;
        }

        if (Low is Variable { Type.Kind: CTypeKind.Long } variable
            && High is ConstExpr { Value: 0 })
        {
            return variable.ToString();
        }

        var lowStr = Low.ToString(GetPrecedence());
        var highStr = High.ToString(GetPrecedence());
        return $"(({lowStr}) | ((long)({highStr}) << 16))";
    }

    /// <summary>Пробует сформировать литерал <c>1234L</c> / <c>0x1234L</c>.</summary>
    public bool TryFormatLiteral(out string literal)
    {
        literal = string.Empty;
        if (Low is not ConstExpr lowConst || High is not ConstExpr highConst)
        {
            return false;
        }

        var unsigned = (uint)(ushort)lowConst.Value | ((uint)(ushort)highConst.Value << 16);
        if (highConst.Value == 0 && (ushort)lowConst.Value == lowConst.Value)
        {
            literal = FormatUnsignedLiteral(unsigned);
            return true;
        }

        var signed = (int)unsigned;
        literal = FormatSignedLiteral(signed);
        return true;
    }

    private static string FormatUnsignedLiteral(uint value) =>
        value <= 9 ? $"{value}L" : $"0x{value:X}L";

    private static string FormatSignedLiteral(int value) =>
        value >= 0 && value <= 9 ? $"{value}L" : $"0x{(uint)value:X}L";
}
