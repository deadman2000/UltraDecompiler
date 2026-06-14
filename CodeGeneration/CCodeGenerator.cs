using System.Text;

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
        ProcedureCodegenModel procedure,
        IReadOnlyList<Operation> operations,
        IReadOnlyList<string> includeDirectives,
        IReadOnlyList<Variable>? globals = null)
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

        AppendGlobalDeclarations(sb, globals);
        sb.Append(FormatCFunction(procedure, operations));
        return sb.ToString();
    }

    /// <summary>
    /// Один .c для round-trip: общие <c>#include</c> и все пользовательские функции подряд.
    /// QuickC 1.0 по-разному раскладывает код при линковке нескольких .obj; единый файл
    /// даёт побайтовое совпадение с исходной однофайловой сборкой.
    /// </summary>
    public static string FormatCombinedCSource(
        IReadOnlyList<(ProcedureCodegenModel Procedure, IReadOnlyList<Operation> Operations, IReadOnlyList<string> Includes)> units,
        IReadOnlyList<Variable>? globals = null)
    {
        var sb = new StringBuilder();
        var includes = units
            .SelectMany(static unit => unit.Includes)
            .Where(static directive => !directive.StartsWith('"'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static directive => directive, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var directive in includes)
        {
            sb.AppendLine($"#include {directive}");
        }

        if (includes.Count > 0)
        {
            sb.AppendLine();
        }

        AppendGlobalDeclarations(sb, globals);

        for (var i = 0; i < units.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
            }

            sb.Append(FormatCFunction(units[i].Procedure, units[i].Operations));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Имя объединённого .c по имени EXE: <c>RECUR.EXE</c> → <c>RECUR.c</c>.
    /// Stem должен укладываться в DOS 8.3
    /// </summary>
    public static string FormatCombinedSourceFileName(string exeFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeFileName);
        return $"{Path.GetFileNameWithoutExtension(exeFileName)}.c";
    }

    /// <summary>
    /// Форматирует процедуру (с её сигнатурой) и готовый список операций в текст C-функции.
    /// Добавляет заголовок функции, тело с отступами и закрывающую скобку.
    /// </summary>
    public static string FormatCFunction(
        ProcedureCodegenModel procedure,
        IReadOnlyList<Operation> operations)
    {
        var sb = new StringBuilder();
        var returnType = procedure.Signature.ReturnType.ToString();
        var parameters = FormatParameterList(procedure);
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
                    if (procedure.Signature.ReturnType.IsVoid)
                    {
                        // QuickC: void без return в исходнике выходит линейным RET; явный return — JMP на эпилог.
                        if (!retOp.IsExplicit)
                        {
                            continue;
                        }

                        sb.AppendLine("    return;");
                    }
                    else if (retOp.Value == null)
                    {
                        sb.AppendLine("    return;");
                    }
                    else
                    {
                        sb.AppendLine($"    return {retOp.Value.RenderExpr()};");
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
        ProcedureCodegenModel procedure,
        IReadOnlyList<Operation> operations)
    {
        var paramVars = procedure.Parameters.Select(static p => p.Variable);
        var locals = UsedVariableCollector.Collect(operations, paramVars, procedure.StackLocals);

        foreach (var variable in locals)
        {
            sb.AppendLine(FormatLocalDeclaration(variable));
        }

        if (locals.Count > 0 && operations.Count > 0)
        {
            sb.AppendLine();
        }
    }

    private static void AppendGlobalDeclarations(StringBuilder sb, IReadOnlyList<Variable>? globals)
    {
        if (globals is not { Count: > 0 })
        {
            return;
        }

        foreach (var global in globals)
        {
            sb.AppendLine(FormatGlobalDeclaration(global));
        }

        sb.AppendLine();
    }

    private static string FormatGlobalDeclaration(Variable global)
    {
        var initializer = global.InitialValue is int value ? $" = {value}" : string.Empty;
        return $"{global.DeclaredType} {global.Name}{initializer};";
    }

    private static string FormatLocalDeclaration(Variable variable)
    {
        if (variable.ArraySize is int size)
        {
            // TODO подставлять тип
            return $"    char {variable}[{size}];";
        }

        if (variable.Type?.IsCharFarPtr == true && variable.FarPointerInitializer is uint initializer)
        {
            return $"    char far *{variable} = (char far *)0x{initializer:X8}L;";
        }

        return $"    {variable.DeclaredType} {variable};";
    }

    private static string FormatParameterList(ProcedureCodegenModel procedure)
    {
        if (procedure.Signature.Parameters.Count == 0)
        {
            return "void";
        }

        if (procedure.Name == "main")
        {
            return FormatMainParameterList(procedure.Signature);
        }

        return string.Join(
            ", ",
            procedure.Signature.Parameters.Select(static (p, i) => $"{p.Type} arg{i}"));
    }

    private static string FormatMainParameterList(ProcedureSignature signature)
    {
        var parts = new List<string> { "int argc", "char *argv[]" };
        if (signature.Parameters.Count >= 3)
        {
            parts.Add("char *envp[]");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Формирует содержимое заголовочного файла с прототипом процедуры.
    /// </summary>
    public static string FormatHeaderFile(ProcedureCodegenModel procedure)
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
    public static string FormatHeaderDeclaration(ProcedureCodegenModel procedure)
    {
        var returnType = procedure.Signature.ReturnType.ToString();
        var parameters = FormatParameterList(procedure);
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
