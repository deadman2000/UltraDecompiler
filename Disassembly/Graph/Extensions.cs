using System.Text;

namespace UltraDecompiler.Disassembly.Graph;

public static class Extensions
{
    extension(ControlFlowGraph graph)
    {
        /// <summary>
        /// Сохраняет граф в формате DOT
        /// </summary>
        /// <param name="fileName"></param>
        public void SaveDot(string fileName)
        {
            if (graph.Blocks.Count == 0)
            {
                File.WriteAllText(fileName, "digraph empty {}");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("digraph ControlFlowGraph {");
            sb.AppendLine("    rankdir=TB;");
            sb.AppendLine("    node [shape=box, fontname=\"Consolas\", fontsize=8, style=filled, fillcolor=\"#FFFACD\"];");
            sb.AppendLine("    edge [fontname=\"Consolas\", fontsize=7];");

            // Узлы - блоки с ассемблерным кодом
            foreach (var block in graph.Blocks)
            {
                string nodeId = $"b_{block.StartOffset:X6}";
                var lines = new List<string> { $"0x{block.StartOffset:X4}..0x{block.EndOffset:X4}" };
                foreach (var instr in block.Instructions)
                {
                    string line = instr.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"");
                    lines.Add(line);
                }
                string label = string.Join("\\l", lines) + "\\l";
                sb.AppendLine($"    {nodeId} [label=\"{label}\"];");
            }

            // Стрелки: один переход - чёрная, два - зелёная (taken) и красная (fallthrough)
            foreach (var block in graph.Blocks)
            {
                string from = $"b_{block.StartOffset:X6}";
                bool hasTwo = block.NextBlock != null && block.ConditionalBlock != null;

                if (block.NextBlock != null)
                {
                    string to = $"b_{block.NextBlock.StartOffset:X6}";
                    if (hasTwo)
                    {
                        sb.AppendLine($"    {from} -> {to} [color=red];");
                    }
                    else
                    {
                        sb.AppendLine($"    {from} -> {to} [color=black];");
                    }
                }

                if (block.ConditionalBlock != null)
                {
                    string to = $"b_{block.ConditionalBlock.StartOffset:X6}";
                    sb.AppendLine($"    {from} -> {to} [color=green];");
                }
            }

            sb.AppendLine("}");
            File.WriteAllText(fileName, sb.ToString());
        }
    }
}
