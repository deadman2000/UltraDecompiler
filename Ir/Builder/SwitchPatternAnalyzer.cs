using UltraDecompiler.Ir.Switch;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Распознаёт switch-паттерны QuickC в графе потока управления.
/// 
/// Задачи:
/// - Заполняет <see cref="BlocksByOffset"/> — отображение offset → ExprBlock
/// - Заполняет <see cref="SwitchByEntry"/> — распознаёт switch-паттерны через <see cref="QuickCSwitchDetector"/>
/// 
/// Используется <see cref="ExpressionBuilder"/> после построения IR-дерева.
/// Результаты хранятся в полях класса и читаются из <see cref="OperationFlattener"/>.
/// </summary>
public class SwitchPatternAnalyzer
{
    /// <summary>
    /// Отображение смещения базового блока на соответствующий ExprBlock.
    /// Используется для поиска блоков по смещению при построении switch-операции.
    /// </summary>
    public Dictionary<int, ExprBlock> BlocksByOffset { get; } = [];

    /// <summary>
    /// Отображение точки входа switch-конструкции на её паттерн.
    /// Ключ — смещение заголовочного блока switch (первый cmp).
    /// </summary>
    public Dictionary<int, QuickCSwitchPattern> SwitchByEntry { get; } = [];

    /// <summary>
    /// Распознаёт switch-паттерны в графе потока управления.
    /// </summary>
    /// <param name="blocks">Все блоки графа потока управления</param>
    /// <param name="exprBlocks">Все построенные ExprBlock'и</param>
    public void Analyze(IReadOnlyList<BasicBlock> blocks, IReadOnlyList<ExprBlock> exprBlocks)
    {
        BlocksByOffset.Clear();
        SwitchByEntry.Clear();

        foreach (var block in exprBlocks)
        {
            BlocksByOffset[block.BasicBlock.StartOffset] = block;
        }

        foreach (var pattern in QuickCSwitchDetector.Detect(blocks))
        {
            SwitchByEntry[pattern.EntryOffset] = pattern;
        }
    }
}
