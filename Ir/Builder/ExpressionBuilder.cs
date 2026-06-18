using System.Diagnostics;
using UltraDecompiler.Compilation;
using UltraDecompiler.Ir.InstructionHandlers;
using UltraDecompiler.Ir.Switch;
using UltraDecompiler.Ir.Variables;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Выполняет преобразование потока инструкций x86 (через CFG) в высокоуровневые
/// выражения и операции (SetOperation, Math1Expr, Math2Expr и т.д.).
/// </summary>
public partial class ExpressionBuilder
{
    private readonly Dictionary<BasicBlock, ExprBlock> _blocksMap = [];
    private readonly Queue<ExprBlock> _queue = new();
    private ExprBlock? _entryBlock;

    // Поля для распознавания switch-паттернов (используются в ExpressionBuilderQuickCUnopt)
    protected readonly Dictionary<int, ExprBlock> _blocksByOffset = [];
    protected readonly Dictionary<int, QuickCSwitchPattern> _switchByEntry = [];

    public List<ExprBlock> Blocks { get; } = [];

    public VariableStorage Variables { get; } = new();

    public static ExpressionBuilder Create(OptimizationLevel optimization)
    {
        return optimization switch
        {
            OptimizationLevel.Disabled => new ExpressionBuilderQuickCUnopt(),
            OptimizationLevel.Enabled or OptimizationLevel.EnableLoop or OptimizationLevel.EnabledFull => new ExpressionBuilderQuickCOpt(),
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
    /// Важно: состояние регистров (RegisterExpressions) передаётся между блоками.
    /// Это позволяет отслеживать значения регистров через границы базовых блоков.
    /// 
    /// Если передан <paramref name="procedures"/>, после построения блоков автоматически
    /// выполняется CallSiteResolver для подстановки имён (в т.ч. библиотечных) и аргументов в CallExpr.
    /// </summary>
    /// <param name="graph">Построенный граф потока управления</param>
    /// <param name="isCom">true для .COM файлов (другая инициализация регистров)</param>
    public void Build(ControlFlowGraph graph, bool isCom = false)
    {
        Variables.Clear();

        var initialRegisters = isCom
            ? RegisterExpressions.InitCom(Variables)
            : RegisterExpressions.InitExe(Variables);

        RunBuild(graph, initialRegisters, []);
    }

    /// <summary>
    /// Выполняет декомпиляцию процедуры (с начальным retAddr на стеке).
    /// </summary>
    public void BuildProc(ControlFlowGraph graph)
    {
        Variables.Clear();
        var initialRegisters = RegisterExpressions.InitProc(Variables);
        List<Expr> stack = [];
        stack.Add(Variables.CreateInternalVariable("retAddr"));
        RunBuild(graph, initialRegisters, stack);
    }

    /// <summary>
    /// Выполняет декомпиляцию с явно заданным начальным состоянием регистров.
    /// 
    /// Полезно в тестах для проверки работы с символическими переменными
    /// (когда регистры уже содержат Variable или сложные выражения).
    /// 
    /// В отличие от версии с isCom, эта перегрузка НЕ очищает <see cref="Variables"/>
    /// (чтобы созданные пользователем переменные в initialRegisters остались валидными).
    /// </summary>
    public void Build(ControlFlowGraph graph, RegisterExpressions initialRegisters, Stack<Expr> initialStack)
    {
        // Важно: Variables НЕ очищаем — пользователь мог создать в них переменные для initialRegisters.
        RunBuild(graph, initialRegisters, initialStack);
    }

    /// <summary>
    /// Общая логика построения (BFS + linking).
    /// </summary>
    private void RunBuild(
        ControlFlowGraph graph,
        RegisterExpressions initialRegisters,
        IEnumerable<Expr> initialStack)
    {
        Blocks.Clear();
        _blocksMap.Clear();
        _queue.Clear();
        _entryBlock = null;

        AnalyzeFunctionParameters(graph);

        // Формируем первый блок и добавляем его в очередь на обработку
        CreateExprBlock(graph.EntryBlock, initialRegisters, initialStack);

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
                CreateExprBlock(block.BasicBlock.NextBlock, block.EndRegisters, block.EndStack);
            }

            if (block.BasicBlock.ConditionalBlock != null)
            {
                CreateExprBlock(block.BasicBlock.ConditionalBlock, block.EndRegisters, block.EndStack);
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

        AnalyzeSwitchPatterns(graph.Blocks);

        BuildGotoLabelMap();

        // Распознавание циклов на уровне связанных блоков
        AnalyzeLoopPatterns();
    }

    private void CreateExprBlock(BasicBlock block, in RegisterExpressions registers, IEnumerable<Expr> stack)
    {
        // TODO Подумать, что делать, если в блок мы попадаем из разных мест
        if (_blocksMap.ContainsKey(block))
            return;

        var exprBlock = new ExprBlock(block)
        {
            Variables = Variables,
            InitRegisters = registers,
            InitStack = stack.ToArray(),
        };
        Blocks.Add(exprBlock);
        _blocksMap[block] = exprBlock;
        _entryBlock ??= exprBlock;
        _queue.Enqueue(exprBlock);
    }

    /// <summary>
    /// Выполняет symbolic execution инструкций одного базового блока.
    /// 
    /// Для каждой инструкции:
    /// - Если это "запись" в регистр (MOV, ADD, AND и т.д.) — мы вычисляем
    ///   новое символическое выражение и сохраняем его в RegisterExpressions.
    /// - Если инструкция имеет побочный эффект, который мы умеем представлять
    ///   (арифметика, логика, сдвиги) — создаём соответствующую Operation
    ///   (обычно SetOperation с Math1Expr или Math2Expr).
    /// 
    /// Важно: мы НЕ создаём Operation для каждого MOV. MOV просто обновляет
    /// текущее символическое значение регистра. Операции создаются только
    /// тогда, когда происходит "полезное" вычисление (ADD, AND, NEG и т.д.).
    /// 
    /// В конце работы метод сохраняет финальное состояние регистров в
    /// exprBlock.EndRegisters.
    /// </summary>
    private ExprBlock GenerateCode(ExprBlock block)
    {
        Debug.Assert(block.BasicBlock.EndOffset != -1);

        // Начинаем обработку блока с копии InitRegisters.
        block.EndRegisters = block.InitRegisters;

        // Копируем символический стек. Reverse() нужен, потому что конструктор Stack<T>(IEnumerable)
        // кладёт первый элемент перечисления на дно, а последний — на вершину.
        // InitStack[0] — самый глубокий, InitStack[^1] — вершина (результат последнего PUSH).
        block.EndStack = new Stack<Expr>(block.InitStack.Reverse());

        Mnemonic? previousMnemonic = null;

        foreach (var instr in block.BasicBlock.Instructions)
        {
            block.PreviousMnemonic = previousMnemonic;

            // === Управление потоком ===
            // При встрече безусловного перехода — завершаем блок (переход моделируется в CFG).
            // При RET/RET_IMM: вызываем обработчик (RetHandler создаст ReturnOperation с AX),
            // затем завершаем блок. Это добавляет явный возврат в IR.
            // Для условных переходов сразу заполняем Condition (в их хендлерах).
            if (instr.IsReturn)
            {
                Handlers.Get(instr.Mnemonic)?.Handle(block, instr);
                Debug.Assert(block.BasicBlock.ConditionalBlock == null);
                return block;
            }

            if (instr.IsUnconditionalJump)
            {
                Debug.Assert(block.BasicBlock.ConditionalBlock == null);
                return block;
            }

            var handler = Handlers.Get(instr.Mnemonic) ?? throw new NotImplementedException($"Instruction {instr} is not yet supported");
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
    /// Анализирует и распознаёт switch-паттерны в графе потока управления.
    /// Вызывается после завершения обхода всех блоков.
    /// </summary>
    /// <param name="blocks">Все блоки графа</param>
    protected virtual void AnalyzeSwitchPatterns(IReadOnlyList<BasicBlock> blocks)
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

    /// <summary>
    /// Распознаёт циклы на уровне связанным ExprBlock'ов.
    /// Ищет back edges: переходы от блока к ранее посещённому блоку в DFS-обходе.
    /// Вызывается после завершения обхода и связывания блоков, но до GetAllOperations.
    /// </summary>
    private void AnalyzeLoopPatterns()
    {
        var visited = new HashSet<ExprBlock>();
        var pathStack = new Stack<ExprBlock>();

        AnalyzeLoopsRecursive(_entryBlock, visited, pathStack);
    }

    /// <summary>
    /// Рекурсивный обход для поиска back edges и преобразования их в WhileOperation.
    /// </summary>
    private void AnalyzeLoopsRecursive(
        ExprBlock? block,
        HashSet<ExprBlock> visited,
        Stack<ExprBlock> pathStack)
    {
        if (block == null)
            return;

        // Проверяем back edge
        var pathList = pathStack.ToList();
        var backEdgeIndex = pathList.IndexOf(block);
        if (backEdgeIndex >= 0)
        {
            // Нашли back edge — это цикл
            // Проверяем, что текущий блок содержит if с условием на разыменовании указателя
            if (TryConvertBackEdgeToWhile(block, pathList[backEdgeIndex], pathList))
            {
                return;
            }
        }

        if (visited.Contains(block))
            return;

        visited.Add(block);
        pathStack.Push(block);

        // Рекурсивно обрабатываем successors
        if (block.Next != null)
            AnalyzeLoopsRecursive(block.Next, visited, pathStack);

        if (block.ConditionalBlock != null)
            AnalyzeLoopsRecursive(block.ConditionalBlock, visited, pathStack);

        pathStack.Pop();
    }

    /// <summary>
    /// Пытается преобразовать back edge к while-цикл.
    /// Паттерн: блок содержит if (cond) { body; } где body переходит к блоку header.
    /// 
    /// На данный момент метод возвращает false, так как основная логика работы с циклами
    /// реализована через IfOperation в GetAllOperations(). Преобразование в WhileOperation
    /// выполняется только для специфических паттернов (REP-инструкции, строковые циклы).
    /// 
    /// Этот метод может быть расширен в будущем для более агрессивного распознавания циклов
    /// на этапе построения IR, а не на этапе сбора операций.
    /// </summary>
    private static bool TryConvertBackEdgeToWhile(ExprBlock currentBlock, ExprBlock headerBlock, IReadOnlyList<ExprBlock> path)
    {
        // Базовая реализация: пока не преобразуем общие циклы в WhileOperation.
        // Циклы обрабатываются через IfOperation в ExpressionBuilder.Flatten.cs.
        // 
        // Преобразование в WhileOperation оставлено для:
        // - REP MOVS/LODS/STOS инструкций
        // - Специфических паттернов QuickC (распознаются в ShouldConvertLoopHeader)
        //
        // Этот метод может быть расширен в будущем для более агрессивного распознавания циклов.
        return false;
    }
}
