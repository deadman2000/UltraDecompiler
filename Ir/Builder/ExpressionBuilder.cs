using System.Diagnostics;
using UltraDecompiler.Common;
using UltraDecompiler.Ir.InstructionHandlers;
using UltraDecompiler.Ir.Variables;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Выполняет преобразование потока инструкций x86 (через CFG) в высокоуровневые
/// выражения и операции (SetOperation, Math1Expr, Math2Expr и т.д.).
/// 
/// Этот класс отвечает ТОЛЬКО за генерацию IR-дерева (ExprBlock) из CFG:
/// - Символическое выполнение инструкций в каждом базовом блоке
/// - Создание Operations (SetOperation, StoreOperation и т.д.) в ExprBlock
/// - Построение связей Next/ConditionalBlock между ExprBlock'ами
/// </summary>
public partial class ExpressionBuilder
{
    private readonly ControlFlowGraph _graph;

    private readonly Dictionary<BasicBlock, ExprBlock> _blocksMap = [];
    private readonly Queue<ExprBlock> _queue = new();
    private ExprBlock? _entryBlock;
    private IReadOnlyDictionary<BasicBlock, List<BasicBlock>> _predecessors =
        new Dictionary<BasicBlock, List<BasicBlock>>();

    internal ExpressionBuilder(ControlFlowGraph graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Точка входа IR-дерева (первый построенный ExprBlock).
    /// </summary>
    internal ExprBlock? EntryBlock => _entryBlock;

    public List<ExprBlock> Blocks { get; } = [];

    public VariableStorage Variables { get; } = new();

    public Stack<Expr> InitialStack { get; } = new();

    public static ExpressionBuilder Create(ControlFlowGraph cfg, OptimizationLevel optimization)
    {
        return optimization switch
        {
            OptimizationLevel.Disabled => new ExpressionBuilderQuickCUnopt(cfg),
            OptimizationLevel.Enabled or OptimizationLevel.EnableLoop or OptimizationLevel.EnabledFull => new ExpressionBuilderQuickCOpt(cfg),
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Выполняет декомпиляцию всего графа потока управления.
    /// 
    /// Алгоритм:
    /// 1. Выбираем начальное символическое состояние регистров (разное для .COM и .EXE).
    /// 2. Обходим все BasicBlock'и в ширину (BFS), начиная с EntryBlock.
    /// 3. Для каждого блока вызываем GenerateCode, который выполняет symbolic execution
    ///    инструкций и заполняет ExprBlock.Operations.
    /// 4. После обхода связываем ExprBlock'и между собой по ссылкам из CFG
    ///    (Next и ConditionalBlock).
    /// 
    /// Если передан <paramref name="procedures"/>, после построения блоков автоматически
    /// выполняется CallSiteResolver для подстановки имён (в т.ч. библиотечных) и аргументов в CallExpr.
    /// </summary>
    /// <param name="graph">Построенный граф потока управления</param>
    public void Build()
    {
        Blocks.Clear();
        _blocksMap.Clear();
        _queue.Clear();
        _entryBlock = null;

        AnalyzeFunctionParameters(_graph);
        _predecessors = BuildPredecessors(_graph.Blocks);

        // Формируем первый блок и добавляем его в очередь на обработку
        CreateExprBlock(_graph.EntryBlock, InitialStack);

        var visited = new HashSet<BasicBlock>();

        // Обход в ширину (BFS) с правильной передачей данных между блоками.
        while (_queue.Count > 0)
        {
            var block = _queue.Dequeue();
            if (visited.Contains(block.BasicBlock))
                continue;

            visited.Add(block.BasicBlock);

            GenerateCode(block);

            // Передаём выходное состояние successor'ам.
            if (block.BasicBlock.NextBlock != null)
            {
                CreateExprBlock(block.BasicBlock.NextBlock, block.EndStack);
            }

            if (block.BasicBlock.ConditionalBlock != null)
            {
                CreateExprBlock(block.BasicBlock.ConditionalBlock, block.EndStack);
            }
        }

        // Второй проход: связываем ExprBlock'и между собой.
        foreach (var kvp in _blocksMap)
        {
            var exprBlock = kvp.Value;
            var basicBlock = kvp.Key;

            if (basicBlock.NextBlock != null && _blocksMap.TryGetValue(basicBlock.NextBlock, out var nextCode))
                exprBlock.Next = nextCode;

            if (basicBlock.ConditionalBlock != null && _blocksMap.TryGetValue(basicBlock.ConditionalBlock, out var condCode))
            {
                exprBlock.ConditionalBlock = condCode;
            }
        }
    }

    public void OptimizeEpilogue()
    {
        InsertTailReturnsBeforeEpilogue();
        RemoveSharedEpilogueBlocks();
    }

    public void Optimize(bool varUsage = true)
    {
        OptimizeEpilogue();

        if (varUsage)
        {
            OptimizeRegisterChains();
            RemoveUnusedSets();
        }
    }

    private void CreateExprBlock(BasicBlock block, IEnumerable<Expr> stack)
    {
        // TODO Подумать, что делать, если в блок мы попадаем из разных мест
        if (_blocksMap.ContainsKey(block))
            return;

        var exprBlock = new ExprBlock(block)
        {
            Variables = Variables,
            InitStack = stack.ToArray(),
        };
        Blocks.Add(exprBlock);
        _blocksMap[block] = exprBlock;
        _entryBlock ??= exprBlock;
        _queue.Enqueue(exprBlock);
    }

    /// <summary>
    /// Выполняет symbolic execution инструкций одного базового блока.
    /// </summary>
    private ExprBlock GenerateCode(ExprBlock block)
    {
        Debug.Assert(block.BasicBlock.EndOffset != -1);

        // Копируем символический стек. Reverse() нужен, потому что конструктор Stack<T>(IEnumerable)
        // кладёт первый элемент перечисления на дно, а последний — на вершину.
        // InitStack[0] — самый глубокий, InitStack[^1] — вершина (результат последнего PUSH).
        block.EndStack = new Stack<Expr>(block.InitStack.Reverse());

        // Определяем диапазоны пролога и эпилога для специальной обработки
        var prologueRange = GetPrologueRange(block.BasicBlock.Instructions);
        var epilogueRange = GetEpilogueRange(block.BasicBlock.Instructions);

        Mnemonic? previousMnemonic = null;

        for (var i = 0; i < block.BasicBlock.Instructions.Count; i++)
        {
            var instr = block.BasicBlock.Instructions[i];
            block.PreviousMnemonic = previousMnemonic;

            // === Специальная обработка пролога ===
            // Инструкции пролога (push bp, mov bp, sp, sub sp, N) не создают операций IR,
            // а только настраивают символическое состояние регистров.
            if (prologueRange.HasValue && i >= prologueRange.Value.Start && i < prologueRange.Value.End)
            {
                HandlePrologueInstruction(block, instr);
                previousMnemonic = instr.Mnemonic;
                continue;
            }

            // === Специальная обработка эпилога ===
            // Инструкции эпилога (mov sp, bp, pop bp, leave) не создают операций IR.
            if (epilogueRange.HasValue && i >= epilogueRange.Value.Start && i < epilogueRange.Value.End)
            {
                HandleEpilogueInstruction(block, instr);
                previousMnemonic = instr.Mnemonic;
                continue;
            }

            // === Управление потоком ===
            // При встрече безусловного перехода — завершаем блок (переход моделируется в CFG).
            // При RET/RET_IMM: вызываем обработчик (RetHandler создаст ReturnOperation с AX),
            // затем завершаем блок. Это добавляет явный возврат в IR.
            // Для условных переходов сразу заполняем Condition (в их хендлерах).
            if (instr.IsReturn)
            {
                if (ShouldEmitReturnFromRet(block.BasicBlock))
                {
                    Handlers.Get(instr.Mnemonic)?.Handle(block, instr);
                }

                Debug.Assert(block.BasicBlock.ConditionalBlock == null);
                return block;
            }

            if (instr.IsUnconditionalJump)
            {
                Debug.Assert(block.BasicBlock.ConditionalBlock == null);
                return block;
            }

            var handler = Handlers.Get(instr.Mnemonic);
            handler.Handle(block, instr);
            previousMnemonic = instr.Mnemonic;

            if (instr.IsExit)
            {
                Debug.Assert(block.BasicBlock.ConditionalBlock == null);
                return block;
            }
        }

        ApplyBlockPatterns(block);

        // Если дошли сюда — блок не закончился явным прыжком/возвратом.
        if (block.BasicBlock.ConditionalBlock != null && block.Condition == null)
        {
            throw new InvalidOperationException(
                $"Block at {block.BasicBlock.StartOffset:X6} has ConditionalBlock but no Condition was set");
        }

        return block;
    }

    /// <summary>
    /// Применяет профиле-зависимые паттерны к блоку после обработки всех инструкций.
    /// Вызывается в конце <see cref="GenerateCode"/> для каждого блока.
    /// </summary>
    /// <param name="block">Блок, к которому применяются паттерны</param>
    protected virtual void ApplyBlockPatterns(ExprBlock block)
    {
        // Базовая реализация — пустая (для оптимизированного кода)
    }

    /// <summary>
    /// Создавать ли <see cref="ReturnOperation"/> для инструкции RET в данном блоке.
    /// Переопределяется в /Od-профиле для подавления дубля с tail-return.
    /// </summary>
    protected virtual bool ShouldEmitReturnFromRet(BasicBlock block) => true;

    /// <summary>
    /// Вставлять ли <see cref="ReturnOperation"/> в блоки с tail jmp в общий эпилог (/Od).
    /// </summary>
    protected virtual bool ShouldInsertTailReturnsBeforeEpilogue() => false;
}

