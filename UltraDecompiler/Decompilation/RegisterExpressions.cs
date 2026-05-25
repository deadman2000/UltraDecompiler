namespace UltraDecompiler.Decompilation;

/// <summary>
/// Выражения в регистрах
/// </summary>
public record struct RegisterExpressions(Expr AX, Expr BX, Expr CX, Expr DX)
{
    public static RegisterExpressions InitZero()
    {
        return new RegisterExpressions(new ConstExpr(0), new ConstExpr(0), new ConstExpr(0), new ConstExpr(0));
    }
}