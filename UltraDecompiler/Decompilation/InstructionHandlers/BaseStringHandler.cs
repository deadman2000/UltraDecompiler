namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Базовый класс для обработчиков строковых операций (MOVS, STOS, LODS, CMPS, SCAS).
/// Содержит общие вспомогательные методы для работы с памятью и указателями.
/// </summary>
public abstract class BaseStringHandler : IInstructionHandler
{
    public abstract void Handle(ExprBlock block, Instruction instr);

    internal enum StringOpKind { Move, Store, Load }

    // ============================================================
    // Общие вспомогательные методы
    // ============================================================

    /// <summary>
    /// Строит MemExpr для чтения из памяти в контексте строковой инструкции.
    /// </summary>
    protected static Expr BuildStringMemoryRead(ExprBlock block, Instruction instr, bool isSource, int size)
    {
        var (addr, seg) = BuildStringMemoryAddress(block, !isSource);

        if (instr.Segment != Segment.None)
        {
            seg = block.EndRegisters.GetSegment(instr.Segment.ToCpuSegmentRegister());
        }

        return new MemExpr(addr, seg);
    }

    /// <summary>
    /// Возвращает адрес и сегмент для строки (DI+ES или SI+DS).
    /// </summary>
    protected static (Expr Address, Expr? Segment) BuildStringMemoryAddress(ExprBlock block, bool isDestination)
    {
        Expr ptr = isDestination
            ? block.EndRegisters.Get16(GpRegister16.DI)
            : block.EndRegisters.Get16(GpRegister16.SI);

        Expr? seg = isDestination
            ? block.EndRegisters.GetSegment(CpuSegmentRegister.ES)
            : block.EndRegisters.GetSegment(CpuSegmentRegister.DS);

        return (ptr, seg);
    }

    /// <summary>
    /// Обновляет SI и/или DI после строковой операции.
    /// </summary>
    protected static void UpdateStringPointers(ExprBlock block, int size, bool updateSi = true, bool updateDi = true)
    {
        Expr df = block.EndRegisters.DF;

        Expr positive = new ConstExpr(size);
        Expr negative = new ConstExpr(-size);

        bool dfIsZero = df is ConstExpr c && c.Value == 0;
        bool dfIsOne = df is ConstExpr c2 && c2.Value != 0;

        Expr deltaSi = dfIsZero ? positive : (dfIsOne ? negative : CreatePostLoopDeltaVariable(block, "si_delta", size));
        Expr deltaDi = dfIsZero ? positive : (dfIsOne ? negative : CreatePostLoopDeltaVariable(block, "di_delta", size));

        if (updateSi)
        {
            Expr currentSi = block.EndRegisters.Get16(GpRegister16.SI);
            Expr newSi = (deltaSi is ConstExpr)
                ? currentSi.Calculate(Math2Operation.Add, deltaSi)
                : block.Variables.CreateVariable("si_after_str");
            block.EndRegisters = block.EndRegisters.Set16(GpRegister16.SI, newSi);
        }

        if (updateDi)
        {
            Expr currentDi = block.EndRegisters.Get16(GpRegister16.DI);
            Expr newDi = (deltaDi is ConstExpr)
                ? currentDi.Calculate(Math2Operation.Add, deltaDi)
                : block.Variables.CreateVariable("di_after_str");
            block.EndRegisters = block.EndRegisters.Set16(GpRegister16.DI, newDi);
        }
    }

    /// <summary>
    /// Создаёт "символическую дельту" для случая, когда DF неизвестен.
    /// </summary>
    protected static Expr CreatePostLoopDeltaVariable(ExprBlock block, string baseName, int size)
    {
        return block.Variables.CreateVariable(baseName);
    }

    /// <summary>
    /// Проверяет, равен ли CX нулю (как константа).
    /// </summary>
    protected static bool IsRepCxZero(ExprBlock block)
    {
        var cx = block.EndRegisters.Get16(GpRegister16.CX);
        return cx is ConstExpr c && c.Value == 0;
    }

    /// <summary>
    /// Возвращает размер операции (1 для байтовых, 2 для словных).
    /// </summary>
    protected static int GetOperationSize(Mnemonic mnemonic)
    {
        return mnemonic switch
        {
            Mnemonic.MOVSB or Mnemonic.STOSB or Mnemonic.LODSB or Mnemonic.CMPSB or Mnemonic.SCASB => 1,
            Mnemonic.MOVSW or Mnemonic.STOSW or Mnemonic.LODSW or Mnemonic.CMPSW or Mnemonic.SCASW => 2,
            _ => 1
        };
    }

    internal static void EmitRepStringLoop(ExprBlock block, Instruction instr, int size, StringOpKind kind)
    {
        // Edge case: если CX == 0 на входе — REP не выполняется ни разу
        if (IsRepCxZero(block))
        {
            return;
        }

        // Создаём loop-переменные
        var siLoop = block.Variables.CreateVariable();
        var diLoop = block.Variables.CreateVariable();
        var cxLoop = block.Variables.CreateVariable();

        // === Инициализация ПЕРЕД циклом ===
        var initOps = new List<Operation>
        {
            new SetOperation(siLoop, block.EndRegisters.Get16(GpRegister16.SI)),
            new SetOperation(diLoop, block.EndRegisters.Get16(GpRegister16.DI)),
            new SetOperation(cxLoop, block.EndRegisters.Get16(GpRegister16.CX))
        };

        // === Тело цикла (без инициализации) ===
        var loopBody = new List<Operation>();

        var iterationOps = kind switch
        {
            StringOpKind.Store => BuildOneStosIterationWithVars(block, size, diLoop, cxLoop),
            StringOpKind.Move => BuildOneMovsIterationWithVars(block, instr, size, siLoop, diLoop),
            StringOpKind.Load => BuildOneLodsIterationWithVars(block, instr, size, siLoop),
            _ => new List<Operation>()
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

    /// <summary>
    /// Версия для REP, которая использует переданные переменные diVar и cxVar.
    /// Это позволяет генерировать понятные выражения вида:
    ///   [es:di_loop] = 0
    ///   di_loop = di_loop + 1
    ///   cx_loop = cx_loop - 1
    /// </summary>
    private static IReadOnlyList<Operation> BuildOneStosIterationWithVars(
        ExprBlock block, int size,
        Variable diVar, Variable cxVar)
    {
        var ops = new List<Operation>();

        Expr value = size == 1
            ? block.EndRegisters.Get8(GpRegister8.AL)
            : block.EndRegisters.Get16(GpRegister16.AX);

        // Используем diVar как адрес для Store
        var (_, dstSeg) = BuildStringMemoryAddress(block, isDestination: true);
        ops.Add(new StoreOperation(diVar, dstSeg, value));

        // Обновление DI относительно diVar
        Expr df = block.EndRegisters.DF;
        Expr delta = (df is ConstExpr c && c.Value == 0) ? new ConstExpr(size) : new ConstExpr(-size);

        Expr newDi = diVar.Calculate(Math2Operation.Add, delta);
        ops.Add(new SetOperation(diVar, newDi));

        // Обновление CX относительно cxVar
        Expr newCx = cxVar.Calculate(Math2Operation.Sub, ConstExpr.One);
        ops.Add(new SetOperation(cxVar, newCx));

        return ops;
    }

    private static IReadOnlyList<Operation> BuildOneMovsIterationWithVars(
        ExprBlock block, Instruction instr, int size,
        Variable siVar, Variable diVar)
    {
        var ops = new List<Operation>();

        Expr src = BuildStringMemoryRead(block, instr, isSource: true, size);
        var (_, dstSeg) = BuildStringMemoryAddress(block, isDestination: true);

        ops.Add(new StoreOperation(diVar, dstSeg, src));

        Expr df = block.EndRegisters.DF;
        Expr delta = (df is ConstExpr c && c.Value == 0)
            ? new ConstExpr(size)
            : new ConstExpr(-size);

        Expr newSi = siVar.Calculate(Math2Operation.Add, delta);
        Expr newDi = diVar.Calculate(Math2Operation.Add, delta);

        ops.Add(new SetOperation(siVar, newSi));
        ops.Add(new SetOperation(diVar, newDi));

        return ops;
    }

    private static IReadOnlyList<Operation> BuildOneLodsIterationWithVars(
        ExprBlock block, Instruction instr, int size,
        Variable siVar)
    {
        var ops = new List<Operation>();

        Expr value = BuildStringMemoryRead(block, instr, isSource: true, size);

        if (size == 1)
            ops.Add(new SetOperation(block.Variables.CreateVariable(), value));
        else
            ops.Add(new SetOperation(block.Variables.CreateVariable(), value));

        Expr df = block.EndRegisters.DF;
        Expr delta = (df is ConstExpr c && c.Value == 0) ? new ConstExpr(size) : new ConstExpr(-size);
        Expr newSi = siVar.Calculate(Math2Operation.Add, delta);
        ops.Add(new SetOperation(siVar, newSi));

        return ops;
    }

    private static void ApplyRepLoopPostState(ExprBlock block, int size, StringOpKind kind)
    {
        Expr cxBefore = block.EndRegisters.Get16(GpRegister16.CX);

        bool cxIsConst = cxBefore is ConstExpr;
        int? cxValue = cxIsConst ? ((ConstExpr)cxBefore).Value : null;

        Expr df = block.EndRegisters.DF;
        bool dfForward = df is ConstExpr c && c.Value == 0;

        int deltaPerIter = dfForward ? size : -size;

        if (cxIsConst && cxValue.HasValue)
        {
            int totalDelta = cxValue.Value * deltaPerIter;

            Expr newSi = block.EndRegisters.Get16(GpRegister16.SI).Calculate(Math2Operation.Add, new ConstExpr(totalDelta));
            Expr newDi = block.EndRegisters.Get16(GpRegister16.DI).Calculate(Math2Operation.Add, new ConstExpr(totalDelta));
            Expr finalCx = ConstExpr.Zero;

            block.EndRegisters = block.EndRegisters
                .Set16(GpRegister16.SI, newSi)
                .Set16(GpRegister16.DI, newDi)
                .Set16(GpRegister16.CX, finalCx);
        }
        else
        {
            // Неизвестное количество итераций → создаём post-loop переменные
            block.EndRegisters = block.EndRegisters
                .Set16(GpRegister16.SI, block.Variables.CreateVariable("si_after_rep"))
                .Set16(GpRegister16.DI, block.Variables.CreateVariable("di_after_rep"))
                .Set16(GpRegister16.CX, block.Variables.CreateVariable("cx_after_rep"));
        }
    }

    internal static void EmitRepCompareScanLoop(ExprBlock block, Instruction instr, int size, bool isCompare)
    {
        if (IsRepCxZero(block))
        {
            return;
        }

        bool isRepz = instr.Prefix.HasFlag(InstructionPrefix.REPZ);
        Expr expectedZf = isRepz ? ConstExpr.One : ConstExpr.Zero;

        var siLoop = block.Variables.CreateVariable();
        var diLoop = block.Variables.CreateVariable();
        var cxLoop = block.Variables.CreateVariable();

        var initOps = new List<Operation>
        {
            new SetOperation(siLoop, block.EndRegisters.Get16(GpRegister16.SI)),
            new SetOperation(diLoop, block.EndRegisters.Get16(GpRegister16.DI)),
            new SetOperation(cxLoop, block.EndRegisters.Get16(GpRegister16.CX))
        };

        var loopBody = new List<Operation>();

        Expr left, right;

        if (isCompare)
        {
            left = BuildStringMemoryRead(block, instr, isSource: true, size);
            right = BuildStringMemoryRead(block, instr, isSource: false, size);
        }
        else
        {
            left = size == 1 ? block.EndRegisters.Get8(GpRegister8.AL) : block.EndRegisters.Get16(GpRegister16.AX);
            right = BuildStringMemoryRead(block, instr, isSource: false, size);
        }

        Expr equality = new CmpExpr(CmpOperation.Eq, left, right);

        block.EndRegisters = block.EndRegisters with
        {
            ZF = equality,
            CF = new CmpExpr(CmpOperation.Ult, left, right)
        };

        loopBody.Add(new SetOperation(block.Variables.CreateVariable(), equality));

        Expr df = block.EndRegisters.DF;
        Expr delta = (df is ConstExpr c && c.Value == 0)
            ? new ConstExpr(size)
            : new ConstExpr(-size);

        Expr newSi = siLoop.Calculate(Math2Operation.Add, delta);
        Expr newDi = diLoop.Calculate(Math2Operation.Add, delta);
        Expr newCx = cxLoop.Calculate(Math2Operation.Sub, ConstExpr.One);

        loopBody.Add(new SetOperation(siLoop, newSi));
        loopBody.Add(new SetOperation(diLoop, newDi));
        loopBody.Add(new SetOperation(cxLoop, newCx));

        Expr cxCondition = new CmpExpr(CmpOperation.Ne, cxLoop, ConstExpr.Zero);
        Expr zfCondition = new CmpExpr(CmpOperation.Eq, block.EndRegisters.ZF, expectedZf);
        Expr condition = RepBoolAnd(cxCondition, zfCondition);

        block.Operations.AddRange(initOps);

        var whileLoop = new WhileOperation(condition, loopBody);
        block.Operations.Add(whileLoop);
    }

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

}
