using UltraDecompiler.Ir.Builder.Patterns;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Выполняет построение IR для программы, скомпилированной в QuickC с отключенной оптимизацией (/Od).
/// Применяет специфичные паттерны декомпиляции для кода без оптимизации:
/// - Распознавание switch-конструкций QuickC
/// - Паттерны push-аргументов через локальные переменные
/// - Специфичные эвристики для циклов
/// </summary>
public class ExpressionBuilderQuickCUnopt : ExpressionBuilder
{
    /// <summary>
    /// Применяет паттерны QuickC /Od к блоку:
    /// - <see cref="StackLocalPushArgPattern"/> — подстановка локалей вместо push reg
    /// </summary>
    protected override void ApplyBlockPatterns(ExprBlock block)
    {
        StackLocalPushArgPattern.Apply(block);
    }

    /// <summary>
    /// Распознаёт switch-паттерны QuickC /Od (таблицы переходов через jmp table).
    /// </summary>
    protected override void AnalyzeSwitchPatterns(IReadOnlyList<BasicBlock> blocks)
    {
        base.AnalyzeSwitchPatterns(blocks);
        // Дополнительная логика может быть добавлена здесь при необходимости
    }

    /// <summary>
    /// Для /Od кода эвристика распознавания циклов более агрессивная,
    /// так как компилятор генерирует явные inc/dec в теле цикла.
    /// </summary>
    protected override bool LoopBodyAdvancesPointer(ExprBlock bodyStart)
    {
        var ops = new List<Operation>();
        var visited = new HashSet<ExprBlock>();
        CollectOperationsStatic(bodyStart, ops, visited, stopBefore: null, maxBlocks: 8);
        return ops.Any(static op => op is IncOperation or DecOperation);
    }

    /// <summary>
    /// Для /Od кода заголовок цикла определяется по наличию обратного ребра.
    /// </summary>
    protected override bool IsLoopHeader(ExprBlock block, ExprBlock? thenStart)
    {
        if (thenStart is null)
        {
            return false;
        }

        return CollectReachable(thenStart).Contains(block);
    }

}
