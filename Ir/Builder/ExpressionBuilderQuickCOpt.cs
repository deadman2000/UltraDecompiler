namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Выполняет построение IR для программы, скомпилированной в QuickC с включенной оптимизацией (/Ot, /Ox).
/// Использует упрощённые эвристики, так как оптимизированный код имеет другую структуру:
/// - Отсутствие стандартных эпилогов функций
/// - Использование регистров вместо стека для аргументов
/// - Более сложные паттерны циклов
/// </summary>
public partial class ExpressionBuilderQuickCOpt(ControlFlowGraph graph, Func<int, string>? calleeNameResolver = null)
    : ExpressionBuilder(graph, calleeNameResolver)
{
    /// <inheritdoc />
    protected override void OptimizeOxPatterns()
    {
        OptimizeSiDiStackAliases();
    }

    /// <inheritdoc />
    protected override void OptimizeIncDecPatterns()
    {
        foreach (var block in Blocks)
        {
            OptimizeIncDecPatternsInBlock(block, IncDecPatternOptions.Optimized);
        }
    }
}
