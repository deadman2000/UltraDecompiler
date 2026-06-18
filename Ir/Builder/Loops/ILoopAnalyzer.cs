namespace UltraDecompiler.Ir.Builder.Loops;

/// <summary>
/// Интерфейс анализатора циклов для конкретного профиля компиляции (QuickC /Od, /Ox и т.д.).
/// </summary>
public interface ILoopAnalyzer
{
    /// <summary>
    /// Анализирует блок-заголовок цикла и определяет его тип и параметры.
    /// </summary>
    /// <param name="headerBlock">Блок-заголовок цикла (содержит условие продолжения).</param>
    /// <param name="allBlocks">Все блоки функции (для поиска достижимости).</param>
    /// <param name="visitedBlocks">Уже посещённые блоки (для предотвращения дублирования).</param>
    /// <param name="enclosingLoopHeader">Заголовок внешнего цикла (для распознавания break/continue).</param>
    /// <returns>Результат анализа или null, если цикл не распознан.</returns>
    LoopAnalysisResult? Analyze(
        ExprBlock headerBlock,
        IReadOnlyList<ExprBlock> allBlocks,
        HashSet<ExprBlock> visitedBlocks,
        ExprBlock? enclosingLoopHeader = null);

    /// <summary>
    /// Определяет, является ли блок заголовком цикла согласно профилю компиляции.
    /// </summary>
    bool IsLoopHeader(ExprBlock block, ExprBlock? enclosingLoopExit = null, ExprBlock? enclosingLoopHeader = null);
}
