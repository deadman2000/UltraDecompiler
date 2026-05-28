namespace UltraDecompiler.Decompilation.Operations;

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
) : Operation
{
    public override string ToString() => ToString(0);

    /// <summary>
    /// Генерирует строковое представление максимально близкое к синтаксису C.
    /// </summary>
    /// <param name="indent">Уровень отступа (в количестве блоков по 4 пробела)</param>
    public string ToString(int indent)
    {
        string indentStr = new(' ', indent * 4);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indentStr}if ({Condition})");
        sb.AppendLine($"{indentStr}{{");
        ControlFlowBodyFormatter.AppendBody(sb, ThenBody, indent);
        sb.AppendLine($"{indentStr}}}");

        if (ElseBody != null)
        {
            sb.AppendLine($"{indentStr}else");
            sb.AppendLine($"{indentStr}{{");
            ControlFlowBodyFormatter.AppendBody(sb, ElseBody, indent);
            sb.Append($"{indentStr}}}");
        }

        return sb.ToString();
    }
}
