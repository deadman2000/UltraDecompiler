namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Составное присваивание вычитания (<c>a -= expr</c>, QuickC: <c>sub [a], expr</c>).
/// </summary>
/// <param name="Target">Переменная или адрес памяти.</param>
/// <param name="Value">Вычитаемое значение.</param>
/// <param name="Segment">Сегмент для записи в память (null для локальной переменной).</param>
public record SubAssignOperation(Expr Target, Expr Value, Expr? Segment = null) : Operation;