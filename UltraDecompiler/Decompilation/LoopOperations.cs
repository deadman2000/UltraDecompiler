namespace UltraDecompiler.Decompilation;

/// <summary>
/// Операция цикла while.
/// 
/// Используется для представления циклов с условием продолжения (в первую очередь
/// REPZ/REPNZ-версий строковых инструкций CMPS и SCAS).
/// 
/// Тело цикла хранится как список вложенных <see cref="Operation"/>, что позволяет
/// рекурсивно описывать сложные структуры (в т.ч. вложенные циклы).
/// </summary>
/// <param name="Condition">Условие продолжения цикла (выражение, которое должно быть истинно для продолжения)</param>
/// <param name="Body">Тело цикла — последовательность операций, выполняемых на каждой итерации</param>
public record WhileOperation(Expr Condition, IReadOnlyList<Operation> Body) : Operation
{
    public override string ToString() => ToString(0);

    /// <summary>
    /// Генерирует строковое представление максимально близкое к синтаксису C.
    /// Поддерживает правильные отступы и рекурсивный вывод вложенных циклов.
    /// </summary>
    /// <param name="indent">Уровень отступа (в количестве блоков по 4 пробела)</param>
    public string ToString(int indent)
    {
        string indentStr = new(' ', indent * 4);
        string innerIndent = new(' ', (indent + 1) * 4);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indentStr}while ({Condition})");
        sb.AppendLine($"{indentStr}{{");

        if (Body.Count == 0)
        {
            sb.AppendLine($"{innerIndent}; // пустое тело");
        }
        else
        {
            foreach (var op in Body)
            {
                string opStr = op.ToString();

                // Рекурсивная поддержка вложенных циклов
                if (op is WhileOperation wo)
                    sb.Append(wo.ToString(indent + 1));
                else if (op is ForOperation fo)
                    sb.Append(fo.ToString(indent + 1));
                else
                    sb.AppendLine($"{innerIndent}{opStr};");
            }
        }

        sb.Append($"{indentStr}}}");
        return sb.ToString();
    }
}

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
        string innerIndent = new(' ', (indent + 1) * 4);

        string initStr = Init?.ToString() ?? "";
        string condStr = Condition?.ToString() ?? "";
        string iterStr = Iteration?.ToString() ?? "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indentStr}for ({initStr}; {condStr}; {iterStr})");
        sb.AppendLine($"{indentStr}{{");

        if (Body.Count == 0)
        {
            sb.AppendLine($"{innerIndent}; // пустое тело");
        }
        else
        {
            foreach (var op in Body)
            {
                string opStr = op.ToString();
                if (op is WhileOperation wo)
                    sb.Append(wo.ToString(indent + 1));
                else if (op is ForOperation fo)
                    sb.Append(fo.ToString(indent + 1));
                else
                    sb.AppendLine($"{innerIndent}{opStr};");
            }
        }

        sb.Append($"{indentStr}}}");
        return sb.ToString();
    }
}
