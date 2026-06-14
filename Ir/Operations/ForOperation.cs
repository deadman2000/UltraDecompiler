using UltraDecompiler.Decompilation;

namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Операция цикла for.
/// 
/// Предпочтительна для представления REP MOVS / LODS / STOS, когда есть явный счётчик
/// (CX) и известна структура инициализации / итерации.
/// 
/// Структура строго соответствует классическому C-циклу:
/// <code>
/// for (Init; Condition; Iteration) {
///     Body
/// }
/// </code>
/// </summary>
/// <param name="Init">Операция инициализации (может быть null)</param>
/// <param name="Condition">Условие продолжения цикла (может быть null)</param>
/// <param name="Iteration">Операция, выполняемая после каждой итерации (может быть null)</param>
/// <param name="Body">Тело цикла</param>
public record ForOperation(
    Operation? Init,
    Expr? Condition,
    Operation? Iteration,
    IReadOnlyList<Operation> Body
) : Operation;
