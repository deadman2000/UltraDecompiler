using System.Text;
using UltraDecompiler.Common;

namespace UltraDecompiler.Ir.Rendering;

/// <summary>
/// Плоское текстовое представление IR по базовым блокам (метки, goto, без вложенных if/циклов).
/// </summary>
public static class IrTreeTextRenderer
{
    /// <summary>
    /// Строит текстовый дамп IR для одной процедуры: блоки <see cref="ExpressionBuilder.Blocks"/> по смещению.
    /// </summary>
    public static string RenderProcedure(DisassembledProcedure procedure, OptimizationLevel optimizationLevel)
    {
        _ = optimizationLevel;

        if (procedure.Expressions is null)
        {
            return "(нет IR)";
        }

        return RenderBlocks(procedure.Expressions.Blocks);
    }

    /// <summary>
    /// Форматирует блоки IR построчно с явными границами и переходами.
    /// </summary>
    public static string RenderBlocks(IReadOnlyList<ExprBlock> blocks)
    {
        if (blocks.Count == 0)
        {
            return "(нет блоков)";
        }

        var sb = new StringBuilder();
        var ordered = blocks.OrderBy(static b => b.BasicBlock.StartOffset).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
            }

            AppendBlock(sb, ordered[i]);
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendBlock(StringBuilder sb, ExprBlock block)
    {
        var start = block.BasicBlock.StartOffset;
        var end = block.BasicBlock.EndOffset;
        sb.AppendLine($"0x{start:X4}..0x{end:X4}");
        sb.AppendLine($"{FormatLabel(block)}:");

        foreach (var op in block.Operations)
        {
            sb.AppendLine(FormatLeaf(op));
        }

        AppendBlockEdges(sb, block);
    }

    private static void AppendBlockEdges(StringBuilder sb, ExprBlock block)
    {
        if (block.Operations.Count > 0 && block.Operations[^1] is ReturnOperation)
        {
            return;
        }

        if (block.Condition is { } condition && block.ConditionalBlock is { } conditionalTarget)
        {
            sb.AppendLine($"if ({condition}) goto {FormatLabel(conditionalTarget)}");
        }

        if (block.Next is { } next)
        {
            sb.AppendLine($"goto {FormatLabel(next)}");
        }
    }

    private static string FormatLabel(ExprBlock block) => $"label_{block.BasicBlock.StartOffset:X4}";

    private static string FormatLeaf(Operation op) =>
        op switch
        {
            SetOperation s => $"{s.Dst} = {s.Src}",
            CallOperation c => $"{c.Name}({string.Join(", ", c.Args)})",
            StoreOperation st => $"[{st.Address}] = {st.Value}",
            ReturnOperation r => r.Value is null ? "return" : $"return {r.Value}",
            IncOperation inc => $"{inc.Target}++",
            DecOperation dec => $"{dec.Target}--",
            AddAssignOperation add => $"{add.Target} += {add.Value}",
            SubAssignOperation sub => $"{sub.Target} -= {sub.Value}",
            ContinueOperation => "continue",
            BreakOperation => "break",
            GotoOperation g => $"goto {g.Label}",
            LabelOperation l => $"{l.Label}:",
            IfOperation i => $"if ({i.Condition})",
            WhileOperation w => $"while ({w.Condition})",
            DoWhileOperation d => $"do-while ({d.Condition})",
            ForOperation f => $"for ({FormatForClause(f.Init)}; {f.Condition}; {FormatForClause(f.Iteration)})",
            SwitchOperation s => $"switch ({s.Discriminant})",
            _ => op.ToString() ?? string.Empty,
        };

    private static string FormatForClause(Operation? operation) =>
        operation switch
        {
            null => "",
            SetOperation set => $"{set.Dst} = {set.Src}",
            _ => FormatLeaf(operation),
        };
}
