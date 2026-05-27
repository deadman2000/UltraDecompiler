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
        Variables.Clear();

        var initialRegisters = isCom
            ? RegisterExpressions.InitCom(Variables)
            : RegisterExpressions.InitExe(Variables);

        RunBuild(graph, initialRegisters);
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
    public void Build(ControlFlowGraph graph, RegisterExpressions initialRegisters)
    {
        // Важно: Variables НЕ очищаем — пользователь мог создать в них переменные для initialRegisters.
        RunBuild(graph, initialRegisters);
    }

    /// <summary>
    /// Общая логика построения (BFS + linking). Clears должны быть сделаны вызывающим кодом.
    /// </summary>
    private void RunBuild(ControlFlowGraph graph, RegisterExpressions initialRegisters)
    {
        Blocks.Clear();
        _blocksMap.Clear();
        _queue.Clear();

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
            // === Управление потоком ===
            // При встрече прыжка/возврата/выхода сразу завершаем обработку блока.
            // Для условных переходов сразу заполняем Condition.
            if (instr.IsUnconditionalJump || instr.IsReturn)
            {
                return exprBlock;
            }

            if (instr.IsConditionalJump)
            {
                if (exprBlock.BasicBlock.ConditionalBlock == null)
                    throw new InvalidOperationException($"Conditional jump {instr.Mnemonic} in block without ConditionalBlock at {exprBlock.BasicBlock.StartOffset:X6}");

                exprBlock.Condition = BuildJumpCondition(instr, exprBlock.EndRegisters);
                return exprBlock;
            }

            if (instr.IsCall)
            {
                throw new NotImplementedException($"Call instruction {instr.Mnemonic} at {exprBlock.BasicBlock.StartOffset:X6} is not supported");
            }

            if (instr.IsExit)
            {
                return exprBlock;
            }

            // === Обычные вычисляющие инструкции ===
            switch (instr.Mnemonic)
            {
                // Данные
                case Mnemonic.MOV:
                    HandleMov(exprBlock, instr);
                    break;
                case Mnemonic.LEA:
                    HandleLea(exprBlock, instr);
                    break;

                // Арифметика
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

                // Логика
                case Mnemonic.AND:
                case Mnemonic.OR:
                case Mnemonic.XOR:
                    HandleLogical(exprBlock, instr);
                    break;

                // Унарные
                case Mnemonic.NOT:
                    HandleUnary(exprBlock, instr, Math1Operation.Not);
                    break;
                case Mnemonic.NEG:
                    HandleUnary(exprBlock, instr, Math1Operation.Neg);
                    break;

                // Сдвиги (SAR трактуем как SHR — упрощение)
                case Mnemonic.SAL:
                    HandleShift(exprBlock, instr, Math2Operation.Shl);
                    break;
                case Mnemonic.SHR:
                case Mnemonic.SAR:
                    HandleShift(exprBlock, instr, Math2Operation.Shr);
                    break;

                // Сравнения (обновляют флаги для последующих Jcc)
                case Mnemonic.CMP:
                    HandleCmp(exprBlock, instr);
                    break;
                case Mnemonic.TEST:
                    HandleTest(exprBlock, instr);
                    break;

                case Mnemonic.NOP:
                    break;

                // TODO: MUL/IMUL/DIV/IDIV, строковые операции и др.
                default:
                    throw new NotImplementedException($"Instruction {instr} is not yet supported");
            }
        }

        // Если дошли сюда — блок не закончился явным прыжком/возвратом.
        if (exprBlock.BasicBlock.ConditionalBlock != null && exprBlock.Condition == null)
        {
            throw new InvalidOperationException(
                $"Block at {exprBlock.BasicBlock.StartOffset:X6} has ConditionalBlock but no Condition was set");
        }

        return exprBlock;
    }

    /// <summary>
    /// Строит символическое условие для взятия ConditionalBlock по Jcc-инструкции
    /// и текущему состоянию флагов (из EndRegisters).
    /// </summary>
    private static Expr BuildJumpCondition(Instruction jumpInstr, RegisterExpressions registers)
    {
        var zf = GetFlagOrTrue(registers.ZF);
        var cf = GetFlagOrTrue(registers.CF);
        var sf = GetFlagOrTrue(registers.SF);
        var of = GetFlagOrTrue(registers.OF);

        // SF == OF  (используем эквивалент XOR: (a&b) | (!a & !b) )
        var sfEqOf = Or(And(sf, of), And(Negate(sf), Negate(of)));

        return jumpInstr.Mnemonic switch
        {
            // Равенство (лучше всего поддерживается после CMP/TEST)
            Mnemonic.JE => zf,
            Mnemonic.JNE => Negate(zf),

            // Беззнаковые сравнения
            Mnemonic.JB => cf,
            Mnemonic.JAE => Negate(cf),

            Mnemonic.JBE => Or(cf, zf),
            Mnemonic.JA => And(Negate(cf), Negate(zf)),

            // Знаковый бит
            Mnemonic.JS => sf,
            Mnemonic.JNS => Negate(sf),

            // Знаковые сравнения
            Mnemonic.JL => Negate(sfEqOf),
            Mnemonic.JGE => sfEqOf,
            Mnemonic.JLE => Or(zf, Negate(sfEqOf)),
            Mnemonic.JG => And(Negate(zf), sfEqOf),

            // Переполнение
            Mnemonic.JO => of,
            Mnemonic.JNO => Negate(of),

            // Чётность (не поддерживаем PF)
            Mnemonic.JP => throw new NotImplementedException("JP/JPE is not supported (PF flag not tracked)"),
            Mnemonic.JNP => throw new NotImplementedException("JNP/JPO is not supported (PF flag not tracked)"),

            // Специальные (CX-based)
            Mnemonic.JCXZ => throw new NotImplementedException("JCXZ is not supported"),

            // Циклы
            Mnemonic.LOOP => throw new NotImplementedException("LOOP is not supported"),
            Mnemonic.LOOPE => throw new NotImplementedException("LOOPE/LOOPZ is not supported"),
            Mnemonic.LOOPNE => throw new NotImplementedException("LOOPNE/LOOPNZ is not supported"),

            _ => throw new NotImplementedException($"Instruction {jumpInstr.Mnemonic} is not yet supported")
        };
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
        else
        {
            throw new NotImplementedException($"LEA with destination {instr.Operand1.Type} is not supported");
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
        else
        {
            throw new NotImplementedException($"MOV with destination {instr.Operand1.Type} is not supported");
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
        else
        {
            throw new NotImplementedException($"Arithmetic {instr.Mnemonic} with destination {dst.Type} is not supported");
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
        else
        {
            throw new NotImplementedException($"{(isInc ? "INC" : "DEC")} with destination {dst.Type} is not supported");
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
            _ => throw new InvalidOperationException($"Unexpected logical mnemonic: {instr.Mnemonic}")
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
        else
        {
            throw new NotImplementedException($"Logical {instr.Mnemonic} with destination {dst.Type} is not supported");
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
        else
        {
            throw new NotImplementedException($"{operation} with destination {dst.Type} is not supported");
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
        else
        {
            throw new NotImplementedException($"Shift {shiftOp} with destination {dst.Type} is not supported");
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