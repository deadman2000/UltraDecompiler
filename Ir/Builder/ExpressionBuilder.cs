using System.Diagnostics;
using UltraDecompiler.Compilation;
using UltraDecompiler.Ir.InstructionHandlers;
using UltraDecompiler.Ir.Variables;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Выполняет преобразование потока инструкций x86 (через CFG) в высокоуровневые
/// выражения и операции (SetOperation, Math1Expr, Math2Expr и т.д.).
/// 
/// Этот класс отвечает ТОЛЬКО за генерацию IR-дерева (ExprBlock) из CFG:
/// - Символическое выполнение инструкций в каждом базовом блоке
/// - Обновление RegisterExpressions (символические значения регистров/флагов)
/// - Создание Operations (SetOperation, StoreOperation и т.д.) в ExprBlock
/// - Построение связей Next/ConditionalBlock между ExprBlock'ами
/// </summary>
public partial class ExpressionBuilder
{
    private readonly Dictionary<BasicBlock, ExprBlock> _blocksMap = [];
    private readonly Queue<ExprBlock> _queue = new();
    private ExprBlock? _entryBlock;

    /// <summary>
    /// Точка входа IR-дерева (первый построенный ExprBlock).
    /// </summary>
    internal ExprBlock? EntryBlock => _entryBlock;

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

        RunBuild(graph, []);
    }

    /// <summary>
    /// Выполняет декомпиляцию процедуры (с начальным retAddr на стеке).
    /// </summary>
    public void BuildProc(ControlFlowGraph graph)
    {
        Variables.Clear();
        List<Expr> stack = [];
        stack.Add(Variables.CreateInternalVariable("retAddr"));
        RunBuild(graph, stack);
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
    public void Build(ControlFlowGraph graph, Stack<Expr> initialStack)
    {
        // Важно: Variables НЕ очищаем — пользователь мог создать в них переменные для initialRegisters.
        RunBuild(graph, initialStack);
    }

    /// <summary>
    /// Общая логика построения (BFS + linking).
    /// </summary>
    private void RunBuild(
        ControlFlowGraph graph,
        IEnumerable<Expr> initialStack)
    {
        Blocks.Clear();
        _blocksMap.Clear();
        _queue.Clear();
        _entryBlock = null;

        AnalyzeFunctionParameters(graph);

        // Формируем первый блок и добавляем его в очередь на обработку
        CreateExprBlock(graph.EntryBlock, initialStack);

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
}

