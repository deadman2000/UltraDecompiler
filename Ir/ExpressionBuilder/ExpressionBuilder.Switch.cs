using UltraDecompiler.Ir.Expressions;
using UltraDecompiler.Ir.Switch;

namespace UltraDecompiler.Decompilation;

public partial class ExpressionBuilder
{
    private readonly Dictionary<int, ExprBlock> _blocksByOffset = [];
    private readonly Dictionary<int, QuickCSwitchPattern> _switchByEntry = [];

    private void AnalyzeQuickCSwitches(IReadOnlyList<BasicBlock> blocks)
    {
        _blocksByOffset.Clear();
        _switchByEntry.Clear();

        foreach (var block in Blocks)
        {
            _blocksByOffset[block.BasicBlock.StartOffset] = block;
        }

        foreach (var pattern in QuickCSwitchDetector.Detect(blocks))
        {
            _switchByEntry[pattern.EntryOffset] = pattern;
        }
    }

    private void CollectQuickCSwitch(
        ExprBlock entryBlock,
        QuickCSwitchPattern pattern,
        List<Operation> result,
        HashSet<ExprBlock> visited)
    {
        visited.Add(entryBlock);

        var discriminant = entryBlock.EndRegisters.Get16(pattern.DiscriminantRegister)
            ?? throw new InvalidOperationException(
                $"Не удалось восстановить discriminant switch на 0x{pattern.EntryOffset:X}.");

        if (!_blocksByOffset.TryGetValue(pattern.MergeOffset, out var mergeBlock))
        {
            throw new InvalidOperationException(
                $"Не найден merge-блок switch на 0x{pattern.MergeOffset:X}.");
        }

        var switchCases = new List<SwitchCase>();

        foreach (var casePattern in pattern.Cases)
        {
            if (!_blocksByOffset.TryGetValue(casePattern.BodyStartOffset, out var bodyBlock))
            {
                throw new InvalidOperationException(
                    $"Не найден case-блок switch на 0x{casePattern.BodyStartOffset:X}.");
            }

            var body = new List<Operation>();
            CollectOperations(bodyBlock, body, visited, stopBefore: mergeBlock);
            switchCases.Add(new SwitchCase(casePattern.Value, body));
        }

        if (!_blocksByOffset.TryGetValue(pattern.DefaultBodyOffset, out var defaultBlock))
        {
            throw new InvalidOperationException(
                $"Не найден default-блок switch на 0x{pattern.DefaultBodyOffset:X}.");
        }

        var defaultBody = new List<Operation>();
        CollectOperations(defaultBlock, defaultBody, visited, stopBefore: mergeBlock);
        switchCases.Add(new SwitchCase(null, defaultBody));

        foreach (var dispatcherOffset in pattern.DispatcherBlockOffsets)
        {
            if (_blocksByOffset.TryGetValue(dispatcherOffset, out var dispatcherBlock))
            {
                visited.Add(dispatcherBlock);
            }
        }

        result.Add(new SwitchOperation(discriminant, switchCases));
    }
}
