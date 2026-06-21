namespace UltraDecompiler.Ir.Expressions;

/// <summary>
/// Переменная
/// </summary>
/// <param name="Number">Номер для <c>varN</c>/<c>tempN</c> (1-based; у <see cref="IsInternal"/> всегда 0).</param>
/// <param name="Name">Имя (параметры, поля PSP, метки строковых циклов).</param>
/// <param name="IsStack">Локаль стекового кадра [BP±disp].</param>
/// <param name="IsTemp">Временная переменная IR-анализа.</param>
/// <param name="IsInternal">Служебная переменная символического выполнения.</param>
/// <param name="IsGlobal">Глобальная переменная DGROUP (объявляется вне функций).</param>
public sealed record Variable(
    int Number = 0,
    string? Name = null,
    bool IsStack = false,
    bool IsTemp = false,
    bool IsInternal = false,
    bool IsGlobal = false,
    bool IsRegister = false)
{
    public bool HasGet { get; set; }

    public bool HasSet { get; set; }

    public VariableExpr ToGet()
    {
        HasGet = true;
        return new VariableExpr() { Var = this };
    }

    public VariableExpr ToSet()
    {
        HasSet = true;
        return new VariableExpr() { Var = this };
    }

    /// <summary>Старшее слово far-указателя на стеке (не объявляется отдельно в C).</summary>
    public bool IsMergedFarPointerSegment { get; set; }

    /// <summary>Старшее слово <c>long</c> на стеке (не объявляется отдельно в C).</summary>
    public bool IsMergedLongHigh { get; set; }

    /// <summary>Сегментная часть far-указателя, загруженная в ES/DS через LES/LDS.</summary>
    public Variable? FarPointerSegmentVariable { get; set; }

    /// <summary>Инициализатор far-указателя (<c>seg:off</c> в одном 32-битном литерале).</summary>
    public uint? FarPointerInitializer { get; set; }

    /// <summary>Нужно ли объявление переменной в сгенерированном C-коде (локально внутри функции).</summary>
    public bool RequiresCDeclaration =>
        !IsMergedFarPointerSegment
        && !IsMergedLongHigh
        && (IsStack || (!IsTemp && !IsInternal && !IsGlobal));

    /// <summary>
    /// Выведенный тип C (из сигнатуры вызова или копирования). <see langword="null"/> — <c>int</c>.
    /// </summary>
    public CType? Type { get; set; }

    /// <summary>
    /// Размер локального массива на стеке (<c>char buf[N]</c>), если известен из <c>_chkstk</c>.
    /// </summary>
    public int? ArraySize { get; set; }

    /// <summary>Начальное значение глобала (из инициализированного _DATA в образе EXE).</summary>
    public int? InitialValue { get; set; }

    /// <summary>Тип для объявления в C-коде (без имени; массивы оформляет <see cref="CCodeGenerator"/>).</summary>
    public string DeclaredType => (Type ?? CType.Int).ToString();

    public override string ToString()
    {
        if (Name is not null)
        {
            return Name;
        }

        if (IsStack)
        {
            return $"var{Number}";
        }

        if (IsTemp)
        {
            return $"temp{Number}";
        }

        return $"var{Number}";
    }
}
