using UltraDecompiler.Ir.Builder;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Собирает имена вызываемых процедур из дерева IR-операций.
/// </summary>
public static class ProcedureDependencyCollector
{
    /// <summary>
    /// Возвращает отсортированный список уникальных имён callee из <paramref name="operations"/>.
    /// </summary>
    public static IReadOnlyList<string> Collect(IReadOnlyList<Operation> operations)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var operation in ExpressionBuilder.EnumerateNested(operations))
        {
            switch (operation)
            {
                case SetOperation { Src: CallExpr call }:
                    names.Add(call.Name);
                    break;
                case CallOperation call:
                    names.Add(call.Name);
                    break;
            }
        }

        return names.OrderBy(static n => n, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Возвращает имена пользовательских процедур, на которые ссылается хотя бы один вызов
    /// из другой пользовательской процедуры.
    /// </summary>
    public static IReadOnlySet<string> CollectReferencedUserProcedureNames(
        IEnumerable<DisassembledProcedure> userProcedures,
        ProcedureStorage storage)
    {
        var referenced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var procedure in userProcedures)
        {
            foreach (var calleeName in procedure.Callees)
            {
                if (storage.TryGetByName(calleeName, out var callee) && callee is { IsLibrary: false })
                {
                    referenced.Add(calleeName);
                }
            }
        }

        return referenced;
    }
}
