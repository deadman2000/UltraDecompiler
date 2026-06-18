namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Выполняет построение IR для программы, скомпилированной в QuickC с включенной оптимизацией (/Ot, /Ox).
/// Использует упрощённые эвристики, так как оптимизированный код имеет другую структуру:
/// - Отсутствие стандартных эпилогов функций
/// - Использование регистров вместо стека для аргументов
/// - Более сложные паттерны циклов
/// </summary>
public partial class ExpressionBuilderQuickCOpt : ExpressionBuilder
{
    /// <summary>
    /// Инициализирует анализатор циклов для /Ox.
    /// </summary>
    public ExpressionBuilderQuickCOpt()
    {
        LoopAnalyzer = new Loops.QuickCOptLoopAnalyzer();
    }

    // Базовая реализация ApplyBlockPatterns и AnalyzeSwitchPatterns — пустая,
    // так как оптимизированный код не следует стандартным паттернам QuickC /Od
}
