namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Условная операция if / else.
///
/// Используется для представления ветвлений, восстановленных из условных переходов CFG.
/// Ветви then/else хранятся как списки вложенных <see cref="Operation"/>.
/// </summary>
/// <param name="Condition">Условие ветвления</param>
/// <param name="ThenBody">Тело ветки «истина»</param>
/// <param name="ElseBody">Тело ветки «ложь» (null, если ветка else отсутствует)</param>
public record IfOperation(
    Expr Condition,
    IReadOnlyList<Operation> ThenBody,
    IReadOnlyList<Operation>? ElseBody = null
) : Operation;
