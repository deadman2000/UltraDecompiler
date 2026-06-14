namespace UltraDecompiler.PostProcessing.Infrastructure;

/// <summary>
/// Общие операции обхода <see cref="SwitchOperation"/> в дереве IR.
/// </summary>
internal static class OperationTreeMapper
{
    /// <summary>Применяет <paramref name="mapBodies"/> к телам всех веток switch.</summary>
    public static SwitchOperation MapSwitchBodies(
        SwitchOperation sw,
        Func<IReadOnlyList<Operation>, IReadOnlyList<Operation>> mapBodies) =>
        new(
            sw.Discriminant,
            sw.Cases.Select(c => new SwitchCase(c.Value, mapBodies(c.Body))).ToList());

    /// <summary>Проверяет использование переменной в discriminant и телах switch.</summary>
    public static bool SwitchUsesVariable(
        SwitchOperation sw,
        Variable variable,
        Func<Operation, Variable, bool> readsOperation) =>
        ExprSubstitution.Contains(sw.Discriminant, variable)
        || sw.Cases.Any(c => c.Body.Any(op => readsOperation(op, variable)));
}
