using System.Text;

namespace UltraDecompiler.Decompilation;

public partial class ExpressionBuilder
{
    /// <summary>
    /// Сохраняет граф выражений в формате DOT (узлы — адрес блока и список операций).
    /// </summary>
    /// <param name="fileName">Путь к выходному .dot файлу</param>
    public void SaveDot(string fileName)
    {
        if (Blocks.Count == 0)
        {
            File.WriteAllText(fileName, "digraph empty {}");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("digraph ExpressionBuilder {");
        sb.AppendLine("    rankdir=TB;");
        sb.AppendLine("    node [shape=box, fontname=\"Consolas\", fontsize=8, style=filled, fillcolor=\"#E6F3FF\"];");
        sb.AppendLine("    edge [fontname=\"Consolas\", fontsize=7];");

        foreach (var block in Blocks)
        {
            string nodeId = NodeId(block);
            var lines = new List<string>
            {
                $"0x{block.BasicBlock.StartOffset:X4}..0x{block.BasicBlock.EndOffset:X4}",
            };

            if (block.Operations.Count == 0)
            {
                lines.Add("(нет операций)");
            }
            else
            {
                foreach (var op in block.Operations)
                {
                    lines.Add(op.ToCString() ?? string.Empty);
                }
            }

            if (block.Condition != null)
            {
                lines.Add($"if ({block.Condition})");
            }

            string label = string.Join("\\l", lines.Select(EscapeDot)) + "\\l";
            sb.AppendLine($"    {nodeId} [label=\"{label}\"];");
        }

        foreach (var block in Blocks)
        {
            string from = NodeId(block);
            bool hasTwo = block.Next != null && block.ConditionalBlock != null;

            if (block.Next != null)
            {
                string to = NodeId(block.Next);
                if (hasTwo)
                {
                    sb.AppendLine($"    {from} -> {to} [color=red, label=\"fallthrough\"];");
                }
                else
                {
                    sb.AppendLine($"    {from} -> {to} [color=black];");
                }
            }

            if (block.ConditionalBlock != null)
            {
                string to = NodeId(block.ConditionalBlock);
                if (hasTwo)
                {
                    sb.AppendLine($"    {from} -> {to} [color=green, label=\"taken\"];");
                }
                else
                {
                    sb.AppendLine($"    {from} -> {to} [color=green];");
                }
            }
        }

        sb.AppendLine("}");
        File.WriteAllText(fileName, sb.ToString());
    }

    private static string NodeId(ExprBlock block) => $"eb_{block.BasicBlock.StartOffset:X6}";

    private static string EscapeDot(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
