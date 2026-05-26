namespace UltraDecompiler.Decompilation;

/// <summary>
/// Базовый класс выражений
/// </summary>
public abstract record Expr;

/// <summary>
/// Переменная
/// </summary>
/// <param name="Number">Номер</param>
public record Variable(int Number = 0) : Expr
{
    /// <summary>
    /// Имя
    /// </summary>
    public string? Name { get; set; }

    public override string ToString() => Name is not null ? Name : $"var{Number}";
}

/// <summary>
/// Константное значение
/// </summary>
/// <param name="Value">Значение</param>
public record ConstExpr(int Value) : Expr
{
    public static readonly ConstExpr Zero = new(0);

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Типы математических операций с одним аргументом
/// </summary>
public enum Math1Operation
{
    /// <summary>
    /// Смена знака числа
    /// </summary>
    Neg,

    /// <summary>
    /// Отрицание
    /// </summary>
    Not,
}

/// <summary>
/// Выражение математической операции с одним аргументом
/// </summary>
/// <param name="Operation">Тип операции</param>
/// <param name="Op">Операнд</param>
public record Math1Expr(Math1Operation Operation, Expr Op) : Expr
{
    public override string ToString() => Operation switch
    {
        Math1Operation.Neg => $"-{Op}",
        Math1Operation.Not => $"!{Op}",
        _ => throw new NotImplementedException(),
    };
}

/// <summary>
/// Типы математических операций с двумя аргументами
/// </summary>
public enum Math2Operation
{
    /// <summary>
    /// Сложение
    /// </summary>
    Add,

    /// <summary>
    /// Вычитание
    /// </summary>
    Sub,

    /// <summary>
    /// Побитовый сдвиг влево
    /// </summary>
    Shl,

    /// <summary>
    /// Побитовый сдвиг вправо
    /// </summary>
    Shr,

    /// <summary>
    /// Побитовое И
    /// </summary>
    And,

    /// <summary>
    /// Побитовое ИЛИ
    /// </summary>
    Or,

    /// <summary>
    /// Побитовое исключающее ИЛИ
    /// </summary>
    Xor,
}

/// <summary>
/// Выражение математической операции с двумя аргументами
/// </summary>
/// <param name="Operation">Тип операции</param>
/// <param name="First">Первый операнд</param>
/// <param name="Second">Второй операнд</param>
public record Math2Expr(Math2Operation Operation, Expr First, Expr Second) : Expr
{
    public override string ToString() => Operation switch
    {
        Math2Operation.Add => $"{First} + {Second}",
        Math2Operation.Sub => $"{First} - {Second}",
        Math2Operation.Shl => $"{First} << {Second}",
        Math2Operation.Shr => $"{First} >> {Second}",
        Math2Operation.And => $"{First} & {Second}",
        Math2Operation.Or => $"{First} | {Second}",
        Math2Operation.Xor => $"{First} ^ {Second}",
        _ => throw new NotImplementedException(),
    };
}

/// <summary>
/// Вызов метода
/// </summary>
public record CallExpr(Procedure Procedure, IReadOnlyList<Expr> Args) : Expr;

/// <summary>
/// Операции сравнения, используемые для представления флагов (ZF и т.д.) и условий.
/// </summary>
public enum CmpOperation
{
    /// <summary>Равно</summary>
    Eq,
    /// <summary>Не равно</summary>
    Ne,
}

/// <summary>
/// Выражение сравнения двух операндов.
/// Используется чтобы символически представить значение флагов после CMP/TEST/арифметики.
/// Пример: после "CMP AX, 5" флаг ZF может быть CmpExpr(Eq, AX, Const(5))
/// </summary>
public record CmpExpr(CmpOperation Operation, Expr Left, Expr Right) : Expr
{
    public override string ToString() => Operation switch
    {
        CmpOperation.Eq => $"{Left} == {Right}",
        CmpOperation.Ne => $"{Left} != {Right}",
        _ => throw new NotImplementedException(),
    };
}
