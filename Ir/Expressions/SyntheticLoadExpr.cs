namespace UltraDecompiler.Ir.Expressions;

/// <summary>Синтетическое выражение с готовым C-текстом (результат переписывания MemExpr).</summary>
public sealed record SyntheticLoadExpr(string Text, Variable? Array = null, Variable? Index = null) : Expr
{
    public override string ToString() => Text;

    public override string ToString(int parentPrec) => Text;
}
