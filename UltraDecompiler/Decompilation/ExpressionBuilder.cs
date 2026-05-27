using UltraDecompiler.Disassembler;
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
        Blocks.Clear();
        Variables.Clear();
        _blocksMap.Clear();
        _queue.Clear();

        // Выбираем начальное символическое состояние регистров.
        // Для .COM и .EXE оно разное (разные значения SP, сегментных регистров и т.д.).
        var initialRegisters = isCom
            ? RegisterExpressions.InitCom(Variables)
            : RegisterExpressions.InitExe(Variables);

        // Формируем первый блок и добавляем его в очередь на обработку
        CreateExprBlock(graph.EntryBlock, initialRegisters);

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
            // Важно: мы сохраняем состояние только при ПЕРВОМ посещении блока.
            // Если к блоку можно прийти несколькими путями (слияние), то
            // "победит" первый обработанный предшественник.
            // Это упрощение. Полноценное решение требует merge/phi-функций.
            if (block.BasicBlock.NextBlock != null)
            {
                CreateExprBlock(block.BasicBlock.NextBlock, block.EndRegisters);
            }

            if (block.BasicBlock.ConditionalBlock != null)
            {
                CreateExprBlock(block.BasicBlock.ConditionalBlock, block.EndRegisters);
            }
        }

        // Второй проход: связываем ExprBlock'и между собой.
        // Это нужно, чтобы потом можно было генерировать структурированный C-код
        // (if/else, циклы и т.д.) на основе связей между блоками.
        foreach (var kvp in _blocksMap)
        {
            var exprBlock = kvp.Value;
            var basicBlock = kvp.Key;

            if (basicBlock.NextBlock != null && _blocksMap.TryGetValue(basicBlock.NextBlock, out var nextCode))
                exprBlock.Next = nextCode;

            if (basicBlock.ConditionalBlock != null && _blocksMap.TryGetValue(basicBlock.ConditionalBlock, out var condCode))
            {
                exprBlock.ConditionalBlock = condCode;

                // Пытаемся построить настоящее условие по последней Jcc + текущим флагам.
                var lastInstr = basicBlock.Instructions.Count > 0
                    ? basicBlock.Instructions[^1]
                    : null;

                if (lastInstr?.IsConditionalJump == true)
                {
                    exprBlock.Condition = BuildJumpCondition(lastInstr, exprBlock.EndRegisters);
                }
                else
                {
                    // fallback (должен быть очень редким)
                    exprBlock.Condition = ConstExpr.One;
                }
            }
        }
    }

    private void CreateExprBlock(BasicBlock block, in RegisterExpressions registers)
    {
        // TODO Подумать, что делать, если в блок мы попадаем из разных мест
        if (_blocksMap.ContainsKey(block))
            return;

        var exprBlock = new ExprBlock(block)
        {
            InitRegisters = registers
        };
        Blocks.Add(exprBlock);
        _blocksMap[block] = exprBlock;
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
    private ExprBlock GenerateCode(ExprBlock exprBlock)
    {
        // Начинаем обработку блока с копии InitRegisters.
        exprBlock.EndRegisters = exprBlock.InitRegisters;

        foreach (var instr in exprBlock.BasicBlock.Instructions)
        {
            switch (instr.Mnemonic)
            {
                case Mnemonic.MOV:
                    HandleMov(exprBlock, instr);
                    break;

                case Mnemonic.LEA:
                    HandleLea(exprBlock, instr);
                    break;

                case Mnemonic.ADD:
                case Mnemonic.SUB:
                    HandleArithmetic(exprBlock, instr);
                    break;

                case Mnemonic.INC:
                    HandleIncDec(exprBlock, instr, true);
                    break;

                case Mnemonic.DEC:
                    HandleIncDec(exprBlock, instr, false);
                    break;

                case Mnemonic.AND:
                case Mnemonic.OR:
                case Mnemonic.XOR:
                    HandleLogical(exprBlock, instr);
                    break;

                case Mnemonic.NOT:
                    HandleUnary(exprBlock, instr, Math1Operation.Not);
                    break;

                case Mnemonic.NEG:
                    HandleUnary(exprBlock, instr, Math1Operation.Neg);
                    break;

                case Mnemonic.SAL:
                    HandleShift(exprBlock, instr, Math2Operation.Shl);
                    break;

                case Mnemonic.SHR:
                    HandleShift(exprBlock, instr, Math2Operation.Shr);
                    break;

                case Mnemonic.SAR:
                    // SAR — арифметический сдвиг вправо (с сохранением знака).
                    // Сейчас мы упрощённо трактуем его как логический SHR.
                    // Для корректной поддержки знаковых типов в будущем потребуется
                    // отдельная операция или флаг в Math2Expr.
                    HandleShift(exprBlock, instr, Math2Operation.Shr);
                    break;

                case Mnemonic.CMP:
                    HandleCmp(exprBlock, instr);
                    break;

                case Mnemonic.TEST:
                    HandleTest(exprBlock, instr);
                    break;

                // TODO: MUL, IMUL, DIV, IDIV — меняют несколько регистров (AX/DX),
                // имеют сложную семантику флагов и переполнений.
                default:
                    // Инструкция не поддерживается — просто пропускаем.
                    // В будущем здесь можно добавлять комментарии или специальные маркеры.
                    break;
            }
        }

        // После обработки всех инструкций EndRegisters уже содержит актуальное
        // состояние (все Handle* методы пишут напрямую в block.EndRegisters).
        return exprBlock;
    }

    private void HandleLea(ExprBlock block, Instruction instr)
    {
        if (instr.Operand1.Type == OperandType.Register16)
        {
            // LEA загружает эффективный адрес (не разыменовывает память).
            Expr eaExpr = instr.Operand2.Type == OperandType.Memory
                ? GetEffectiveAddress(instr.Operand2, block.EndRegisters, instr.Segment)
                : GetExpression(instr.Operand2, block.EndRegisters, instr.Segment);

            block.EndRegisters = block.EndRegisters.Set16(instr.Operand1.Value, eaExpr);
        }
    }

    private void HandleMov(ExprBlock block, Instruction instr)
    {
        var exprSrc = GetExpression(instr.Operand2, block.EndRegisters, instr.Segment);

        // Обновляем символическое значение регистра-назначения.
        // Сама операция MOV обычно не порождает отдельный SetOperation —
        // она просто "передаёт" выражение дальше.
        if (instr.Operand1.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(instr.Operand1.Value, exprSrc);
        }
        else if (instr.Operand1.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(instr.Operand1.Value, exprSrc);
        }
        else if (instr.Operand1.Type == OperandType.SegmentRegister)
        {
            block.EndRegisters = block.EndRegisters.SetSegment(instr.Operand1.Value, exprSrc);
        }
        else if (instr.Operand1.Type == OperandType.Memory)
        {
            var (addr, seg) = BuildMemoryReference(instr.Operand1, block.EndRegisters, instr.Segment);
            block.Operations.Add(new StoreOperation(addr, seg, exprSrc));
        }
    }

    /// <summary>
    /// Обрабатывает ADD и SUB.
    /// Создаёт выражение "dstCurrent + src" или "dstCurrent - src",
    /// сохраняет его в новую Variable и записывает эту переменную в регистр-назначение.
    /// 
    /// Мы всегда выделяем новую Variable для результата — это позволяет
    /// в дальнейшем строить более чистые выражения и избегать мутации.
    /// </summary>
    private void HandleArithmetic(ExprBlock block, Instruction instr)
    {
        var dst = instr.Operand1;
        var srcExpr = GetExpression(instr.Operand2, block.EndRegisters, instr.Segment);
        var dstCurrent = GetExpression(dst, block.EndRegisters, instr.Segment);

        var op = instr.Mnemonic == Mnemonic.ADD ? Math2Operation.Add : Math2Operation.Sub;
        Expr result = Calculate(op, dstCurrent, srcExpr);

        if (result is not ConstExpr)
        {
            var resultVar = Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        // Обновляем символическое состояние
        if (dst.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(dst.Value, result);
        }
        else if (dst.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(dst.Value, result);
        }
        else if (dst.Type == OperandType.Memory)
        {
            var (addr, seg) = BuildMemoryReference(dst, block.EndRegisters, instr.Segment);
            block.Operations.Add(new StoreOperation(addr, seg, result));
        }

        block.EndRegisters = ApplyArithmeticFlags(block.EndRegisters, result);
    }

    /// <summary>
    /// Обрабатывает INC и DEC (специальный случай арифметики на 1).
    /// Выделена в отдельный метод для читаемости и будущего расширения
    /// (INC/DEC по-разному влияют на флаги по сравнению с ADD/SUB 1).
    /// </summary>
    private void HandleIncDec(ExprBlock block, Instruction instr, bool isInc)
    {
        var dst = instr.Operand1;
        var current = GetExpression(dst, block.EndRegisters, instr.Segment);

        var op = isInc ? Math2Operation.Add : Math2Operation.Sub;
        Expr result = Calculate(op, current, ConstExpr.One);

        if (result is not ConstExpr)
        {
            var resultVar = Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        if (dst.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(dst.Value, result);
        }
        else if (dst.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(dst.Value, result);
        }
        else if (dst.Type == OperandType.Memory)
        {
            var (addr, seg) = BuildMemoryReference(dst, block.EndRegisters, instr.Segment);
            block.Operations.Add(new StoreOperation(addr, seg, result));
        }

        block.EndRegisters = ApplyArithmeticFlags(block.EndRegisters, result);
    }

    /// <summary>
    /// Обрабатывает побитовые логические операции: AND, OR, XOR.
    /// Логика полностью аналогична HandleArithmetic, но использует
    /// соответствующие Math2Operation (And, Or, Xor).
    /// </summary>
    private void HandleLogical(ExprBlock block, Instruction instr)
    {
        var dst = instr.Operand1;
        var srcExpr = GetExpression(instr.Operand2, block.EndRegisters, instr.Segment);
        var dstCurrent = GetExpression(dst, block.EndRegisters, instr.Segment);

        var op = instr.Mnemonic switch
        {
            Mnemonic.AND => Math2Operation.And,
            Mnemonic.OR => Math2Operation.Or,
            Mnemonic.XOR => Math2Operation.Xor,
            _ => throw new InvalidOperationException()
        };

        Expr result = Calculate(op, dstCurrent, srcExpr);

        if (result is not ConstExpr)
        {
            var resultVar = Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        if (dst.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(dst.Value, result);
        }
        else if (dst.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(dst.Value, result);
        }
        else if (dst.Type == OperandType.Memory)
        {
            var (addr, seg) = BuildMemoryReference(dst, block.EndRegisters, instr.Segment);
            block.Operations.Add(new StoreOperation(addr, seg, result));
        }

        block.EndRegisters = ApplyArithmeticFlags(block.EndRegisters, result);
    }

    /// <summary>
    /// Обрабатывает унарные операции: NOT и NEG.
    /// NOT — побитовое отрицание (~).
    /// NEG — арифметическое отрицание (-x, с учётом переполнения для MIN_VALUE).
    /// 
    /// Результат всегда оборачивается в новую Variable через SetOperation.
    /// </summary>
    private void HandleUnary(ExprBlock block, Instruction instr, Math1Operation operation)
    {
        var dst = instr.Operand1;
        var current = GetExpression(dst, block.EndRegisters, instr.Segment);
        Expr result = Calculate(operation, current);

        if (result is not ConstExpr)
        {
            var resultVar = Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        if (dst.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(dst.Value, result);
        }
        else if (dst.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(dst.Value, result);
        }
        else if (dst.Type == OperandType.Memory)
        {
            var (addr, seg) = BuildMemoryReference(dst, block.EndRegisters, instr.Segment);
            block.Operations.Add(new StoreOperation(addr, seg, result));
        }

        block.EndRegisters = ApplyArithmeticFlags(block.EndRegisters, result);
    }

    /// <summary>
    /// Обрабатывает сдвиги: SAL/SHL, SHR, SAR.
    /// 
    /// Второй операнд (количество бит) может быть:
    /// - непосредственным значением (1, 3, 5 и т.д.)
    /// - регистром CL (динамический сдвиг)
    /// 
    /// Сейчас SAR трактуется как SHR. Это упрощение.
    /// </summary>
    private void HandleShift(ExprBlock block, Instruction instr, Math2Operation shiftOp)
    {
        var dst = instr.Operand1;

        // Второй операнд сдвига — обычно константа или CL.
        // GetExpression корректно обработает и то, и другое.
        var srcExpr = GetExpression(instr.Operand2, block.EndRegisters, instr.Segment);
        var dstCurrent = GetExpression(dst, block.EndRegisters, instr.Segment);

        Expr result = Calculate(shiftOp, dstCurrent, srcExpr);

        if (result is not ConstExpr)
        {
            var resultVar = Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        if (dst.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(dst.Value, result);
        }
        else if (dst.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(dst.Value, result);
        }
        else if (dst.Type == OperandType.Memory)
        {
            var (addr, seg) = BuildMemoryReference(dst, block.EndRegisters, instr.Segment);
            block.Operations.Add(new StoreOperation(addr, seg, result));
        }

        block.EndRegisters = ApplyArithmeticFlags(block.EndRegisters, result);
    }

    /// <summary>
    /// Обрабатывает CMP (сравнение). Не создаёт SetOperation (результат не записывается),
    /// но обновляет символические значения флагов в RegisterExpressions.
    /// ZF для CMP представляется как CmpExpr(Eq, left, right) — это позволяет
    /// в будущем легко строить условия вида "if (ax == bx)".
    /// </summary>
    private void HandleCmp(ExprBlock block, Instruction instr)
    {
        var left = GetExpression(instr.Operand1, block.EndRegisters, instr.Segment);
        var right = GetExpression(instr.Operand2, block.EndRegisters, instr.Segment);

        // Прямое равенство — лучший способ представить ZF после CMP
        var zfExpr = new CmpExpr(CmpOperation.Eq, left, right);

        // Другие флаги (CF, SF, OF) оставляем как есть или null (полная модель сложнее).
        // В будущем можно вычислять через виртуальный Sub и bit-анализ.
        block.EndRegisters = block.EndRegisters with { ZF = zfExpr };
    }

    /// <summary>
    /// Обрабатывает TEST (побитовое И без записи результата).
    /// Обновляет флаги: ZF = ((left & right) == 0), CF=0, OF=0.
    /// Не порождает Operation.
    /// </summary>
    private void HandleTest(ExprBlock block, Instruction instr)
    {
        var left = GetExpression(instr.Operand1, block.EndRegisters, instr.Segment);
        var right = GetExpression(instr.Operand2, block.EndRegisters, instr.Segment);

        var andExpr = Calculate(Math2Operation.And, left, right);
        var zfExpr = new CmpExpr(CmpOperation.Eq, andExpr, ConstExpr.Zero);

        block.EndRegisters = block.EndRegisters with
        {
            ZF = zfExpr,
            CF = ConstExpr.Zero,
            OF = ConstExpr.Zero
            // SF/PF от битов результата AND — при необходимости доработать
        };
    }

    /// <summary>
    /// Применяет обновление флагов после арифметической/логической операции,
    /// которая записала результат в resultExpr (обычно Variable).
    /// На данный момент устанавливаем только ZF = (resultExpr == 0).
    /// </summary>
    private static RegisterExpressions ApplyArithmeticFlags(RegisterExpressions regs, Expr resultExpr)
    {
        return regs with
        {
            ZF = new CmpExpr(CmpOperation.Eq, resultExpr, ConstExpr.Zero)
            // CF, SF, OF при необходимости (требуют знания операции и bit-логики)
        };
    }
}