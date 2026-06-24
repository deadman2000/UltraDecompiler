namespace UltraDecompiler.Ir.Builder.Loops;

/// <summary>
/// Анализатор циклов для кода QuickC без оптимизации (/Od).
/// Распознаёт классические паттерны: for с переменной на стеке, while по указателю, do-while.
/// </summary>
public sealed class QuickCUnoptLoopAnalyzer : LoopAnalyzerBase
{
    public override LoopAnalysisResult? Analyze(
        ExprBlock headerBlock, IReadOnlyList<ExprBlock> allBlocks,
        HashSet<ExprBlock> visitedBlocks,
        ExprBlock? enclosingLoopHeader = null)
    {
        throw new NotImplementedException();
    }

    public override bool IsLoopHeader(
        ExprBlock block,
        ExprBlock? enclosingLoopExit = null,
        ExprBlock? enclosingLoopHeader = null)
    {
        return false;
    }
}
