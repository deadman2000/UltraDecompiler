using System.Diagnostics;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Выражения в регистрах (символическое состояние для декомпиляции).
/// 
/// Для 16-битных gp-регистров (AX/BX/CX/DX) и их 8-битных половин (AH/AL и т.д.) поддерживается
/// две канонические формы хранения:
///   1. X != null, H = L = null   — последнее присвоение было 16-битным (полное выражение).
///   2. X = null, H != null, L != null — последнее присвоение было через 8-битные регистры.
/// 
/// Инварианты (охраняются Debug.Assert в WithGroup + проверки в Get):
///   - Никогда не все три (X/H/L) null в группе.
///   - Никогда не (X != null && H/L установлены одновременно).
/// 
/// При Set/Get 8-битных регистров автоматически выполняются правила "split/merge":
///   Set AX → H=L=null;   Set AH (когда X) → AL=Low(X);   Get AX (когда X=null) → (H<<8)|L и т.д.
/// </summary>
public record struct RegisterExpressions(
    Expr? AX, Expr? BX, Expr? CX, Expr? DX,
    Expr SP, Expr BP, Expr SI, Expr DI,
    Expr ES, Expr CS, Expr SS, Expr DS)
{
    public Expr? AH { get; init; }
    public Expr? AL { get; init; }
    public Expr? BH { get; init; }
    public Expr? BL { get; init; }
    public Expr? CH { get; init; }
    public Expr? CL { get; init; }
    public Expr? DH { get; init; }
    public Expr? DL { get; init; }

    // Флаги (символические выражения). null = неизвестен/не вычислен.
    public Expr? ZF { get; init; }
    public Expr? CF { get; init; }
    public Expr? SF { get; init; }
    public Expr? OF { get; init; }

    // ===== Хелперы для сокращения дублирования логики 4 групп (AX/AH/AL, CX/CH/CL и т.д.) =====

    private static int Reg8ToGroup(int reg8) => reg8 switch
    {
        0 or 4 => 0, // AL/AH -> AX
        1 or 5 => 1, // CL/CH -> CX
        2 or 6 => 2, // DL/DH -> DX
        3 or 7 => 3, // BL/BH -> BX
        _ => -1
    };

    private static int Reg16ToGroup(int reg16) => reg16 is >= 0 and <= 3 ? reg16 : -1;

    private static bool IsHighReg8(int reg8) => reg8 >= 4;

    private readonly (Expr? X, Expr? H, Expr? L) GetGroup(int group) => group switch
    {
        0 => (AX, AH, AL),
        1 => (CX, CH, CL),
        2 => (DX, DH, DL),
        3 => (BX, BH, BL),
        _ => (null, null, null)
    };

    private RegisterExpressions WithGroup(int group, Expr? x, Expr? h, Expr? l)
    {
        Debug.Assert(IsValidGroupState(x, h, l),
            $"Invalid register group {group} state: X={x}, H={h}, L={l}");
        return group switch
        {
            0 => this with { AX = x, AH = h, AL = l },
            1 => this with { CX = x, CH = h, CL = l },
            2 => this with { DX = x, DH = h, DL = l },
            3 => this with { BX = x, BH = h, BL = l },
            _ => this
        };
    }

    private static bool IsValidGroupState(Expr? x, Expr? h, Expr? l) =>
        (x != null && h == null && l == null) ||
        (x == null && h != null && l != null);

    // ===== Инициализация =====

    public static RegisterExpressions InitZero()
    {
        var zero = ConstExpr.Zero;
        return new RegisterExpressions(zero, zero, zero, zero, zero, zero, zero, zero, zero, zero, zero, zero);
    }

    public static RegisterExpressions InitCom(VariableStorage variables)
    {
        var psp = variables.PspBase;   // каноническая база PSP

        var zero = ConstExpr.Zero;
        return new RegisterExpressions(AX: zero,
                                       BX: zero,
                                       CX: zero,
                                       DX: zero,
                                       SP: new ConstExpr(0xfffe),
                                       BP: zero,
                                       SI: zero,
                                       DI: zero,
                                       ES: psp,
                                       CS: psp,
                                       SS: psp,
                                       DS: psp);
    }

    public static RegisterExpressions InitExe(VariableStorage variables)
    {
        var psp = variables.PspBase;   // каноническая база PSP
        var initCS = variables.CreateVariable("_initCS");
        var initSS = variables.CreateVariable("_initSS");
        var initSP = variables.CreateVariable("_initSP");

        var zero = ConstExpr.Zero;
        return new RegisterExpressions(AX: zero,
                                       BX: zero,
                                       CX: zero,
                                       DX: zero,
                                       SP: initSP,
                                       BP: zero,
                                       SI: zero,
                                       DI: zero,
                                       ES: psp,
                                       CS: initCS,
                                       SS: initSS,
                                       DS: psp);
    }

    /// <summary>
    /// Установка 16-битного регистра: сбрасывает H и L в null.
    /// Для gp-регистров (0-3) переводит группу в состояние (X=expr, H=null, L=null).
    /// </summary>
    public RegisterExpressions Set16(int reg16, Expr expr)
    {
        int group = Reg16ToGroup(reg16);
        if (group >= 0)
        {
            return WithGroup(group, expr, null, null);
        }
        return reg16 switch
        {
            4 => this with { SP = expr },
            5 => this with { BP = expr },
            6 => this with { SI = expr },
            7 => this with { DI = expr },
            _ => this
        };
    }

    private static Expr LowByte(Expr e)
    {
        if (e is ConstExpr c)
            return new ConstExpr(c.Value & 0xff);
        return new Math2Expr(Math2Operation.And, e, new ConstExpr(0xff));
    }

    private static Expr HighByte(Expr e)
    {
        if (e is ConstExpr c)
            return new ConstExpr(c.Value >> 8);
        return new Math2Expr(Math2Operation.Shr, e, new ConstExpr(8));
    }

    /// <summary>
    /// Установка 8-битного регистра.
    /// Точная логика (по ТЗ):
    /// - если AX (или CX..) != null, то при установке AH: AL = AX &amp; 0xFF; при установке AL: AH = AX >> 8.
    /// - X всегда сбрасывается в null, устанавливаются оба байта (H и L).
    /// - Если X == null — просто обновляем нужный байт, второй оставляем как есть (предполагается, что он был).
    /// </summary>
    public RegisterExpressions Set8(int reg8, Expr expr)
    {
        int group = Reg8ToGroup(reg8);
        if (group < 0) return this;

        bool isHigh = IsHighReg8(reg8);
        var (x, oldH, oldL) = GetGroup(group);

        Expr? newH;
        Expr? newL;
        if (x != null)
        {
            // Правило: при присвоении AH/AL, если есть X — вычисляем "другой" байт из X
            Expr other = isHigh ? LowByte(x) : HighByte(x);
            newH = isHigh ? expr : other;
            newL = !isHigh ? expr : other;
        }
        else
        {
            // X == null: оставляем второй байт как был (GetH/GetL)
            newH = isHigh ? expr : oldH;
            newL = !isHigh ? expr : oldL;
        }

        // WithGroup выполнит assert IsValidGroupState (X=null + оба bytes != null)
        return WithGroup(group, null, newH, newL);
    }

    /// <summary>
    /// Получение 16-битного как выражение.
    /// Если X != null — возвращаем его.
    /// Если X == null — собираем (H << 8) | L (если оба байта установлены).
    /// При нарушении инварианта (все null) — assert + исключение.
    /// </summary>
    public readonly Expr Get16(int reg16)
    {
        int group = Reg16ToGroup(reg16);
        if (group >= 0)
        {
            var (x, h, l) = GetGroup(group);

            if (h != null && l != null)
            {
                // Собираем выражение из байтов (правило "если AX null — объединяем AL и AH")
                Expr shl = h is ConstExpr ch
                    ? new ConstExpr(ch.Value << 8)
                    : new Math2Expr(Math2Operation.Shl, h, new ConstExpr(8));
                Expr combined = (shl is ConstExpr cshl && l is ConstExpr cl)
                    ? new ConstExpr(cshl.Value | cl.Value)
                    : new Math2Expr(Math2Operation.Or, shl, l);
                return combined;
            }

            if (x != null)
                return x;

            // Недопустимое состояние: X null и не оба байта
            Debug.Assert(false, $"Group {group} is in invalid state (all null or partial bytes) in Get16");
            throw new InvalidOperationException($"Register group {group} has no value (X/H/L all effectively null)");
        }
        return reg16 switch
        {
            4 => SP,
            5 => BP,
            6 => SI,
            7 => DI,
            _ => ConstExpr.Zero
        };
    }

    /// <summary>
    /// Получение 8-битного выражения.
    /// Если байт (H или L) установлен — возвращаем его.
    /// Если байт null, но есть X — вычисляем HighByte/LowByte(X).
    /// При нарушении инварианта — assert + исключение.
    /// </summary>
    public readonly Expr Get8(int reg8)
    {
        int group = Reg8ToGroup(reg8);
        if (group < 0) return ConstExpr.Zero;

        bool isHigh = IsHighReg8(reg8);
        var (x, h, l) = GetGroup(group);

        Expr? b = isHigh ? h : l;
        if (b != null) return b;

        // Правило: "при получении AH/AL, если они null — вычисляем из AX"
        if (x != null)
            return isHigh ? HighByte(x) : LowByte(x);

        // Недопустимое состояние
        Debug.Assert(false, $"Group {group} is in invalid state in Get8 (no byte and no X)");
        throw new InvalidOperationException($"Register group {group} has no value for 8-bit access");
    }

    /// <summary>
    /// Установка сегментного регистра (ES=0, CS=1, SS=2, DS=3)
    /// </summary>
    public RegisterExpressions SetSegment(int sreg, Expr expr)
    {
        return sreg switch
        {
            0 => this with { ES = expr },
            1 => this with { CS = expr },
            2 => this with { SS = expr },
            3 => this with { DS = expr },
            _ => this
        };
    }

    /// <summary>
    /// Получение сегментного регистра
    /// </summary>
    public readonly Expr GetSegment(int sreg)
    {
        return sreg switch
        {
            0 => ES,
            1 => CS,
            2 => SS,
            3 => DS,
            _ => ConstExpr.Zero
        };
    }
}