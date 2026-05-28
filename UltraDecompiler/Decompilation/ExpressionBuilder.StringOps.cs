using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Обработка одиночных (без REP) строковых инструкций 8086.
/// 
/// Все инструкции здесь разлагаются на существующие примитивы IR:
/// - StoreOperation (запись в память)
/// - SetOperation (запись в регистр/переменную)
/// - CmpExpr (для обновления флагов в CMPS/SCAS)
/// 
/// Обновление SI/DI происходит с учётом текущего значения DF в EndRegisters.
/// Специальные String*Operation не создаются (по архитектурному решению).
/// </summary>
public partial class ExpressionBuilder
{
    // ============================================================
    // MOVS — копирование из [DS:SI] в [ES:DI]
    // ============================================================

    private void HandleStringMove(ExprBlock block, Instruction instr)
    {
        int size = instr.Mnemonic == Mnemonic.MOVSB ? 1 : 2;

        if (HasRepPrefix(instr))
        {
            EmitRepStringLoop(block, instr, size, StringOpKind.Move);
            return;
        }

        // Обычная (не REP) версия
        Expr src = BuildStringMemoryRead(block, instr, isSource: true, size);
        var (dstAddr, dstSeg) = BuildStringMemoryAddress(block, isDestination: true);

        block.Operations.Add(new StoreOperation(dstAddr, dstSeg, src));
        UpdateStringPointers(block, size, updateSi: true, updateDi: true);
    }

    private bool HasRepPrefix(Instruction instr)
    {
        return instr.Prefix.HasFlag(InstructionPrefix.REPZ) ||
               instr.Prefix.HasFlag(InstructionPrefix.REPNZ);
    }

    /// <summary>
    /// Проверяет, равен ли CX нулю (как константа). Если да — REP-инструкция не выполняется ни разу.
    /// Это важный edge case.
    /// </summary>
    private bool IsRepCxZero(ExprBlock block)
    {
        var cx = block.EndRegisters.Get16(1);
        return cx is ConstExpr c && c.Value == 0;
    }

    private enum StringOpKind { Move, Store, Load }

    // ============================================================
    // STOS — запись AL/AX в [ES:DI]
    // ============================================================

    private void HandleStringStore(ExprBlock block, Instruction instr)
    {
        int size = instr.Mnemonic == Mnemonic.STOSB ? 1 : 2;

        if (HasRepPrefix(instr))
        {
            EmitRepStringLoop(block, instr, size, StringOpKind.Store);
            return;
        }

        Expr value = size == 1 
            ? block.EndRegisters.Get8(0)   // AL
            : block.EndRegisters.Get16(0); // AX

        var (dstAddr, dstSeg) = BuildStringMemoryAddress(block, isDestination: true);

        block.Operations.Add(new StoreOperation(dstAddr, dstSeg, value));
        UpdateStringPointers(block, size, updateSi: false, updateDi: true);
    }

    // ============================================================
    // LODS — загрузка из [DS:SI] в AL/AX
    // ============================================================

    private void HandleStringLoad(ExprBlock block, Instruction instr)
    {
        int size = instr.Mnemonic == Mnemonic.LODSB ? 1 : 2;

        if (HasRepPrefix(instr))
        {
            EmitRepStringLoop(block, instr, size, StringOpKind.Load);
            return;
        }

        Expr value = BuildStringMemoryRead(block, instr, isSource: true, size);

        if (size == 1)
            block.EndRegisters = block.EndRegisters.Set8(0, value);  // AL
        else
            block.EndRegisters = block.EndRegisters.Set16(0, value); // AX

        UpdateStringPointers(block, size, updateSi: true, updateDi: false);
    }

    // ============================================================
    // CMPS — сравнение [DS:SI] ? [ES:DI]
    // ============================================================

    private void HandleStringCompare(ExprBlock block, Instruction instr)
    {
        int size = instr.Mnemonic == Mnemonic.CMPSB ? 1 : 2;

        if (HasRepPrefix(instr))
        {
            EmitRepCompareScanLoop(block, instr, size, isCompare: true);
            return;
        }

        Expr left  = BuildStringMemoryRead(block, instr, isSource: true, size);  // [DS:SI]
        Expr right = BuildStringMemoryRead(block, instr, isSource: false, size); // [ES:DI]

        // Обновляем флаги как при обычном CMP
        HandleStringComparisonFlags(block, left, right);

        UpdateStringPointers(block, size, updateSi: true, updateDi: true);
    }

    // ============================================================
    // SCAS — сравнение AL/AX ? [ES:DI]
    // ============================================================

    private void HandleStringScan(ExprBlock block, Instruction instr)
    {
        int size = instr.Mnemonic == Mnemonic.SCASB ? 1 : 2;

        if (HasRepPrefix(instr))
        {
            EmitRepCompareScanLoop(block, instr, size, isCompare: false);
            return;
        }

        Expr left = size == 1 
            ? block.EndRegisters.Get8(0) 
            : block.EndRegisters.Get16(0);

        Expr right = BuildStringMemoryRead(block, instr, isSource: false, size); // [ES:DI]

        HandleStringComparisonFlags(block, left, right);

        UpdateStringPointers(block, size, updateSi: false, updateDi: true);
    }

    // ============================================================
    // Вспомогательные методы
    // ============================================================

    /// <summary>
    /// Строит MemExpr для чтения из памяти в контексте строковой инструкции.
    /// Учитывает сегментный префикс и то, является ли это источником (SI + DS) или приёмником (DI + ES).
    /// </summary>
    private Expr BuildStringMemoryRead(ExprBlock block, Instruction instr, bool isSource, int size)
    {
        var (addr, seg) = BuildStringMemoryAddress(block, !isSource);

        // Если есть явный сегментный префикс — он применяется к источнику или приёмнику соответственно
        Segment segOverride = instr.Segment;

        if (segOverride != Segment.None)
        {
            // Для строковых операций префикс обычно применяется к источнику (DS:SI)
            // Но для MOVS/CMPS/SCAS приёмник всегда ES, если нет префикса
            // Здесь мы упрощённо применяем префикс к адресу, если он указан
            int segIdx = segOverride switch
            {
                Segment.ES => 0,
                Segment.CS => 1,
                Segment.SS => 2,
                Segment.DS => 3,
                _ => 3
            };
            seg = block.EndRegisters.GetSegment(segIdx);
        }

        var mem = new MemExpr(addr, seg);
        return mem; // Для байт/слов — на уровне Store/Load размер неявный
    }

    /// <summary>
    /// Возвращает адрес и сегмент для строки:
    /// - isDestination = true  → DI + ES
    /// - isDestination = false → SI + DS
    /// </summary>
    private (Expr Address, Expr? Segment) BuildStringMemoryAddress(ExprBlock block, bool isDestination)
    {
        Expr ptr = isDestination 
            ? block.EndRegisters.Get16(7)  // DI (reg 7)
            : block.EndRegisters.Get16(6); // SI (reg 6)

        Expr? seg = isDestination
            ? block.EndRegisters.GetSegment(0) // ES
            : block.EndRegisters.GetSegment(3); // DS

        return (ptr, seg);
    }

    /// <summary>
    /// Обновляет SI и/или DI после строковой операции с учётом DF и размера.
    /// - Если DF известен как константа — вычисляем точный +/- размер.
    /// - Если DF — сложное выражение — сбрасываем соответствующий указатель в "неизвестное" (новая Variable).
    /// </summary>
    private void UpdateStringPointers(ExprBlock block, int size, bool updateSi = true, bool updateDi = true)
    {
        Expr df = block.EndRegisters.DF;

        Expr positive = new ConstExpr(size);
        Expr negative = new ConstExpr(-size);

        bool dfIsZero = df is ConstExpr c && c.Value == 0;
        bool dfIsOne  = df is ConstExpr c2 && c2.Value != 0;

        Expr deltaSi = dfIsZero ? positive : (dfIsOne ? negative : CreatePostLoopDeltaVariable("si_delta", size));
        Expr deltaDi = dfIsZero ? positive : (dfIsOne ? negative : CreatePostLoopDeltaVariable("di_delta", size));

        if (updateSi)
        {
            Expr currentSi = block.EndRegisters.Get16(6);
            Expr newSi = (deltaSi is ConstExpr) 
                ? Calculate(Math2Operation.Add, currentSi, deltaSi)
                : Variables.CreateVariable("si_after_str");
            block.EndRegisters = block.EndRegisters.Set16(6, newSi);
        }

        if (updateDi)
        {
            Expr currentDi = block.EndRegisters.Get16(7);
            Expr newDi = (deltaDi is ConstExpr)
                ? Calculate(Math2Operation.Add, currentDi, deltaDi)
                : Variables.CreateVariable("di_after_str");
            block.EndRegisters = block.EndRegisters.Set16(7, newDi);
        }
    }

    /// <summary>
    /// Создаёт "символическую дельту" для случая, когда DF неизвестен.
    /// На практике возвращает просто новую переменную (мы не знаем, в какую сторону и на сколько сдвинулся указатель).
    /// </summary>
    private Expr CreatePostLoopDeltaVariable(string baseName, int size)
    {
        // На текущем этапе мы не хотим усложнять IR сложными условными выражениями.
        // Поэтому просто создаём новую Variable, обозначающую "неизвестное изменение".
        return Variables.CreateVariable(baseName);
    }

    // ============================================================
    // REP MOVS / STOS / LODS — генерация циклов
    // ============================================================

    private void EmitRepStringLoop(ExprBlock block, Instruction instr, int size, StringOpKind kind)
    {
        // Edge case: если CX == 0 на входе — REP не выполняется ни разу
        if (IsRepCxZero(block))
        {
            return;
        }

        // Создаём loop-переменные
        var diLoop = Variables.CreateVariable();
        var cxLoop = Variables.CreateVariable();

        // === Инициализация ПЕРЕД циклом ===
        var initOps = new List<Operation>
        {
            new SetOperation(diLoop, block.EndRegisters.Get16(7)), // текущий DI
            new SetOperation(cxLoop, block.EndRegisters.Get16(1))  // текущий CX
        };

        // === Тело цикла (без инициализации) ===
        var loopBody = new List<Operation>();

        var iterationOps = kind switch
        {
            StringOpKind.Store => BuildOneStosIterationWithVars(block, instr, size, (Variable)diLoop, (Variable)cxLoop),
            // Для Move и Load пока оставляем старую логику
            _ => kind switch
            {
                StringOpKind.Move  => BuildOneMovsIteration(block, instr, size),
                StringOpKind.Load  => BuildOneLodsIteration(block, instr, size),
                _ => new List<Operation>()
            }
        };

        loopBody.AddRange(iterationOps);

        // Условие: cxLoop != 0
        Expr condition = new CmpExpr(CmpOperation.Ne, cxLoop, ConstExpr.Zero);

        // Добавляем инициализацию в Operations блока
        block.Operations.AddRange(initOps);

        // Добавляем сам цикл
        var whileLoop = new WhileOperation(condition, loopBody);
        block.Operations.Add(whileLoop);

        // Обновляем EndRegisters после REP-цикла
        ApplyRepLoopPostState(block, size, kind);
    }

    private IReadOnlyList<Operation> BuildOneMovsIteration(ExprBlock block, Instruction instr, int size)
    {
        var ops = new List<Operation>();

        Expr src = BuildStringMemoryRead(block, instr, isSource: true, size);
        var (dstAddr, dstSeg) = BuildStringMemoryAddress(block, isDestination: true);

        ops.Add(new StoreOperation(dstAddr, dstSeg, src));

        // Обновление SI/DI внутри тела цикла
        Expr df = block.EndRegisters.DF;
        Expr delta = (df is ConstExpr c && c.Value == 0)
            ? new ConstExpr(size)
            : new ConstExpr(-size);

        Expr currentSi = block.EndRegisters.Get16(6);
        Expr currentDi = block.EndRegisters.Get16(7);

        Expr newSi = Calculate(Math2Operation.Add, currentSi, delta);
        Expr newDi = Calculate(Math2Operation.Add, currentDi, delta);

        // Эмитим SetOperation на "текущие" значения указателей (в реальности они будут обновляться на каждой итерации)
        ops.Add(new SetOperation(Variables.CreateVariable(), newSi));
        ops.Add(new SetOperation(Variables.CreateVariable(), newDi));

        return ops;
    }

    /// <summary>
    /// Версия для REP, которая использует переданные переменные diVar и cxVar.
    /// Это позволяет генерировать понятные выражения вида:
    ///   [es:di_loop] = 0
    ///   di_loop = di_loop + 1
    ///   cx_loop = cx_loop - 1
    /// </summary>
    private IReadOnlyList<Operation> BuildOneStosIterationWithVars(
        ExprBlock block, Instruction instr, int size,
        Variable diVar, Variable cxVar)
    {
        var ops = new List<Operation>();

        Expr value = size == 1
            ? block.EndRegisters.Get8(0)
            : block.EndRegisters.Get16(0);

        // Используем diVar как адрес для Store
        var (_, dstSeg) = BuildStringMemoryAddress(block, isDestination: true);
        ops.Add(new StoreOperation(diVar, dstSeg, value));

        // Обновление DI относительно diVar
        Expr df = block.EndRegisters.DF;
        Expr delta = (df is ConstExpr c && c.Value == 0) ? new ConstExpr(size) : new ConstExpr(-size);

        Expr newDi = Calculate(Math2Operation.Add, diVar, delta);
        ops.Add(new SetOperation(diVar, newDi));

        // Обновление CX относительно cxVar
        Expr newCx = Calculate(Math2Operation.Sub, cxVar, ConstExpr.One);
        ops.Add(new SetOperation(cxVar, newCx));

        return ops;
    }

    private IReadOnlyList<Operation> BuildOneLodsIteration(ExprBlock block, Instruction instr, int size)
    {
        var ops = new List<Operation>();

        Expr value = BuildStringMemoryRead(block, instr, isSource: true, size);

        // Обновление AX/AL
        if (size == 1)
            ops.Add(new SetOperation(Variables.CreateVariable(), value));
        else
            ops.Add(new SetOperation(Variables.CreateVariable(), value));

        // Обновление SI
        Expr df = block.EndRegisters.DF;
        Expr delta = (df is ConstExpr c && c.Value == 0) ? new ConstExpr(size) : new ConstExpr(-size);
        Expr newSi = Calculate(Math2Operation.Add, block.EndRegisters.Get16(6), delta);
        ops.Add(new SetOperation(Variables.CreateVariable(), newSi));

        return ops;
    }

    // ============================================================
    // REPZ / REPNZ CMPS и SCAS — поддержка циклов с составными условиями выхода
    // ============================================================

    private void EmitRepCompareScanLoop(ExprBlock block, Instruction instr, int size, bool isCompare)
    {
        // Edge case: если CX == 0 на входе — REP не выполняется ни разу
        if (IsRepCxZero(block))
        {
            return;
        }

        bool isRepz = instr.Prefix.HasFlag(InstructionPrefix.REPZ);

        // Для REPZ (REPE): продолжаем, пока ZF == 1 (равны)
        // Для REPNZ (REPNE): продолжаем, пока ZF == 0 (не равны)
        Expr expectedZf = isRepz ? ConstExpr.One : ConstExpr.Zero;

        // === Строим тело одной итерации ===
        var bodyOps = new List<Operation>();

        Expr left, right;

        if (isCompare)
        {
            left  = BuildStringMemoryRead(block, instr, isSource: true, size);   // [DS:SI]
            right = BuildStringMemoryRead(block, instr, isSource: false, size);  // [ES:DI]
        }
        else
        {
            left  = size == 1 ? block.EndRegisters.Get8(0) : block.EndRegisters.Get16(0);
            right = BuildStringMemoryRead(block, instr, isSource: false, size);
        }

        // Добавляем операции сравнения + обновление флагов
        // Мы эмитим CmpExpr для ZF (основной флаг для REPZ/REPNZ)
        Expr equality = new CmpExpr(CmpOperation.Eq, left, right);

        // Обновляем флаги в EndRegisters **сразу** (это даст эффект "последнего сравнения" после цикла)
        // Для тела цикла мы всё равно добавляем операции, но флаги обновляем здесь для пост-состояния.
        block.EndRegisters = block.EndRegisters with
        {
            ZF = equality,
            CF = new CmpExpr(CmpOperation.Ult, left, right)
        };

        // Добавляем в тело "след" сравнения (для читаемости IR)
        bodyOps.Add(new SetOperation(Variables.CreateVariable("last_cmp"), equality));

        // Обновляем SI/DI внутри тела
        Expr df = block.EndRegisters.DF;
        Expr delta = (df is ConstExpr c && c.Value == 0)
            ? new ConstExpr(size)
            : new ConstExpr(-size);

        Expr newSi = Calculate(Math2Operation.Add, block.EndRegisters.Get16(6), delta);
        Expr newDi = Calculate(Math2Operation.Add, block.EndRegisters.Get16(7), delta);

        bodyOps.Add(new SetOperation(Variables.CreateVariable(), newSi));
        bodyOps.Add(new SetOperation(Variables.CreateVariable(), newDi));

        // Декремент CX
        Expr cxExpr = block.EndRegisters.Get16(1);
        Expr decrementedCx = Calculate(Math2Operation.Sub, cxExpr, ConstExpr.One);
        bodyOps.Add(new SetOperation(Variables.CreateVariable(), decrementedCx));

        // === Составное условие цикла ===
        // CX != 0 && ZF == expected
        Expr cxCondition = new CmpExpr(CmpOperation.Ne, cxExpr, ConstExpr.Zero);
        Expr zfCondition = new CmpExpr(CmpOperation.Eq, block.EndRegisters.ZF, expectedZf);
        Expr condition = RepBoolAnd(cxCondition, zfCondition);

        var whileLoop = new WhileOperation(condition, bodyOps);
        block.Operations.Add(whileLoop);

        // После цикла флаги уже обновлены на значение от "последнего" сравнения
        // (это компромисс текущей реализации)
    }

    // Простая булева конъюнкция (локальная версия)
    private static Expr RepBoolAnd(Expr a, Expr b)
    {
        if (a is ConstExpr ca)
        {
            if (ca.Value == 0) return ConstExpr.Zero;
            return b;
        }
        if (b is ConstExpr cb)
        {
            if (cb.Value == 0) return ConstExpr.Zero;
            return a;
        }
        return new Math2Expr(Math2Operation.And, a, b);
    }

    private void ApplyRepLoopPostState(ExprBlock block, int size, StringOpKind kind)
    {
        Expr cxBefore = block.EndRegisters.Get16(1); // CX до цикла

        bool cxIsConst = cxBefore is ConstExpr;
        int? cxValue = cxIsConst ? ((ConstExpr)cxBefore).Value : (int?)null;

        Expr df = block.EndRegisters.DF;
        bool dfForward = df is ConstExpr c && c.Value == 0;

        int deltaPerIter = dfForward ? size : -size;

        if (cxIsConst && cxValue.HasValue)
        {
            int totalDelta = cxValue.Value * deltaPerIter;

            Expr newSi = Calculate(Math2Operation.Add, block.EndRegisters.Get16(6), new ConstExpr(totalDelta));
            Expr newDi = Calculate(Math2Operation.Add, block.EndRegisters.Get16(7), new ConstExpr(totalDelta));
            Expr finalCx = ConstExpr.Zero;

            block.EndRegisters = block.EndRegisters
                .Set16(6, newSi)
                .Set16(7, newDi)
                .Set16(1, finalCx); // CX = 0
        }
        else
        {
            // Неизвестное количество итераций → создаём post-loop переменные
            block.EndRegisters = block.EndRegisters
                .Set16(6, Variables.CreateVariable("si_after_rep"))
                .Set16(7, Variables.CreateVariable("di_after_rep"))
                .Set16(1, Variables.CreateVariable("cx_after_rep"));
        }
    }

    /// <summary>
    /// Обновляет флаги после строкового сравнения (CMPS/SCAS).
    /// Аналогично обычному CMP.
    /// </summary>
    private void HandleStringComparisonFlags(ExprBlock block, Expr left, Expr right)
    {
        // ZF = (left == right)
        block.EndRegisters = block.EndRegisters with
        {
            ZF = new CmpExpr(CmpOperation.Eq, left, right),
            // CF, SF, OF — упрощённо, как в HandleCmp
            CF = new CmpExpr(CmpOperation.Ult, left, right)
        };
    }
}
