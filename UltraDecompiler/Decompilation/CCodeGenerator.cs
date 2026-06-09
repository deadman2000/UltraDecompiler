using System.Text;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Генератор исходного кода на C из декомпилированных процедур и IR-операций.
/// Отвечает за построение CFG и ExpressionBuilder для отдельной процедуры,
/// а также за форматирование операций в текст C-функции с сигнатурой.
/// </summary>
public static class CCodeGenerator
{
    /// <summary>
    /// Форматирует процедуру (с её сигнатурой) и готовый список операций в текст C-функции.
    /// Добавляет заголовок функции, тело с отступами и закрывающую скобку.
    /// </summary>
    public static string FormatCFunction(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations)
    {
        var sb = new StringBuilder();
        var returnType = procedure.Signature.ReturnType.ToString();
        var parameters = FormatParameterList(procedure.Signature);
        sb.AppendLine($"{returnType} {procedure.Name}({parameters})");
        sb.AppendLine("{");

        if (operations.Count == 0)
        {
            sb.AppendLine("    ;");
        }
        else
        {
            foreach (var operation in operations)
            {
                if (operation is ReturnOperation retOp)
                {
                    // Для void — всегда голый return, даже если ReturnOperation несёт значение AX.
                    // Это гарантирует корректный C-код. Value из AX сохраняется в IR для анализа/тестов.
                    if (procedure.Signature.ReturnType.IsVoid || retOp.Value == null)
                    {
                        sb.AppendLine("    return;");
                    }
                    else
                    {
                        sb.AppendLine($"    return {retOp.Value};");
                    }
                }
                else
                {
                    operation.AppendToCString(sb, indent: 1, asStatement: true);
                }
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string FormatParameterList(ProcedureSignature signature)
    {
        if (signature.Parameters.Count == 0)
        {
            return "void";
        }

        return string.Join(", ", signature.Parameters.Select(static (p, i) => $"{p.Type} arg{i}"));
    }

    /// <summary>
    /// Формирует имя выходного .c файла для процедуры.
    /// Если имя валидное для идентификатора C (начинается с буквы или _), использует его как base;
    /// иначе добавляет суффикс со смещением (для синтетических sub_XXXX).
    /// </summary>
    public static string FormatOutputFileName(string name, int offset)
    {
        if (name.StartsWith('_') || char.IsLetter(name[0]))
        {
            return $"{name}.c";
        }

        return $"{name}_{offset:X4}.c";
    }
}
