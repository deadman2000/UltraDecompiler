using System.Diagnostics;
using UltraDecompiler.Decompilation.InstructionHandlers;
using UltraDecompiler.Graph;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Основной класс декомпилятора.
/// 
/// Выполняет преобразование потока инструкций x86 (через CFG) в высокоуровневые
/// выражения и операции (SetOperation, Math1Expr, Math2Expr и т.д.).
/// </summary>
public partial class ExpressionBuilder
{
    private readonly Dictionary<BasicBlock, ExprBlock> _blocksMap = [];
    private readonly Queue<ExprBlock> _queue = new();
    private ExprBlock? _entryBlock;

    public List<ExprBlock> Blocks { get; } = [];

    public VariableStorage Variables { get; } = new();

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
    /// Общая логика построения (BFS + linking). Clears должны быть сделаны вызывающим кодом.
    /// </summary>
    private void RunBuild(ControlFlowGraph graph, RegisterExpressions initialRegisters, IEnumerable<Expr> initialStack)
    {
        Blocks.Clear();
        _blocksMap.Clear();
        _queue.Clear();
        _entryBlock = null;

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
            InitStack = stack.ToArray()
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
    private static ExprBlock GenerateCode(ExprBlock block)
    {
        // Начинаем обработку блока с копии InitRegisters.
        block.EndRegisters = block.InitRegisters;

        // Копируем символический стек. Reverse() нужен, потому что конструктор Stack<T>(IEnumerable)
        // кладёт первый элемент перечисления на дно, а последний — на вершину.
        // InitStack[0] — самый глубокий, InitStack[^1] — вершина (результат последнего PUSH).
        block.EndStack = new Stack<Expr>(block.InitStack.Reverse());

        foreach (var instr in block.BasicBlock.Instructions)
        {
            // === Управление потоком ===
            // При встрече прыжка/возврата/выхода сразу завершаем обработку блока.
            // Для условных переходов сразу заполняем Condition.
            if (instr.IsUnconditionalJump || instr.IsReturn)
            {
                Debug.Assert(block.BasicBlock.ConditionalBlock == null);
                return block;
            }

            var handler = Handlers.Get(instr.Mnemonic) ?? throw new NotImplementedException($"Instruction {instr} is not yet supported");
            handler.Handle(block, instr);

            if (instr.IsExit)
            {
                Debug.Assert(block.BasicBlock.ConditionalBlock == null);
                return block;
            }
        }

        // Если дошли сюда — блок не закончился явным прыжком/возвратом.
        if (block.BasicBlock.ConditionalBlock != null && block.Condition == null)
        {
            throw new InvalidOperationException(
                $"Block at {block.BasicBlock.StartOffset:X6} has ConditionalBlock but no Condition was set");
        }

        return block;
    }
}