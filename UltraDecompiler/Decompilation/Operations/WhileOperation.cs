namespace UltraDecompiler.Decompilation.Operations;

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

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indentStr}while ({Condition})");
        sb.AppendLine($"{indentStr}{{");
        ControlFlowBodyFormatter.AppendBody(sb, Body, indent);
        sb.Append($"{indentStr}}}");
        return sb.ToString();
    }
}
