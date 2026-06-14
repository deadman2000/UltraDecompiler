namespace UltraDecompiler.Decompilation.Operations;

/// <summary>
/// Операция <c>switch</c>, восстановленная из линейной цепочки сравнений QuickC.
/// </summary>
/// <param name="Discriminant">Выражение в заголовке <c>switch (...)</c>.</param>
/// <param name="Cases">Упорядоченный список веток case/default.</param>
public record SwitchOperation(Expr Discriminant, IReadOnlyList<SwitchCase> Cases) : Operation;
