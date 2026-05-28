namespace UltraDecompiler.Decompilation.Operations;

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
) : Operation
{
    public override string ToString() => ToString(0);

    /// <summary>
    /// Генерирует строковое представление максимально близкое к синтаксису C.
    /// </summary>
    /// <param name="indent">Уровень отступа</param>
    public string ToString(int indent)
    {
        string indentStr = new(' ', indent * 4);

        string initStr = Init?.ToString() ?? "";
        string condStr = Condition?.ToString() ?? "";
        string iterStr = Iteration?.ToString() ?? "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indentStr}for ({initStr}; {condStr}; {iterStr})");
        sb.AppendLine($"{indentStr}{{");
        ControlFlowBodyFormatter.AppendBody(sb, Body, indent);
        sb.Append($"{indentStr}}}");
        return sb.ToString();
    }
}
