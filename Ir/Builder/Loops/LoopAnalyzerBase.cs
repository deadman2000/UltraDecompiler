namespace UltraDecompiler.Ir.Builder.Loops;

/// <summary>
/// Базовый класс для анализаторов циклов QuickC.
/// Содержит общую логику распознавания структуры цикла.
/// </summary>
public abstract class LoopAnalyzerBase : ILoopAnalyzer
{
    /// <summary>
    /// Анализирует блок-заголовок цикла и определяет его тип и параметры.
    /// </summary>
    public abstract LoopAnalysisResult? Analyze(
        ExprBlock headerBlock,
        IReadOnlyList<ExprBlock> allBlocks,
        HashSet<ExprBlock> visitedBlocks,
        ExprBlock? enclosingLoopHeader = null);

    /// <summary>
    /// Определяет, является ли блок заголовком цикла согласно профилю компиляции.
    /// </summary>
    public abstract bool IsLoopHeader(ExprBlock block, ExprBlock? enclosingLoopExit = null, ExprBlock? enclosingLoopHeader = null);
}
