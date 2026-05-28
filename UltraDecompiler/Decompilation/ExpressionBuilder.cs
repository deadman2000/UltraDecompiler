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
            InitRegisters = registers,
            InitStack= stack.ToArray()
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

        // Копируем символический стек. Reverse() нужен, потому что конструктор Stack<T>(IEnumerable)
        // кладёт первый элемент перечисления на дно, а последний — на вершину.
        // InitStack[0] — самый глубокий, InitStack[^1] — вершина (результат последнего PUSH).
        exprBlock.EndStack = new Stack<Expr>(exprBlock.InitStack.Reverse());

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
                case Mnemonic.LDS:
                case Mnemonic.LES:
                    HandleLdsLes(exprBlock, instr);
                    break;
                case Mnemonic.XCHG:
                    HandleXchg(exprBlock, instr);
                    break;

                // Арифметика
                case Mnemonic.ADD:
                case Mnemonic.SUB:
                case Mnemonic.ADC:
                case Mnemonic.SBB:
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

                case Mnemonic.CBW:
                    HandleCbw(exprBlock, instr);
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

                // Флаговые инструкции
                case Mnemonic.CLI:
                    // CLI → _disable() (отключение аппаратных прерываний)
                    exprBlock.Operations.Add(new CallOperation(new Procedure { Name = "_disable" }, []));
                    break;

                case Mnemonic.STI:
                    // STI → _enable() (включение аппаратных прерываний)
                    exprBlock.Operations.Add(new CallOperation(new Procedure { Name = "_enable" }, []));
                    break;

                case Mnemonic.CLD:
                    // DF = 0 → инкремент SI/DI при строковых операциях
                    exprBlock.EndRegisters = exprBlock.EndRegisters with { DF = ConstExpr.Zero };
                    break;

                case Mnemonic.STD:
                    // DF = 1 → декремент SI/DI при строковых операциях
                    exprBlock.EndRegisters = exprBlock.EndRegisters with { DF = ConstExpr.One };
                    break;

                case Mnemonic.CLC:
                    exprBlock.EndRegisters = exprBlock.EndRegisters with { CF = ConstExpr.Zero };
                    break;

                case Mnemonic.STC:
                    exprBlock.EndRegisters = exprBlock.EndRegisters with { CF = ConstExpr.One };
                    break;

                case Mnemonic.CMC:
                    exprBlock.EndRegisters = exprBlock.EndRegisters with { CF = BoolNot(exprBlock.EndRegisters.CF) };
                    break;

                case Mnemonic.INT:
                    HandleInterrupt(exprBlock, instr);
                    break;

                case Mnemonic.CALL:
                case Mnemonic.CALL_FAR:
                    HandleCall(exprBlock, instr);
                    break;

                // Стек
                case Mnemonic.PUSH:
                    HandlePush(exprBlock, instr);
                    break;

                case Mnemonic.POP:
                    HandlePop(exprBlock, instr);
                    break;

                case Mnemonic.LEAVE:
                    HandleLeave(exprBlock, instr);
                    break;

                // Одиночные строковые инструкции (без REP)
                case Mnemonic.MOVSB:
                case Mnemonic.MOVSW:
                    HandleStringMove(exprBlock, instr);
                    break;

                case Mnemonic.STOSB:
                case Mnemonic.STOSW:
                    HandleStringStore(exprBlock, instr);
                    break;

                case Mnemonic.LODSB:
                case Mnemonic.LODSW:
                    HandleStringLoad(exprBlock, instr);
                    break;

                case Mnemonic.CMPSB:
                case Mnemonic.CMPSW:
                    HandleStringCompare(exprBlock, instr);
                    break;

                case Mnemonic.SCASB:
                case Mnemonic.SCASW:
                    HandleStringScan(exprBlock, instr);
                    break;

                // TODO: MUL/IMUL/DIV/IDIV, ROL/RCR и др. ротации, PUSHF/POPF/LAHF/SAHF, IN/OUT, DAA/DAS/AAA/AAS, LOOP* и др.
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
        // SF == OF  (эквивалентность, т.е. не XOR)
        // Используется для знаковых условных переходов.
        // Используем Bool* версии, чтобы сразу сворачивать константы.
        Expr SfEqOf() => BoolOr(
            BoolAnd(registers.SF, registers.OF),
            BoolAnd(BoolNot(registers.SF), BoolNot(registers.OF))
        );
        Expr SfNeOf() => BoolNot(SfEqOf());

        return jumpInstr.Mnemonic switch
        {
            // Равенство
            Mnemonic.JE => registers.ZF,
            Mnemonic.JNE => BoolNot(registers.ZF),

            // Беззнаковые сравнения
            Mnemonic.JB => registers.CF,
            Mnemonic.JAE => BoolNot(registers.CF),
            Mnemonic.JBE => BoolOr(registers.CF, registers.ZF),
            Mnemonic.JA => BoolAnd(BoolNot(registers.CF), BoolNot(registers.ZF)),

            // Знаковый бит
            Mnemonic.JS => registers.SF,
            Mnemonic.JNS => BoolNot(registers.SF),

            // Знаковые сравнения
            Mnemonic.JL => SfNeOf(),
            Mnemonic.JGE => SfEqOf(),
            Mnemonic.JLE => BoolOr(registers.ZF, SfNeOf()),
            Mnemonic.JG => BoolAnd(BoolNot(registers.ZF), SfEqOf()),

            // Переполнение
            Mnemonic.JO => registers.OF,
            Mnemonic.JNO => BoolNot(registers.OF),

            // Чётность
            Mnemonic.JP => throw new NotImplementedException("JP/JPE is not supported (PF flag not tracked)"),
            Mnemonic.JNP => throw new NotImplementedException("JNP/JPO is not supported (PF flag not tracked)"),

            // Специальные (CX-based)
            Mnemonic.JCXZ => new CmpExpr(CmpOperation.Eq, registers.Get16(1), ConstExpr.Zero),

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

    /// <summary>
    /// Обрабатывает LDS и LES — загрузку far-указателя (DWORD) из памяти.
    /// Младшее слово (offset) загружается в gp-регистр (Operand1).
    /// Старшее слово (segment) загружается в DS (для LDS) или ES (для LES).
    /// Сегментный префикс инструкции (если есть) применяется к адресу самого указателя в памяти.
    /// </summary>
    private void HandleLdsLes(ExprBlock block, Instruction instr)
    {
        if (instr.Operand2.Type != OperandType.Memory)
        {
            throw new NotImplementedException("LDS/LES с регистром в качестве источника не поддерживается (недопустимая кодировка на 8086)");
        }

        // Адрес в памяти, по которому лежит far-указатель (DWORD: offset + segment)
        var (ptrAddr, ptrSeg) = BuildMemoryReference(instr.Operand2, block.EndRegisters, instr.Segment);

        // Младшее слово — offset, загружается в целевой gp-регистр
        var knownOffset = Variables.TryGetKnownMemoryVariable(ptrAddr, ptrSeg);
        Expr offsetExpr = knownOffset != null ? knownOffset : new MemExpr(ptrAddr, ptrSeg);

        // Старшее слово (+2) — значение сегмента
        Expr highAddr = Calculate(Math2Operation.Add, ptrAddr, new ConstExpr(2));
        var knownSegVal = Variables.TryGetKnownMemoryVariable(highAddr, ptrSeg);
        Expr segValue = knownSegVal != null ? knownSegVal : new MemExpr(highAddr, ptrSeg);

        // Загружаем offset в gp-регистр (аналогично MOV/LEA — без создания SetOperation)
        block.EndRegisters = block.EndRegisters.Set16(instr.Operand1.Value, offsetExpr);

        // Выбираем целевой сегментный регистр
        int segIndex = instr.Mnemonic == Mnemonic.LDS ? 3 /* DS */ : 0 /* ES */;
        block.EndRegisters = block.EndRegisters.SetSegment(segIndex, segValue);
    }

    /// <summary>
    /// Обрабатывает XCHG — обмен значений между двумя операндами.
    /// Поддерживает reg/reg и reg/mem формы (самые распространённые).
    /// Для памяти: читаем старое значение, пишем в память старое значение регистра,
    /// а в регистр — старое значение из памяти.
    /// </summary>
    private void HandleXchg(ExprBlock block, Instruction instr)
    {
        var op1 = instr.Operand1;
        var op2 = instr.Operand2;

        Expr val1 = GetExpression(op1, block.EndRegisters, instr.Segment);
        Expr val2 = GetExpression(op2, block.EndRegisters, instr.Segment);

        // Обновляем первый операнд значением второго
        if (op1.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(op1.Value, val2);
        }
        else if (op1.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(op1.Value, val2);
        }
        else if (op1.Type == OperandType.Memory)
        {
            var (addr, seg) = BuildMemoryReference(op1, block.EndRegisters, instr.Segment);
            block.Operations.Add(new StoreOperation(addr, seg, val2));
        }

        // Обновляем второй операнд значением первого (симметрично)
        if (op2.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(op2.Value, val1);
        }
        else if (op2.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(op2.Value, val1);
        }
        else if (op2.Type == OperandType.Memory)
        {
            var (addr, seg) = BuildMemoryReference(op2, block.EndRegisters, instr.Segment);
            block.Operations.Add(new StoreOperation(addr, seg, val1));
        }
    }

    /// <summary>
    /// Обрабатывает CBW — знаковое расширение AL → AX.
    /// Если старший бит AL = 1, то AH = 0xFF, иначе AH = 0.
    /// </summary>
    private void HandleCbw(ExprBlock block, Instruction instr)
    {
        var al = block.EndRegisters.Get8(0); // AL

        // signBit = (AL >> 7) & 1   → 0 или 1
        Expr signBit = Calculate(Math2Operation.Shr,
            Calculate(Math2Operation.And, al, new ConstExpr(0x80)),
            new ConstExpr(7));

        // high = ((0 - signBit) & 0xFF) << 8   → 0x0000 или 0xFF00
        // (0 - 1) даёт -1, -1 & 0xFF = 0xFF (благодаря constant folding)
        Expr minusSign = Calculate(Math2Operation.Sub, ConstExpr.Zero, signBit);
        Expr highByte = Calculate(Math2Operation.And, minusSign, new ConstExpr(0xFF));
        Expr high = Calculate(Math2Operation.Shl, highByte, new ConstExpr(8));

        Expr axValue = Calculate(Math2Operation.Or, high, Calculate(Math2Operation.And, al, new ConstExpr(0xFF)));

        block.EndRegisters = block.EndRegisters.Set16(0, axValue); // AX
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
    /// Обрабатывает ADD, SUB, ADC, SBB.
    /// Создаёт выражение с учётом операндов.
    /// Для ADC/SBB при известном CF=1 добавляет/вычитает единицу (приближённая модель).
    /// 
    /// Также обновляет флаги ZF и CF (для ADC/SBB CF обновляется упрощённо).
    /// </summary>
    private void HandleArithmetic(ExprBlock block, Instruction instr)
    {
        var dst = instr.Operand1;
        var srcExpr = GetExpression(instr.Operand2, block.EndRegisters, instr.Segment);
        var dstCurrent = GetExpression(dst, block.EndRegisters, instr.Segment);

        // Специальная обработка: SUB reg, reg → результат всегда 0
        if (instr.Mnemonic == Mnemonic.SUB && OperandsReferToSameLocation(dst, instr.Operand2))
        {
            if (dst.Type == OperandType.Register16)
                block.EndRegisters = block.EndRegisters.Set16(dst.Value, ConstExpr.Zero);
            else if (dst.Type == OperandType.Register8)
                block.EndRegisters = block.EndRegisters.Set8(dst.Value, ConstExpr.Zero);
            else if (dst.Type == OperandType.Memory)
            {
                var (addr, seg) = BuildMemoryReference(dst, block.EndRegisters, instr.Segment);
                block.Operations.Add(new StoreOperation(addr, seg, ConstExpr.Zero));
            }

            block.EndRegisters = ApplyArithmeticFlags(block.EndRegisters, ConstExpr.Zero);
            return;
        }

        bool isAdcSbb = instr.Mnemonic == Mnemonic.ADC || instr.Mnemonic == Mnemonic.SBB;
        bool isAddLike = instr.Mnemonic == Mnemonic.ADD || instr.Mnemonic == Mnemonic.ADC;

        var baseOp = isAddLike ? Math2Operation.Add : Math2Operation.Sub;
        Expr result = Calculate(baseOp, dstCurrent, srcExpr);

        // Для ADC/SBB добавляем/вычитаем CF (только если CF — известная константа 0/1)
        if (isAdcSbb)
        {
            Expr carry = ConstExpr.Zero;
            if (block.EndRegisters.CF is ConstExpr cfC && cfC.Value != 0)
                carry = ConstExpr.One;

            if (carry is ConstExpr c && c.Value != 0)
            {
                result = Calculate(baseOp, result, carry);
            }
            // Если CF символический — игнорируем carry (приближённая модель).
            // Полноценное моделирование carry требует более сложного IR.
        }

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

        // Обновление CF для ADD/SUB/ADC/SBB
        if (instr.Mnemonic == Mnemonic.SUB || instr.Mnemonic == Mnemonic.SBB)
        {
            var cfExpr = new CmpExpr(CmpOperation.Ult, dstCurrent, srcExpr);
            block.EndRegisters = block.EndRegisters with { CF = cfExpr };
        }
        else if (instr.Mnemonic == Mnemonic.ADD || instr.Mnemonic == Mnemonic.ADC)
        {
            var cfExpr = new CmpExpr(CmpOperation.Ult, result, dstCurrent);
            block.EndRegisters = block.EndRegisters with { CF = cfExpr };
        }
    }

    /// <summary>
    /// Обрабатывает INC и DEC (специальный случай арифметики на 1).
    /// 
    /// Важно: на реальном x86 INC/DEC **не затрагивают** флаг CF
    /// (в отличие от ADD/SUB 1). Поэтому мы не трогаем CF здесь.
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
    ///
    /// На x86 эти операции сбрасывают флаги CF и OF в 0.
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

        // Специальная обработка: XOR reg, reg  →  результат всегда 0
        // (даже если reg содержал Variable или сложное выражение)
        if (instr.Mnemonic == Mnemonic.XOR && OperandsReferToSameLocation(dst, instr.Operand2))
        {
            if (dst.Type == OperandType.Register16)
                block.EndRegisters = block.EndRegisters.Set16(dst.Value, ConstExpr.Zero);
            else if (dst.Type == OperandType.Register8)
                block.EndRegisters = block.EndRegisters.Set8(dst.Value, ConstExpr.Zero);
            else if (dst.Type == OperandType.Memory)
            {
                var (addr, seg) = BuildMemoryReference(dst, block.EndRegisters, instr.Segment);
                block.Operations.Add(new StoreOperation(addr, seg, ConstExpr.Zero));
            }

            block.EndRegisters = block.EndRegisters with { CF = ConstExpr.Zero, OF = ConstExpr.Zero };
            block.EndRegisters = ApplyArithmeticFlags(block.EndRegisters, ConstExpr.Zero);
            return;
        }

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

        // На реальном x86 AND, OR, XOR сбрасывают Carry и Overflow
        block.EndRegisters = block.EndRegisters with
        {
            CF = ConstExpr.Zero,
            OF = ConstExpr.Zero
        };
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
    ///
    /// - ZF = (left == right)
    /// - CF = (left u< right)   ← беззнаковое "меньше", соответствует биту переноса (borrow)
    ///
    /// Это позволяет корректно строить условия для JAE/JB/JA/JBE.
    /// </summary>
    private void HandleCmp(ExprBlock block, Instruction instr)
    {
        var left = GetExpression(instr.Operand1, block.EndRegisters, instr.Segment);
        var right = GetExpression(instr.Operand2, block.EndRegisters, instr.Segment);

        var zfExpr = new CmpExpr(CmpOperation.Eq, left, right);
        var cfExpr = new CmpExpr(CmpOperation.Ult, left, right); // left < right (unsigned) → CF

        block.EndRegisters = block.EndRegisters with
        {
            ZF = zfExpr,
            CF = cfExpr
            // SF и OF можно добавить позже при необходимости (для знаковых Jcc)
        };
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
    /// Обрабатывает INT (программное прерывание).
    ///
    /// Non-exit INT представляются как CallExpr, вызывающие функции из msdos.h
    /// (dos_open, dos_read, dos_print_string и др.) либо fallback к int86/intdos.
    ///
    /// Логика выбора операции:
    ///   - Если функция в msdos.h объявлена как <c>void</c> (dos_print_string, dos_char_output,
    ///     dos_set_current_drive, dos_exit и т.п.) → порождаем <b>CallOperation</b>.
    ///   - Если функция возвращает значение (dos_open, dos_read, dos_lseek и т.д.) →
    ///     порождаем <b>SetOperation(resultVar, CallExpr)</b> и кладём результат в AX.
    ///
    /// Это позволяет корректно моделировать как "fire-and-forget" прерывания,
    /// так и те, чей результат важен для дальнейшего кода.
    /// </summary>
    private void HandleInterrupt(ExprBlock block, Instruction instr)
    {
        if (instr.Operand1.Type != OperandType.Immediate8 &&
            instr.Operand1.Type != OperandType.Immediate16)
        {
            // TODO Подставлять CallExpr на intdos, int86 и т.д.
            throw new NotImplementedException($"INT with non-immediate operand is not supported: {instr}");
        }

        int vector = instr.Operand1.Value;

        var callExpr = DosInterruptHelper.CreateForInt(vector, block.EndRegisters);

        if (DosInterruptHelper.ShouldEmitAsCallOperation(vector, block.EndRegisters, callExpr))
        {
            // Функция объявлена как void в msdos.h (например dos_print_string, dos_set_current_drive и т.д.)
            // Порождаем чистый CallOperation без захвата результата.
            block.Operations.Add(new CallOperation(callExpr.Procedure, callExpr.Args));
        }
        else
        {
            // Функция возвращает значение (handle, код ошибки и т.д.) — захватываем в переменную.
            // Это позволяет использовать результат INT 21h в дальнейшем коде.
            var resultVar = Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, callExpr));

            // Символическое значение AX после INT — это результат вызова
            block.EndRegisters = block.EndRegisters.Set16(0, resultVar);
        }

        // Дополнительные эффекты (clobber регистров, CF и т.д.) — через хелпер
        block.EndRegisters = DosInterruptHelper.ApplyPostInterruptEffects(
            vector, block.EndRegisters, Variables, callExpr);
    }

    private void HandlePush(ExprBlock exprBlock, Instruction instr)
    {
        var expr = GetExpression(instr.Operand1, exprBlock.EndRegisters, instr.Segment);
        exprBlock.EndStack.Push(expr);
    }

    private void HandlePop(ExprBlock exprBlock, Instruction instr)
    {
        if (exprBlock.EndStack.Count == 0)
        {
            throw new InvalidOperationException(
                $"POP at offset {instr.Offset:X4} from empty symbolic stack (unbalanced PUSH/POP or indirect manipulation of SP)");
        }

        var value = exprBlock.EndStack.Pop();

        var dst = instr.Operand1;
        if (dst.Type == OperandType.Register16)
        {
            // POP reg16 — просто обновляем символическое значение (аналогично MOV, без SetOperation)
            exprBlock.EndRegisters = exprBlock.EndRegisters.Set16(dst.Value, value);
        }
        else if (dst.Type == OperandType.SegmentRegister)
        {
            // POP ES / SS / DS (CS через POP невозможен на 8086)
            exprBlock.EndRegisters = exprBlock.EndRegisters.SetSegment(dst.Value, value);
        }
        else if (dst.Type == OperandType.Memory)
        {
            // POP WORD PTR [addr] — создаём StoreOperation
            var (addr, seg) = BuildMemoryReference(dst, exprBlock.EndRegisters, instr.Segment);
            exprBlock.Operations.Add(new StoreOperation(addr, seg, value));
        }
        else
        {
            throw new NotImplementedException($"POP into {dst.Type} is not supported");
        }
    }

    /// <summary>
    /// Обрабатывает LEAVE — стандартный эпилог процедуры (mov sp, bp; pop bp).
    /// Обновляет SP = BP, затем выполняет POP в BP (берёт значение со стека).
    /// </summary>
    private void HandleLeave(ExprBlock block, Instruction instr)
    {
        // SP ← BP
        var bpValue = block.EndRegisters.Get16(5); // BP = 5
        block.EndRegisters = block.EndRegisters.Set16(4, bpValue); // SP = 4

        // POP BP: берём значение со стека (если есть)
        if (block.EndStack.Count > 0)
        {
            var value = block.EndStack.Pop();
            block.EndRegisters = block.EndRegisters.Set16(5, value); // BP
        }
        else
        {
            // Если стек символически пуст — создаём "неизвестное" значение из памяти
            // (редко, но корректно для анализа)
            var spAddr = block.EndRegisters.Get16(4);
            var memVal = new MemExpr(spAddr, block.EndRegisters.GetSegment(2)); // SS
            block.EndRegisters = block.EndRegisters.Set16(5, memVal);
        }
    }

    /// <summary>
    /// Обрабатывает CALL и CALL_FAR.
    ///
    /// Прямые near-вызовы (E8) получают имя вида "sub_XXXX" по целевому адресу в образе.
    /// Косвенные вызовы (FF/2, FF/3) представляются как indirect_call/far_sub с выражением адреса в аргументах.
    ///
    /// Все вызовы по умолчанию моделируются как возвращающие значение (в AX):
    /// создаём SetOperation(resultVar, CallExpr) и обновляем AX = resultVar.
    /// Это позволяет продолжать symbolic execution кода, который использует результат вызова.
    ///
    /// Аргументы функций пока не анализируются (требуется восстановление соглашений о вызовах и анализ стека).
    /// </summary>
    private void HandleCall(ExprBlock block, Instruction instr)
    {
        string name;
        var args = new List<Expr>();

        var op = instr.Operand1.IsSet ? instr.Operand1 : instr.Operand2;

        if (op.Type == OperandType.Relative16)
        {
            // Прямой near call. Target — уже вычисленный абсолютный адрес в образе.
            name = $"sub_{op.Value:X4}";
        }
        else if (instr.Mnemonic == Mnemonic.CALL_FAR)
        {
            name = "far_sub";
            if (op.Type == OperandType.Memory)
            {
                var targetExpr = GetExpression(op, block.EndRegisters, instr.Segment);
                args.Add(targetExpr);
            }
        }
        else if (op.Type == OperandType.Memory || op.Type == OperandType.Register16)
        {
            // Косвенный near call (обычно FF /2)
            name = "indirect_call";
            var targetExpr = GetExpression(op, block.EndRegisters, instr.Segment);
            args.Add(targetExpr);
        }
        else
        {
            name = "unknown_call";
        }

        var proc = new Procedure { Name = name };
        var callExpr = new CallExpr(proc, args);

        // Моделируем возврат значения через AX (стандартная практика для DOS/QuickC).
        var resultVar = Variables.CreateVariable();
        block.Operations.Add(new SetOperation(resultVar, callExpr));
        block.EndRegisters = block.EndRegisters.Set16(0, resultVar);
    }

    /// <summary>
    /// Проверяет, указывают ли два операнда на одно и то же место
    /// (один и тот же регистр или одна и та же ячейка памяти).
    /// Используется для оптимизации XOR reg,reg → 0 и SUB reg,reg → 0.
    /// </summary>
    private static bool OperandsReferToSameLocation(Operand op1, Operand op2)
    {
        if (op1.Type != op2.Type)
            return false;

        return op1.Type switch
        {
            OperandType.Register8 or OperandType.Register16 => op1.Value == op2.Value,
            OperandType.Memory => op1.BaseReg == op2.BaseReg &&
                                  op1.IndexReg == op2.IndexReg &&
                                  op1.Value == op2.Value,
            _ => false
        };
    }

}