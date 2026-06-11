using System.Text;
using UltraDecompiler.Decompilation;
using UltraDecompiler.PostProcessing;

namespace UltraDecompiler.CodeGeneration;

/// <summary>
/// Генератор исходного кода на C из декомпилированных процедур и IR-операций.
/// Отвечает за построение CFG и ExpressionBuilder для отдельной процедуры,
/// а также за форматирование операций в текст C-функции с сигнатурой.
/// </summary>
public static class CCodeGenerator
{
    /// <summary>
    /// Формирует полный C-файл: директивы <c>#include</c> и определение функции.
    /// </summary>
    /// <param name="includeDirectives">Фрагменты после <c>#include</c> (например <c>&lt;STDIO.H&gt;</c>).</param>
    public static string FormatCSource(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations,
        IReadOnlyList<string> includeDirectives)
    {
        var sb = new StringBuilder();

        foreach (var directive in includeDirectives)
        {
            sb.AppendLine($"#include {directive}");
        }

        if (includeDirectives.Count > 0)
        {
            sb.AppendLine();
        }

        sb.Append(FormatCFunction(procedure, operations));
        return sb.ToString();
    }

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

        AppendLocalVariableDeclarations(sb, procedure, operations);

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

    /// <summary>
    /// Добавляет объявления локальных переменных (int), используемых в теле функции.
    /// Параметры из сигнатуры не дублируются.
    /// </summary>
    private static void AppendLocalVariableDeclarations(
        StringBuilder sb,
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations)
    {
        if (procedure.Expressions == null)
            return;

        var parameters = procedure.Expressions.Parameters.Select(static p => p.Variable);
        var stackLocals = procedure.Expressions.Variables.StackLocals.Select(static e => e.Variable);
        var locals = UsedVariableCollector.Collect(operations, parameters, stackLocals);

        foreach (var variable in locals)
        {
            sb.AppendLine(FormatLocalDeclaration(variable));
        }

        if (locals.Count > 0 && operations.Count > 0)
        {
            sb.AppendLine();
        }
    }

    private static string FormatLocalDeclaration(Variable variable)
    {
        if (variable.ArraySize is int size)
        {
            // TODO подставлять тип
            return $"    char {variable}[{size}];";
        }

        return $"    {variable.DeclaredType} {variable};";
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
    /// Формирует содержимое заголовочного файла с прототипом процедуры.
    /// </summary>
    public static string FormatHeaderFile(DisassembledProcedure procedure)
    {
        var guard = FormatIncludeGuard(procedure.Name, procedure.Offset);
        var sb = new StringBuilder();
        sb.AppendLine($"#ifndef {guard}");
        sb.AppendLine($"#define {guard}");
        sb.AppendLine();
        sb.AppendLine(FormatHeaderDeclaration(procedure));
        sb.AppendLine();
        sb.AppendLine("#endif");
        return sb.ToString();
    }

    /// <summary>Строка объявления функции для заголовка (с точкой с запятой).</summary>
    public static string FormatHeaderDeclaration(DisassembledProcedure procedure)
    {
        var returnType = procedure.Signature.ReturnType.ToString();
        var parameters = FormatParameterList(procedure.Signature);
        return $"{returnType} {procedure.Name}({parameters});";
    }

    /// <summary>
    /// Формирует имя выходного .c файла для процедуры.
    /// Если имя валидное для идентификатора C (начинается с буквы или _), использует его как base;
    /// иначе добавляет суффикс со смещением (для синтетических sub_XXXX).
    /// </summary>
    public static string FormatOutputFileName(string name, int offset) =>
        FormatBaseFileName(name, offset, ".c");

    /// <summary>Формирует имя выходного .h файла для процедуры.</summary>
    public static string FormatHeaderFileName(string name, int offset) =>
        FormatBaseFileName(name, offset, ".h");

    private static string FormatBaseFileName(string name, int offset, string extension)
    {
        if (name.StartsWith('_') || char.IsLetter(name[0]))
        {
            return $"{name}{extension}";
        }

        return $"{name}_{offset:X4}{extension}";
    }

    private static string FormatIncludeGuard(string name, int offset)
    {
        var baseName = FormatBaseFileName(name, offset, string.Empty)
            .Replace('.', '_')
            .ToUpperInvariant();
        return $"{baseName}_H";
    }
}
