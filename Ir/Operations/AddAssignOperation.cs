namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Составное присваивание сложения (<c>a += expr</c>, QuickC: <c>add [a], expr</c>).
/// </summary>
/// <param name="Target">Переменная или адрес памяти.</param>
/// <param name="Value">Добавляемое значение (константа, переменная или выражение).</param>
/// <param name="Segment">Сегмент для записи в память (null для локальной переменной).</param>
public record AddAssignOperation(Expr Target, Expr Value, Expr? Segment = null) : Operation;