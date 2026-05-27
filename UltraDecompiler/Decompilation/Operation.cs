namespace UltraDecompiler.Decompilation;

public abstract record Operation;

/// <summary>
/// Установка значения
/// </summary>
/// <param name="Dst">Назначение</param>
/// <param name="Src">Источник</param>
public record SetOperation(Variable Dst, Expr Src) : Operation
{
    public override string ToString() => $"{Dst} = {Src}";
}

/// <summary>
/// Вызов метода
/// </summary>
public record CallOperation(Procedure Procedure, IReadOnlyList<Expr> Args) : Operation;

/// <summary>
/// Запись значения в память (store).
/// </summary>
public record StoreOperation(Expr Address, Expr? Segment, Expr Value) : Operation
{
    public override string ToString()
    {
        var segPrefix = Segment != null ? $"{Segment}:" : "";
        return $"{segPrefix}[{Address}] = {Value}";
    }
}
