using System.Text;

namespace UltraDecompiler.CodeGeneration.Rendering;

/// <summary>
/// Форматирование <see cref="Operation"/> в строки, близкие к синтаксису C.
/// </summary>
public static class COperationRenderer
{
    public static string Render(Operation op, int indent = 0, bool asStatement = false)
    {
        string text = op switch
        {
            SetOperation s => FormatSet(s, indent),
            CallOperation c => FormatCall(c, indent),
            StoreOperation st => FormatStore(st, indent),
            IncOperation inc => FormatIncDec(inc.Target, inc.Segment, "++", indent),
            DecOperation dec => FormatIncDec(dec.Target, dec.Segment, "--", indent),
            AddAssignOperation add => FormatCompoundAssign(add.Target, add.Segment, "+=", add.Value, indent),
            SubAssignOperation sub => FormatCompoundAssign(sub.Target, sub.Segment, "-=", sub.Value, indent),
            ReturnOperation r => FormatReturn(r, indent),
            WhileOperation w => FormatWhile(w, indent),
            DoWhileOperation d => FormatDoWhile(d, indent),
            ForOperation f => FormatFor(f, indent),
            IfOperation i => FormatIf(i, indent),
            SwitchOperation s => FormatSwitch(s, indent),
            ContinueOperation => FormatContinue(indent),
            BreakOperation => FormatBreak(indent),
            GotoOperation g => FormatGoto(g, indent),
            LabelOperation l => FormatLabel(l, indent),
            _ => Indent(indent) + op.ToString(),
        };

        if (asStatement && op is not (WhileOperation or DoWhileOperation or ForOperation or IfOperation or SwitchOperation or LabelOperation))
        {
            return text + ";";
        }

        return text;
    }

    public static void Append(StringBuilder sb, Operation op, int indent = 0, bool asStatement = false)
    {
        string text = Render(op, indent, asStatement);
        if (op is WhileOperation or DoWhileOperation or ForOperation or IfOperation or SwitchOperation)
        {
            sb.Append(text);
        }
        else
        {
            sb.AppendLine(text);
        }
    }

    static string Indent(int level) => new(' ', level * 4);

    static string FormatSet(SetOperation set, int indent) =>
        $"{Indent(indent)}{FormatAssignmentLvalue(set.Dst)} = {set.Src.RenderExpr()}";

    static string FormatAssignmentLvalue(Expr dst)
    {
        if (dst is MemExpr mem && PointerDerefFormatter.TryFormatLoad(mem, out var deref))
        {
            return deref;
        }

        if (PointerStoreFormatter.TryFormat(new StoreOperation(dst, null, ConstExpr.Zero), out var indexed))
        {
            return indexed;
        }

        return dst.RenderExpr();
    }

    static string FormatCall(CallOperation call, int indent)
    {
        var args = TrimPrintfArguments(call.Name, call.Args);
        var argsText = string.Join(", ", args.Select(a => a.RenderExpr()));
        return $"{Indent(indent)}{call.Name}({argsText})";
    }

    static IReadOnlyList<Expr> TrimPrintfArguments(string name, IReadOnlyList<Expr> args)
    {
        if (args.Count < 2
            || !string.Equals(name, "printf", StringComparison.Ordinal)
            || args[0] is not StringExpr format
            || !format.Value.Contains("%ld", StringComparison.Ordinal))
        {
            return args;
        }

        var result = new List<Expr>(args);
        while (result.Count > 2 && result[^1] is ConstExpr { Value: 0 })
        {
            result.RemoveAt(result.Count - 1);
        }

        if (result.Count >= 3
            && result[1] is CallExpr
            && result[2] is ConstExpr { Value: 0 })
        {
            result.RemoveAt(2);
        }

        return result;
    }

    static string FormatReturn(ReturnOperation ret, int indent)
    {
        if (ret.Value is { } v)
        {
            return $"{Indent(indent)}return {v.RenderExpr()}";
        }
        return $"{Indent(indent)}return";
    }

    static string FormatStore(StoreOperation store, int indent)
    {
        if (PointerStoreFormatter.TryFormat(store, out var lvalue))
        {
            return $"{Indent(indent)}{lvalue} = {store.Value.RenderExpr()}";
        }

        var segPrefix = store.Segment != null ? $"{store.Segment}:" : "";
        return $"{Indent(indent)}{segPrefix}[{store.Address.RenderExpr()}] = {store.Value.RenderExpr()}";
    }

    static string FormatIncDec(Expr target, Expr? segment, string suffix, int indent)
    {
        if (target is VariableExpr { Var: var variable } && segment is null)
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


    static string FormatCompoundAssign(Expr target, Expr? segment, string op, Expr value, int indent)
    {
        if (target is VariableExpr { Var: var variable } && segment is null)
        {
            return $"{Indent(indent)}{variable} {op} {value.RenderExpr()}";
        }

        if (PointerStoreFormatter.TryFormat(new StoreOperation(target, segment, ConstExpr.Zero), out var lvalue))
        {
            return $"{Indent(indent)}{lvalue} {op} {value.RenderExpr()}";
        }

        var segPrefix = segment != null ? $"{segment}:" : "";
        return $"{Indent(indent)}{segPrefix}[{target}] {op} {value.RenderExpr()}";
    }
    static string FormatWhile(WhileOperation loop, int indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Indent(indent)}while ({loop.Condition.RenderExpr()})");
        sb.AppendLine($"{Indent(indent)}{{");
        AppendBody(sb, loop.Body, indent);
        sb.AppendLine($"{Indent(indent)}}}");
        return sb.ToString();
    }

    static string FormatDoWhile(DoWhileOperation loop, int indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Indent(indent)}do");
        sb.AppendLine($"{Indent(indent)}{{");
        AppendBody(sb, loop.Body, indent);
        sb.AppendLine($"{Indent(indent)}}}");
        sb.AppendLine($"{Indent(indent)}while ({loop.Condition.RenderExpr()});");
        return sb.ToString();
    }

    static string FormatFor(ForOperation loop, int indent)
    {
        string initStr = FormatForClause(loop.Init);
        string condStr = loop.Condition?.RenderExpr() ?? "";
        string iterStr = FormatForClause(loop.Iteration);

        var sb = new StringBuilder();
        sb.AppendLine($"{Indent(indent)}for ({initStr}; {condStr}; {iterStr})");
        sb.AppendLine($"{Indent(indent)}{{");
        AppendBody(sb, loop.Body, indent);
        sb.AppendLine($"{Indent(indent)}}}");
        return sb.ToString();
    }

    /// <summary>Фрагмент заголовка for без завершающей «;» (разделители добавляет сам for).</summary>
    static string FormatForClause(Operation? operation)
    {
        if (operation is null)
        {
            return "";
        }

        var text = operation is SetOperation set
            ? COperationRenderer.Render(set, asStatement: true)
            : COperationRenderer.Render(operation, asStatement: true);

        return text.EndsWith(';') ? text[..^1] : text;
    }

    static string FormatContinue(int indent) => $"{Indent(indent)}continue";

    static string FormatBreak(int indent) => $"{Indent(indent)}break";

    static string FormatGoto(GotoOperation g, int indent) => $"{Indent(indent)}goto {g.Label}";

    static string FormatLabel(LabelOperation l, int indent) => $"{Indent(indent)}{l.Label}:";

    static string FormatIf(IfOperation branch, int indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Indent(indent)}if ({branch.Condition.RenderExpr()})");
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

    static string FormatSwitch(SwitchOperation sw, int indent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Indent(indent)}switch ({sw.Discriminant.RenderExpr()})");
        sb.AppendLine($"{Indent(indent)}{{");
        foreach (var switchCase in sw.Cases)
        {
            if (switchCase.Value is ConstExpr value)
            {
                sb.AppendLine($"{Indent(indent + 1)}case {value.Value}:");
            }
            else
            {
                sb.AppendLine($"{Indent(indent + 1)}default:");
            }

            AppendBody(sb, switchCase.Body, indent + 1);
            sb.AppendLine($"{Indent(indent + 1)}break;");
        }

        sb.AppendLine($"{Indent(indent)}}}");
        return sb.ToString();
    }

    static void AppendBody(StringBuilder sb, IReadOnlyList<Operation> body, int indent)
    {
        if (body.Count == 0)
        {
            sb.AppendLine($"{Indent(indent + 1)};");
            return;
        }

        foreach (var bodyOp in body)
        {
            bodyOp.AppendToCString(sb, indent + 1, asStatement: true);
        }
    }
}
