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

    public Expr ZF { get; init; } = ConstExpr.Zero;
    public Expr CF { get; init; } = ConstExpr.Zero;
    public Expr SF { get; init; } = ConstExpr.Zero;
    public Expr OF { get; init; } = ConstExpr.Zero;
    public Expr DF { get; init; } = ConstExpr.Zero;   // Direction Flag: 0 = вперёд, 1 = назад

    // ===== Хелперы для сокращения дублирования логики 4 групп (AX/AH/AL, CX/CH/CL и т.д.) =====

    private enum GpRegisterGroup : byte
    {
        Ax = 0,
        Cx = 1,
        Dx = 2,
        Bx = 3,
    }

    private static GpRegisterGroup Reg8ToGroup(GpRegister8 reg8) => reg8 switch
    {
        GpRegister8.AL or GpRegister8.AH => GpRegisterGroup.Ax,
        GpRegister8.CL or GpRegister8.CH => GpRegisterGroup.Cx,
        GpRegister8.DL or GpRegister8.DH => GpRegisterGroup.Dx,
        GpRegister8.BL or GpRegister8.BH => GpRegisterGroup.Bx,
        _ => throw new ArgumentOutOfRangeException(nameof(reg8), reg8, null),
    };

    private static GpRegisterGroup? Reg16ToGroup(GpRegister16 reg16) => reg16 switch
    {
        GpRegister16.AX => GpRegisterGroup.Ax,
        GpRegister16.CX => GpRegisterGroup.Cx,
        GpRegister16.DX => GpRegisterGroup.Dx,
        GpRegister16.BX => GpRegisterGroup.Bx,
        _ => null,
    };

    private static bool IsHighReg8(GpRegister8 reg8) => reg8 >= GpRegister8.AH;

    private readonly (Expr? X, Expr? H, Expr? L) GetGroup(GpRegisterGroup group) => group switch
    {
        GpRegisterGroup.Ax => (AX, AH, AL),
        GpRegisterGroup.Cx => (CX, CH, CL),
        GpRegisterGroup.Dx => (DX, DH, DL),
        GpRegisterGroup.Bx => (BX, BH, BL),
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
    };

    private RegisterExpressions WithGroup(GpRegisterGroup group, Expr? x, Expr? h, Expr? l)
    {
        Debug.Assert(IsValidGroupState(x, h, l),
            $"Invalid register group {group} state: X={x}, H={h}, L={l}");
        return group switch
        {
            GpRegisterGroup.Ax => this with { AX = x, AH = h, AL = l },
            GpRegisterGroup.Cx => this with { CX = x, CH = h, CL = l },
            GpRegisterGroup.Dx => this with { DX = x, DH = h, DL = l },
            GpRegisterGroup.Bx => this with { BX = x, BH = h, BL = l },
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
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
        var initCS = variables.CreateInternalVariable("_initCS");
        var initSS = variables.CreateInternalVariable("_initSS");
        var initSP = variables.CreateInternalVariable("_initSP");

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

    public static RegisterExpressions InitProc(VariableStorage variables)
    {
        var zero = ConstExpr.Zero;
        return new RegisterExpressions(AX: variables.CreateInternalVariable("varAX"),
                                       BX: variables.CreateInternalVariable("varBX"),
                                       CX: variables.CreateInternalVariable("varCX"),
                                       DX: variables.CreateInternalVariable("varDX"),
                                       SP: new ConstExpr(0xfffe),
                                       BP: zero,
                                       SI: zero,
                                       DI: zero,
                                       ES: variables.PspBase,
                                       CS: variables.CreateInternalVariable("varCS"),
                                       SS: variables.CreateInternalVariable("varSS"),
                                       DS: variables.PspBase);
    }

    /// <summary>
    /// Установка 16-битного регистра: сбрасывает H и L в null.
    /// Для gp-регистров (AX–BX) переводит группу в состояние (X=expr, H=null, L=null).
    /// </summary>
    public RegisterExpressions Set16(GpRegister16 reg16, Expr expr)
    {
        GpRegisterGroup? group = Reg16ToGroup(reg16);
        if (group.HasValue)
        {
            return WithGroup(group.Value, expr, null, null);
        }
        return reg16 switch
        {
            GpRegister16.SP => this with { SP = expr },
            GpRegister16.BP => this with { BP = expr },
            GpRegister16.SI => this with { SI = expr },
            GpRegister16.DI => this with { DI = expr },
            _ => this
        };
    }

    /// <summary>
    /// Установка 8-битного регистра.
    /// Точная логика (по ТЗ):
    /// - если AX (или CX..) != null, то при установке AH: AL = AX &amp; 0xFF; при установке AL: AH = AX >> 8.
    /// - X всегда сбрасывается в null, устанавливаются оба байта (H и L).
    /// - Если X == null — просто обновляем нужный байт, второй оставляем как есть (предполагается, что он был).
    /// </summary>
    public RegisterExpressions Set8(GpRegister8 reg8, Expr expr)
    {
        GpRegisterGroup group = Reg8ToGroup(reg8);

        bool isHigh = IsHighReg8(reg8);
        var (x, oldH, oldL) = GetGroup(group);

        Expr? newH;
        Expr? newL;
        if (x != null)
        {
            // Правило: при присвоении AH/AL, если есть X — вычисляем "другой" байт из X
            Expr other = isHigh ? x.LowByte() : x.HighByte();
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
    public readonly Expr Get16(GpRegister16 reg16)
    {
        GpRegisterGroup? group = Reg16ToGroup(reg16);
        if (group.HasValue)
        {
            var (x, h, l) = GetGroup(group.Value);

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
            GpRegister16.SP => SP,
            GpRegister16.BP => BP,
            GpRegister16.SI => SI,
            GpRegister16.DI => DI,
            _ => ConstExpr.Zero
        };
    }

    /// <summary>
    /// Получение 8-битного выражения.
    /// Если байт (H или L) установлен — возвращаем его.
    /// Если байт null, но есть X — вычисляем HighByte/LowByte(X).
    /// При нарушении инварианта — assert + исключение.
    /// </summary>
    public readonly Expr Get8(GpRegister8 reg8)
    {
        GpRegisterGroup group = Reg8ToGroup(reg8);

        bool isHigh = IsHighReg8(reg8);
        var (x, h, l) = GetGroup(group);

        Expr? b = isHigh ? h : l;
        if (b != null) return b;

        // Правило: "при получении AH/AL, если они null — вычисляем из AX"
        if (x != null)
            return isHigh ? x.HighByte() : x.LowByte();

        // Недопустимое состояние
        Debug.Assert(false, $"Group {group} is in invalid state in Get8 (no byte and no X)");
        throw new InvalidOperationException($"Register group {group} has no value for 8-bit access");
    }

    /// <summary>
    /// Установка сегментного регистра.
    /// </summary>
    public RegisterExpressions SetSegment(CpuSegmentRegister sreg, Expr expr)
    {
        return sreg switch
        {
            CpuSegmentRegister.ES => this with { ES = expr },
            CpuSegmentRegister.CS => this with { CS = expr },
            CpuSegmentRegister.SS => this with { SS = expr },
            CpuSegmentRegister.DS => this with { DS = expr },
            _ => this
        };
    }

    /// <summary>
    /// Получение сегментного регистра.
    /// </summary>
    public readonly Expr GetSegment(CpuSegmentRegister sreg)
    {
        return sreg switch
        {
            CpuSegmentRegister.ES => ES,
            CpuSegmentRegister.CS => CS,
            CpuSegmentRegister.SS => SS,
            CpuSegmentRegister.DS => DS,
            _ => ConstExpr.Zero
        };
    }
}
