using System.Text;
using UltraDecompiler.PostProcessing;

namespace UltraDecompiler.Decompilation.Operations;

/// <summary>
/// Форматирование <see cref="Operation"/> в строки, близкие к синтаксису C.
/// </summary>
public static class Extensions
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
                SetOperation s => FormatSet(s, indent),
                CallOperation c => FormatCall(c, indent),
                StoreOperation st => FormatStore(st, indent),
                IncOperation inc => FormatIncDec(inc.Target, inc.Segment, "++", indent),
                DecOperation dec => FormatIncDec(dec.Target, dec.Segment, "--", indent),
                ReturnOperation r => FormatReturn(r, indent),
                WhileOperation w => FormatWhile(w, indent),
                ForOperation f => FormatFor(f, indent),
                IfOperation i => FormatIf(i, indent),
                _ => Indent(indent) + op.ToString(),
            };

            if (asStatement && op is not (WhileOperation or ForOperation or IfOperation))
            {
                return text + ";";
            }

            return text;
        }

        /// <summary>
        /// Добавляет строковое представление операции в <see cref="StringBuilder"/> с учётом многострочности.
        /// </summary>
        public void AppendToCString(StringBuilder sb, int indent = 0, bool asStatement = false)
        {
            string text = op.ToCString(indent, asStatement);
            if (op is WhileOperation or ForOperation or IfOperation)
            {
                sb.Append(text);
            }
            else
            {
                sb.AppendLine(text);
            }
        }
    }

    static string Indent(int level) => new(' ', level * 4);

    static string FormatSet(SetOperation set, int indent) =>
        $"{Indent(indent)}{set.Dst} = {set.Src}";

    static string FormatCall(CallOperation call, int indent)
    {
        var args = string.Join(", ", call.Args);
        return $"{Indent(indent)}{call.Name}({args})";
    }

    static string FormatReturn(ReturnOperation ret, int indent)
    {
        if (ret.Value is { } v)
        {
            return $"{Indent(indent)}return {v}";
        }
        return $"{Indent(indent)}return";
    }

    static string FormatStore(StoreOperation store, int indent)
    {
        if (PointerStoreFormatter.TryFormat(store, out var lvalue))
        {
            return $"{Indent(indent)}{lvalue} = {store.Value}";
        }

        var segPrefix = store.Segment != null ? $"{store.Segment}:" : "";
        return $"{Indent(indent)}{segPrefix}[{store.Address}] = {store.Value}";
    }

    static string FormatIncDec(Expr target, Expr? segment, string suffix, int indent)
    {
        if (target is Variable variable && segment is null)
        {
            return $"{Indent(indent)}{variable}{suffix}";
        }

        if (PointerStoreFormatter.TryFormat(new StoreOperation(target, segment, ConstExpr.Zero), out var lvalue))
        {
            return $"{Indent(indent)}{lvalue}{suffix}";
        }

        var segPrefix = segment != null ? $"{segment}:" : "";
        return $"{Indent(indent)}{segPrefix}[{target}]{suffix}";
    }

    static string FormatWhile(WhileOperation loop, int indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Indent(indent)}while ({loop.Condition})");
        sb.AppendLine($"{Indent(indent)}{{");
        AppendBody(sb, loop.Body, indent);
        sb.AppendLine($"{Indent(indent)}}}");
        return sb.ToString();
    }

    static string FormatFor(ForOperation loop, int indent)
    {
        string initStr = loop.Init?.ToCString() ?? "";
        string condStr = loop.Condition?.ToString() ?? "";
        string iterStr = loop.Iteration?.ToCString() ?? "";

        var sb = new StringBuilder();
        sb.AppendLine($"{Indent(indent)}for ({initStr}; {condStr}; {iterStr})");
        sb.AppendLine($"{Indent(indent)}{{");
        AppendBody(sb, loop.Body, indent);
        sb.AppendLine($"{Indent(indent)}}}");
        return sb.ToString();
    }

    static string FormatIf(IfOperation branch, int indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Indent(indent)}if ({branch.Condition})");
        sb.AppendLine($"{Indent(indent)}{{");
        AppendBody(sb, branch.ThenBody, indent);
        sb.AppendLine($"{Indent(indent)}}}");

        if (branch.ElseBody is { Count: > 0 })
        {
            sb.AppendLine($"{Indent(indent)}else");
            sb.AppendLine($"{Indent(indent)}{{");
            AppendBody(sb, branch.ElseBody, indent);
            sb.AppendLine($"{Indent(indent)}}}");
        }

        return sb.ToString();
    }

    static void AppendBody(StringBuilder sb, IReadOnlyList<Operation> body, int indent)
    {
        if (body.Count == 0)
        {
            sb.AppendLine($"{Indent(indent + 1)}; // empty body");
            return;
        }

        foreach (var bodyOp in body)
        {
            bodyOp.AppendToCString(sb, indent + 1, asStatement: true);
        }
    }
}
