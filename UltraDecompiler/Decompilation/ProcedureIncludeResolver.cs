using UltraDecompiler.CodeGeneration;
using UltraDecompiler.Headers;
using UltraDecompiler.PostProcessing;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Определяет директивы <c>#include</c> для сгенерированного C-файла процедуры.
/// </summary>
public static class ProcedureIncludeResolver
{
    /// <summary>
    /// Строит список include-директив: сначала заголовки QuickC INCLUDE, затем локальные <c>.h</c> пользовательских процедур.
    /// Каждый элемент — фрагмент после <c>#include</c> (например <c>&lt;STDIO.H&gt;</c> или <c>"sub_0010.h"</c>).
    /// </summary>
    public static IReadOnlyList<string> ResolveIncludes(
        DisassembledProcedure procedure,
        IReadOnlyList<string> calleeNames,
        ProcedureStorage storage,
        HeaderCatalog catalog)
    {
        var libraryHeaders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var userHeaders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var name in calleeNames)
        {
            if (string.Equals(name, procedure.Name, StringComparison.Ordinal))
            {
                continue;
            }

            if (ShouldSkipInclude(name))
            {
                continue;
            }

            if (storage.TryGetByName(name, out var callee) && callee is { IsLibrary: false })
            {
                userHeaders.Add(CCodeGenerator.FormatHeaderFileName(callee.Name, callee.Offset));
                continue;
            }

            if (catalog.TryGetHeaderFile(name, out var headerFile) && headerFile is not null)
            {
                libraryHeaders.Add(headerFile);
            }
        }

        var result = new List<string>(libraryHeaders.Count + userHeaders.Count);
        result.AddRange(libraryHeaders.Select(static h => $"<{h}>"));
        result.AddRange(userHeaders.Select(static h => $"\"{h}\""));
        return result;
    }

    private static bool ShouldSkipInclude(string name) =>
        StackCheckDetector.IsChkstkName(name)
        || name is "__exit" or "_disable" or "_enable"
        || name.StartsWith("indirect_", StringComparison.Ordinal)
        || name.StartsWith("unknown_", StringComparison.Ordinal)
        || name.StartsWith("far_", StringComparison.Ordinal);
}
