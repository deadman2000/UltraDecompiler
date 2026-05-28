namespace UltraDecompiler.Decompilation.Operations;

/// <summary>
/// Вызов метода
/// </summary>
public record CallOperation(Procedure Procedure, IReadOnlyList<Expr> Args) : Operation;
