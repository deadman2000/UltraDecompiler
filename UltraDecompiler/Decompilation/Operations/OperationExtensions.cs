using System.Text;

namespace UltraDecompiler.Decompilation.Operations;

/// <summary>
/// Форматирование <see cref="Operation"/> в строки, близкие к синтаксису C.
/// </summary>
public static class OperationExtensions
{
    extension(Operation op)
    {
        /// <summary>
        /// Генерирует строковое представление операции максимально близкое к синтаксису C.
        /// </summary>
        /// <param name="indent">Уровень отступа (в количестве блоков по 4 пробела)</param>
        /// <param name="asStatement">Добавить «;» для простых операций (не if/while/for)</param>
        public string ToCString(int indent = 0, bool asStatement = false)
        {
            string text = op switch
            {
                SetOperation s => $"{s.Dst} = {s.Src}",
                CallOperation c => FormatCall(c),
                StoreOperation st => FormatStore(st),
                WhileOperation w => FormatWhile(w, indent),
                ForOperation f => FormatFor(f, indent),
                IfOperation i => FormatIf(i, indent),
                _ => op.ToString(),
            };

            if (asStatement && op is not (WhileOperation or ForOperation or IfOperation))
            {
                return text + ";";
            }

            return text;
        }
    }

    static string FormatCall(CallOperation call)
    {
        var args = string.Join(", ", call.Args);
        return $"{call.Procedure.Name}({args})";
    }

    static string FormatStore(StoreOperation store)
    {
        var segPrefix = store.Segment != null ? $"{store.Segment}:" : "";
        return $"{segPrefix}[{store.Address}] = {store.Value}";
    }

    static string FormatWhile(WhileOperation loop, int indent)
    {
        string indentStr = new(' ', indent * 4);

        var sb = new StringBuilder();
        sb.AppendLine($"{indentStr}while ({loop.Condition})");
        sb.AppendLine($"{indentStr}{{");
        AppendBody(sb, loop.Body, indent);
        sb.AppendLine($"{indentStr}}}");
        return sb.ToString();
    }

    static string FormatFor(ForOperation loop, int indent)
    {
        string indentStr = new(' ', indent * 4);

        string initStr = loop.Init?.ToCString() ?? "";
        string condStr = loop.Condition?.ToString() ?? "";
        string iterStr = loop.Iteration?.ToCString() ?? "";

        var sb = new StringBuilder();
        sb.AppendLine($"{indentStr}for ({initStr}; {condStr}; {iterStr})");
        sb.AppendLine($"{indentStr}{{");
        AppendBody(sb, loop.Body, indent);
        sb.AppendLine($"{indentStr}}}");
        return sb.ToString();
    }

    static string FormatIf(IfOperation branch, int indent)
    {
        string indentStr = new(' ', indent * 4);

        var sb = new StringBuilder();
        sb.AppendLine($"{indentStr}if ({branch.Condition})");
        sb.AppendLine($"{indentStr}{{");
        AppendBody(sb, branch.ThenBody, indent);
        sb.AppendLine($"{indentStr}}}");

        if (branch.ElseBody != null)
        {
            sb.AppendLine($"{indentStr}else");
            sb.AppendLine($"{indentStr}{{");
            AppendBody(sb, branch.ElseBody, indent);
            sb.AppendLine($"{indentStr}}}");
        }

        return sb.ToString();
    }

    static void AppendBody(StringBuilder sb, IReadOnlyList<Operation> body, int indent)
    {
        string innerIndent = new(' ', (indent + 1) * 4);

        if (body.Count == 0)
        {
            sb.AppendLine($"{innerIndent}; // пустое тело");
            return;
        }

        foreach (var bodyOp in body)
        {
            switch (bodyOp)
            {
                case WhileOperation wo:
                    sb.Append(wo.ToCString(indent + 1));
                    break;
                case ForOperation fo:
                    sb.Append(fo.ToCString(indent + 1));
                    break;
                case IfOperation io:
                    sb.Append(io.ToCString(indent + 1));
                    break;
                default:
                    sb.AppendLine($"{innerIndent}{bodyOp.ToCString(asStatement: true)}");
                    break;
            }
        }
    }
}
