using System.Diagnostics.CodeAnalysis;

namespace UltraDecompiler.Ir.Variables;

/// <summary>
/// Анализ левой части <see cref="Operations.SetOperation"/>.
/// </summary>
public static class AssignmentTarget
{
    /// <summary>Возвращает переменную, если назначение — простое <c>var = …</c>.</summary>
    public static bool TryGetVariable(Expr dst, [NotNullWhen(true)] out Variable? variable)
    {
        if (dst is Variable v)
        {
            variable = v;
            return true;
        }

        variable = null;
        return false;
    }

    /// <summary>Проверяет, что левая часть — присваивание в указанную переменную.</summary>
    public static bool ReferencesVariable(Expr dst, Variable variable) =>
        TryGetVariable(dst, out var target) && ReferenceEquals(target, variable);

    /// <summary>
    /// Переменная переопределяется присваиванием (включая побочные эффекты <c>*ptr++ = …</c>).
    /// </summary>
    public static bool DefinesVariable(Expr dst, Variable variable)
    {
        if (ReferencesVariable(dst, variable))
        {
            return true;
        }

        return dst switch
        {
            MemExpr { Address: IncDecExpr inc } => ReferencesIncDecOperand(inc, variable),
            IncDecExpr inc => ReferencesIncDecOperand(inc, variable),
            _ => false,
        };
    }

    private static bool ReferencesIncDecOperand(IncDecExpr inc, Variable variable) =>
        inc.Operand is Variable operand && ReferenceEquals(operand, variable);
}
