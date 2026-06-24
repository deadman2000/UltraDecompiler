namespace UltraDecompiler.Ir.Builder.Loops;

/// <summary>
/// Анализатор циклов для кода QuickC с оптимизацией (/Ot, /Ox).
/// Распознаёт циклы со счётчиком в регистрах (SI, DI, BX, CX),
/// паттерны с and reg,reg + jg, inc/dec вместо add/sub.
/// </summary>
public sealed class QuickCOptLoopAnalyzer : LoopAnalyzerBase
{
    public override LoopAnalysisResult? Analyze(
        ExprBlock headerBlock,
        IReadOnlyList<ExprBlock> allBlocks,
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
