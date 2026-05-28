using System.Text;

namespace UltraDecompiler.Decompilation.Operations;

/// <summary>
/// Общее форматирование тел операций управления потоком (while/for/if).
/// </summary>
static class ControlFlowBodyFormatter
{
    public static void AppendBody(StringBuilder sb, IReadOnlyList<Operation> body, int indent)
    {
        string innerIndent = new(' ', (indent + 1) * 4);

        if (body.Count == 0)
        {
            sb.AppendLine($"{innerIndent}; // пустое тело");
            return;
        }

        foreach (var op in body)
        {
            switch (op)
            {
                case WhileOperation wo:
                    sb.Append(wo.ToString(indent + 1));
                    break;
                case ForOperation fo:
                    sb.Append(fo.ToString(indent + 1));
                    break;
                case IfOperation io:
                    sb.Append(io.ToString(indent + 1));
                    break;
                default:
                    sb.AppendLine($"{innerIndent}{op};");
                    break;
            }
        }
    }
}
