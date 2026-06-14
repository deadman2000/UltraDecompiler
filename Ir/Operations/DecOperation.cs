using UltraDecompiler.Decompilation;

namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Постфиксный декремент (<c>a--</c>, <c>ptr--</c>).
/// </summary>
/// <param name="Target">Переменная или адрес памяти.</param>
/// <param name="Segment">Сегмент для записи в памяти (null для локальной переменной).</param>
public record DecOperation(Expr Target, Expr? Segment = null) : Operation;
