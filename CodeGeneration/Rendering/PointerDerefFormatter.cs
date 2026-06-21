using UltraDecompiler.Headers;

namespace UltraDecompiler.CodeGeneration.Rendering;

/// <summary>
/// Форматирует near-разыменование указателя как <c>*ptr</c> вместо <c>_psp:[ptr]</c>.
/// </summary>
public static class PointerDerefFormatter
{
    /// <summary>
    /// Пытается представить обращение к памяти как разыменование near-указателя (<c>*var</c>).
    /// </summary>
    public static bool TryFormatLoad(MemExpr mem, out string formatted)
    {
        formatted = string.Empty;

        if (FarPointerFormatter.TryFormatLoad(mem, out formatted))
        {
            return true;
        }

        if (CharPtrArrayFormatter.TryFormatCharPtrArrayElement(mem, out formatted, out _, out _))
        {
            return true;
        }

        if (CharPtrArrayFormatter.TryFormatCharPtrElement(mem, out formatted, out _, out _))
        {
            return true;
        }

        if (mem.Address is IncDecExpr incDec && TryFormatIncDecDeref(incDec, out formatted))
        {
            return true;
        }

        if (!TryGetNearPointerBase(mem, out var ptr))
        {
            return false;
        }

        formatted = $"*{ptr}";
        return true;
    }

    /// <summary>
    /// Проверяет, что MemExpr — near-разыменование указателя (DS/_psp + смещение в Variable).
    /// </summary>
    public static bool IsNearPointerDeref(MemExpr mem) =>
        TryGetNearPointerBase(mem, out _);

    /// <summary>
    /// Возвращает базовый указатель near-разыменования, если оно распознано.
    /// </summary>
    public static bool TryGetNearPointerBase(MemExpr mem, out Variable ptr)
    {
        ptr = null!;

        if (mem.Address is IncDecExpr incDec && TryGetIncDecPointerBase(incDec, out ptr))
        {
            return true;
        }

        if (mem.Address is not VariableExpr { Var: var variable } || IsSegmentBase(variable))
        {
            return false;
        }

        if (variable.Type?.IsCharPtrPtr == true)
        {
            return false;
        }

        if (!IsNearDataSegment(mem.Segment))
        {
            return false;
        }

        if (variable.Type is not null && variable.Type.Kind is CTypeKind.Pointer or CTypeKind.Int)
        {
            // ok
        }
        else if (variable.Type is not null && variable.Type.Kind != CTypeKind.Char)
        {
            return false;
        }

        ptr = variable;
        return true;
    }

    /// <summary>
    /// Сегмент near-данных в small-модели: DS, совпадающий с _psp, или неизвестен.
    /// </summary>
    public static bool IsNearDataSegment(Expr? segment) =>
        segment switch
        {
            null => true,
            VariableExpr { Var.Name: "_psp" } => true,
            _ => false,
        };

    private static bool IsSegmentBase(Variable variable) =>
        variable.Name is "_psp";

    private static bool TryGetIncDecPointerBase(IncDecExpr incDec, out Variable ptr)
    {
        ptr = null!;

        if (incDec.Operand is not VariableExpr { Var: var variable } || IsSegmentBase(variable))
        {
            return false;
        }

        if (variable.Type?.IsCharPtrPtr == true)
        {
            return false;
        }

        ptr = variable;
        return true;
    }

    private static bool TryFormatIncDecDeref(IncDecExpr incDec, out string formatted)
    {
        formatted = string.Empty;

        if (!TryGetIncDecPointerBase(incDec, out var ptr))
        {
            return false;
        }

        formatted = incDec.Kind switch
        {
            IncDecKind.PostInc => $"*{ptr}++",
            IncDecKind.PreInc => $"*++{ptr}",
            IncDecKind.PostDec => $"*{ptr}--",
            IncDecKind.PreDec => $"*--{ptr}",
            _ => string.Empty,
        };

        return formatted.Length > 0;
    }
}
