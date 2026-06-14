namespace UltraDecompiler.Ir.Variables;

/// <summary>
/// Вывод знаковости целочисленных переменных по контексту инструкций x86.
/// </summary>
public static class VariableSignedness
{
    /// <summary>Помечает переменные в выражении как знаковый <c>int</c>.</summary>
    public static void MarkSigned(Expr? expr)
    {
        Apply(expr, CType.Int);
    }

    /// <summary>Помечает переменные в выражении как беззнаковый <c>unsigned</c>.</summary>
    public static void MarkUnsigned(Expr? expr)
    {
        Apply(expr, CType.UnsignedInt);
    }

    /// <summary>Помечает переменные в выражении как знаковый <c>char</c> (8 бит).</summary>
    public static void MarkChar(Expr? expr)
    {
        Apply(expr, CType.Char);
    }

    /// <summary>
    /// Можно ли применить тип к переменной с уже известным типом.
    /// </summary>
    public static bool CanApplyType(CType? current, CType inferred) => ShouldApplyType(current, inferred);

    /// <summary>
    /// Устанавливает тип переменной с учётом уже известного типа (указатели и конфликты не перезаписываются).
    /// </summary>
    public static bool TrySetType(Variable variable, CType type)
    {
        if (!ShouldApplyType(variable.Type, type))
        {
            return false;
        }

        if (variable.Type == type)
        {
            return false;
        }

        variable.Type = type;
        return true;
    }

    private static void Apply(Expr? expr, CType type)
    {
        if (expr is null)
        {
            return;
        }

        foreach (var variable in ExprSubstitution.CollectVariables(expr))
        {
            TrySetType(variable, type);
        }
    }

    private static bool ShouldApplyType(CType? current, CType inferred)
    {
        if (current == inferred)
        {
            return false;
        }

        if (current is not null && IsPointerLike(current))
        {
            return false;
        }

        if (current is not null && IsSignedInteger(current) && inferred.Kind == CTypeKind.Unsigned)
        {
            return false;
        }

        if (current?.Kind == CTypeKind.Unsigned && IsSignedInteger(inferred))
        {
            return false;
        }

        if (current?.Kind == CTypeKind.Char && inferred.Kind == CTypeKind.Int)
        {
            return false;
        }

        return true;
    }

    private static bool IsPointerLike(CType type) =>
        type.Kind == CTypeKind.Pointer || type.IsCharPtr || type.IsVoidPtr;

    private static bool IsSignedInteger(CType type) =>
        type.Kind is CTypeKind.Int or CTypeKind.Char or CTypeKind.Long;

    /// <summary>Условный переход сравнивает операнды как беззнаковые (JB, JA, …).</summary>
    public static bool IsUnsignedConditionalJump(Mnemonic mnemonic) =>
        mnemonic is Mnemonic.JB or Mnemonic.JAE or Mnemonic.JBE or Mnemonic.JA;

    /// <summary>Условный переход сравнивает операнды как знаковые (JL, JG, JS, …).</summary>
    public static bool IsSignedConditionalJump(Mnemonic mnemonic) =>
        mnemonic is Mnemonic.JL or Mnemonic.JGE or Mnemonic.JLE or Mnemonic.JG
            or Mnemonic.JS or Mnemonic.JNS;
}
