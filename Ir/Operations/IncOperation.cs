namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Постфиксный инкремент (<c>a++</c>, <c>ptr++</c>).
/// </summary>
/// <param name="Target">Переменная или адрес памяти.</param>
/// <param name="Segment">Сегмент для записи в память (null для локальной переменной).</param>
public record IncOperation(Expr Target, Expr? Segment = null) : Operation;
