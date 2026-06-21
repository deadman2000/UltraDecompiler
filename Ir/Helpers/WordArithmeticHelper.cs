namespace UltraDecompiler.Ir.Helpers;

/// <summary>
/// Нормализация 16-битной арифметики QuickC: <c>add x, 0FFFFh</c> ≡ <c>x - 1</c>.
/// </summary>
public static class WordArithmeticHelper
{
    /// <summary>
    /// Нормализует <c>local = local ± imm</c> из цепочки mov/add/mov QuickC (в т.ч. <c>add ax, 0FFFBh</c>).
    /// </summary>
    public static bool TryNormalizeSelfAssignMath(Variable local, Math2Expr math, bool isWord, out Math2Expr normalized)
    {
        normalized = null!;

        if (math.First is not VariableExpr { Var: var first } || !ReferenceEquals(first, local) || math.Second is not ConstExpr imm)
        {
            return false;
        }

        var signed = isWord
            ? math.Operation == Math2Operation.Add ? (short)imm.Value : -(short)imm.Value
            : math.Operation == Math2Operation.Add ? (sbyte)(byte)imm.Value : -(sbyte)(byte)imm.Value;

        if (signed == 0)
        {
            return false;
        }

        normalized = signed > 0
            ? new Math2Expr(Math2Operation.Add, local.ToGet(), new ConstExpr((ushort)signed))
            : new Math2Expr(Math2Operation.Sub, local.ToGet(), new ConstExpr((ushort)(-signed)));
        return true;
    }
}