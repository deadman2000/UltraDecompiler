using UltraDecompiler.Common;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Выполняет построение IR для программы, скомпилированной в QuickC с включенной оптимизацией (/Ot, /Ox).
/// Использует упрощённые эвристики, так как оптимизированный код имеет другую структуру:
/// - Отсутствие стандартных эпилогов функций
/// - Использование регистров вместо стека для аргументов
/// - Более сложные паттерны циклов
/// </summary>
public partial class ExpressionBuilderQuickCOpt(ControlFlowGraph graph) : ExpressionBuilder(graph)
{
    // Базовая реализация ApplyBlockPatterns и AnalyzeSwitchPatterns — пустая,
    // так как оптимизированный код не следует стандартным паттернам QuickC /Od

    /// <inheritdoc />
    protected override void OptimizeIncDecPatterns()
    {
        // Не применяется для /Ox: QuickC оптимизирует a = a + 1 в INC,
        // и мы должны сохранить исходную семантику выражения
    }
}
