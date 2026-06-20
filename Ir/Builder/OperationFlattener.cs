using UltraDecompiler.Ir.Builder.Loops;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Преобразует IR-дерево (<see cref="ExprBlock"/>) в линейный список операций (flat <see cref="Operation"/>).
/// </summary>
public partial class OperationFlattener
{
    private readonly ExpressionBuilder _builder;
    private readonly ILoopAnalyzer _loopAnalyzer;
    private readonly IReadOnlyList<BasicBlock> _cfgBlocks;

    /// <summary>
    /// Создаёт flatten-обработчик для заданного <see cref="ExpressionBuilder"/> и анализатора циклов.
    /// </summary>
    /// <param name="builder">Построенный ExpressionBuilder с IR-деревом (Blocks и т.д.)</param>
    /// <param name="cfgBlocks">Базовые блоки CFG</param>
    /// <param name="loopAnalyzer">Анализатор циклов для текущего профиля оптимизации (/Od или /Ox)</param>
    public OperationFlattener(ExpressionBuilder builder, IReadOnlyList<BasicBlock> cfgBlocks, ILoopAnalyzer loopAnalyzer)
    {
        _builder = builder;
        _cfgBlocks = cfgBlocks;
        _loopAnalyzer = loopAnalyzer;
    }

    /// <summary>
    /// Возвращает линейный список операций всей декомпилированной программы.
    ///
    /// Обходит дерево <see cref="ExprBlock"/> от точки входа, разворачивая связи
    /// <see cref="ExprBlock.ConditionalBlock"/> / <see cref="ExprBlock.Next"/> в
    /// <see cref="IfOperation"/> (ветка «истина» — переход по условию, «ложь» — fallthrough).
    /// Операции управления потоком, уже вложенные в <see cref="ExprBlock.Operations"/>
    /// (<see cref="WhileOperation"/>, <see cref="ForOperation"/>), сохраняются как есть.
    /// </summary>
    public IReadOnlyList<Operation> GetAllOperations()
    {
        var entryBlock = _builder.EntryBlock;
        if (entryBlock == null)
            return [];

        var result = new List<Operation>();
        var visited = new HashSet<ExprBlock>();
        CollectOperations(entryBlock, result, visited);
        return result;
    }

    /// <summary>
    /// Рекурсивно перечисляет все операции, включая тела <see cref="IfOperation"/>,
    /// <see cref="WhileOperation"/> и <see cref="ForOperation"/>.
    /// </summary>
    public static IEnumerable<Operation> EnumerateNested(IEnumerable<Operation> operations)
    {
        foreach (var op in operations)
        {
            yield return op;

            switch (op)
            {
                case IfOperation i:
                    foreach (var nested in EnumerateNested(i.ThenBody))
                        yield return nested;
                    if (i.ElseBody != null)
                    {
                        foreach (var nested in EnumerateNested(i.ElseBody))
                            yield return nested;
                    }
                    break;
                case WhileOperation w:
                    foreach (var nested in EnumerateNested(w.Body))
                        yield return nested;
                    break;
                case DoWhileOperation d:
                    foreach (var nested in EnumerateNested(d.Body))
                        yield return nested;
                    break;
                case BreakOperation:
                    break;
                case ContinueOperation:
                case GotoOperation:
                case LabelOperation:
                    break;
                case ForOperation f:
                    if (f.Init != null)
                        yield return f.Init;
                    foreach (var nested in EnumerateNested(f.Body))
                        yield return nested;
                    if (f.Iteration != null)
                        yield return f.Iteration;
                    break;
                case SwitchOperation s:
                    foreach (var switchCase in s.Cases)
                    {
                        foreach (var nested in EnumerateNested(switchCase.Body))
                            yield return nested;
                    }
                    break;
            }
        }
    }
}
