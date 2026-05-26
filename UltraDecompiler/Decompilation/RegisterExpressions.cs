namespace UltraDecompiler.Decompilation;

/// <summary>
/// Выражения в регистрах
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

    public static RegisterExpressions InitZero()
    {
        var zero = ConstExpr.Zero;
        return new RegisterExpressions(zero, zero, zero, zero, zero, zero, zero, zero, zero, zero, zero, zero)
        {
            AH = zero,
            AL = zero,
            BH = zero,
            BL = zero,
            CH = zero,
            CL = zero,
            DH = zero,
            DL = zero,
        };
    }

    public static RegisterExpressions InitCom(VariableStorage variables)
    {
        var initCS = variables.CreateVariable("_initCS");

        var zero = ConstExpr.Zero;
        return new RegisterExpressions(AX: zero,
                                       BX: zero,
                                       CX: zero,
                                       DX: zero,
                                       SP: new ConstExpr(0xfffe),
                                       BP: zero,
                                       SI: zero,
                                       DI: zero,
                                       ES: initCS,
                                       CS: initCS,
                                       SS: initCS,
                                       DS: initCS)
        {
            AH = zero,
            AL = zero,
            BH = zero,
            BL = zero,
            CH = zero,
            CL = zero,
            DH = zero,
            DL = zero,
        };
    }

    public static RegisterExpressions InitExe(VariableStorage variables)
    {
        var psp = variables.CreateVariable("_psp");
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
                                       DS: psp)
        {
            AH = zero,
            AL = zero,
            BH = zero,
            BL = zero,
            CH = zero,
            CL = zero,
            DH = zero,
            DL = zero,
        };
    }

    private readonly Expr? GetX(int group) => group switch
    {
        0 => AX,
        1 => CX,
        2 => DX,
        3 => BX,
        _ => ConstExpr.Zero
    };

    private readonly Expr? GetH(int group) => group switch
    {
        0 => AH,
        1 => CH,
        2 => DH,
        3 => BH,
        _ => null
    };

    private readonly Expr? GetL(int group) => group switch
    {
        0 => AL,
        1 => CL,
        2 => DL,
        3 => BL,
        _ => null
    };

    private RegisterExpressions SetX(int group, Expr? expr) => group switch
    {
        0 => this with { AX = expr ?? ConstExpr.Zero, AH = null, AL = null },
        1 => this with { CX = expr ?? ConstExpr.Zero, CH = null, CL = null },
        2 => this with { DX = expr ?? ConstExpr.Zero, DH = null, DL = null },
        3 => this with { BX = expr ?? ConstExpr.Zero, BH = null, BL = null },
        _ => this
    };

    /// <summary>
    /// Установка 16-битного регистра: сбрасывает H и L в null
    /// </summary>
    public RegisterExpressions Set16(int reg16, Expr expr)
    {
        if (reg16 >= 0 && reg16 <= 3)
        {
            int group = reg16;
            return SetX(group, expr);
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

    private static Math2Expr LowByte(Expr e) => new(Math2Operation.And, e, new ConstExpr(0xff));
    private static Math2Expr HighByte(Expr e) => new(Math2Operation.Shr, e, new ConstExpr(8));

    /// <summary>
    /// Установка 8-битного регистра с логикой: если есть X - разделяем на другой байт с & 0xff или >>8, X в null
    /// </summary>
    public RegisterExpressions Set8(int reg8, Expr expr)
    {
        int group = reg8 switch
        {
            0 or 4 => 0, // AL/AH -> AX
            1 or 5 => 1, // CL/CH -> CX
            2 or 6 => 2, // DL/DH -> DX
            3 or 7 => 3, // BL/BH -> BX
            _ => -1
        };
        if (group < 0) return this;

        bool isHigh = reg8 >= 4;
        Expr? x = GetX(group);
        Expr? other = x != null ? (isHigh ? LowByte(x) : HighByte(x)) : null;

        Expr? newH = isHigh ? expr : (other ?? GetH(group));
        Expr? newL = !isHigh ? expr : (other ?? GetL(group));

        // сбрасываем X, устанавливаем H/L
        return group switch
        {
            0 => this with { AX = null, AH = newH, AL = newL },
            1 => this with { CX = null, CH = newH, CL = newL },
            2 => this with { DX = null, DH = newH, DL = newL },
            3 => this with { BX = null, BH = newH, BL = newL },
            _ => this
        };
    }

    /// <summary>
    /// Получение 16-битного как выражение: если оба H и L установлены - объединяем (H << 8) | L
    /// </summary>
    public readonly Expr Get16(int reg16)
    {
        int group = reg16 switch { 0 => 0, 1 => 1, 2 => 2, 3 => 3, _ => -1 };
        if (group >= 0)
        {
            Expr? h = GetH(group);
            Expr? l = GetL(group);
            if (h != null && l != null)
            {
                var shl = new Math2Expr(Math2Operation.Shl, h, new ConstExpr(8));
                return new Math2Expr(Math2Operation.Or, shl, l);
            }

            return GetX(group) ?? throw new InvalidOperationException();
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
    /// Получение 8-битного выражения
    /// </summary>
    public readonly Expr Get8(int reg8)
    {
        int group = reg8 switch
        {
            0 or 4 => 0,
            1 or 5 => 1,
            2 or 6 => 2,
            3 or 7 => 3,
            _ => -1
        };
        if (group < 0) return ConstExpr.Zero;

        bool isHigh = reg8 >= 4;
        Expr? b = isHigh ? GetH(group) : GetL(group);
        if (b != null) return b;

        Expr x = GetX(group) ?? throw new InvalidOperationException();
        return isHigh ? HighByte(x) : LowByte(x);
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