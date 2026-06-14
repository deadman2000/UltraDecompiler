namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Вызов функции (без использования возвращаемого значения).
/// </summary>
public record CallOperation(string Name, IReadOnlyList<Expr> Args) : Operation;
