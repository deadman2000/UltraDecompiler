using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Распознавание циклов QuickC /Ox со счётчиком в регистре (типично SI).
/// </summary>
public partial class ExpressionBuilderQuickCOpt
{
    private static readonly GpRegister16[] OxCounterRegisters =
    [
        GpRegister16.SI,
        GpRegister16.DI,
        GpRegister16.BX,
        GpRegister16.CX,
    ];

    /// <inheritdoc />
    protected override bool TryBuildOxRegisterCounterLoop(
        ExprBlock header,
        ExprBlock bodyStart,
        ExprBlock? exitStart,
        List<Operation> loopBody,
        List<Operation> initSearchList,
        out ForOperation forOp,
        out Variable? spillCounterVar)
    {
        forOp = null!;
        spillCounterVar = null;

        if (!TryParseOxRegisterLoopHeader(header, out var counterReg, out var limit, out var countDown))
        {
            return false;
        }

        if (!TryFindOxRegisterInit(counterReg, header.BasicBlock.StartOffset, out var initValue))
        {
            return false;
        }

        if (!TryFindOxCounterVariable(exitStart, counterReg, out var counterVar))
        {
            return false;
        }

        if (!TryGetOxLoopIteration(bodyStart, counterReg, out var isIncrement))
        {
            return false;
        }

        if (countDown && isIncrement || !countDown && !isIncrement)
        {
            return false;
        }

        var bodyOps = BuildOxRegisterLoopBody(bodyStart, counterReg, counterVar);
        if (bodyOps.Count == 0)
        {
            return false;
        }

        var condition = BuildOxLoopCondition(counterVar, limit, countDown);
        Operation iteration = isIncrement
            ? new IncOperation(counterVar)
            : new DecOperation(counterVar);

        forOp = new ForOperation(
            new SetOperation(counterVar, initValue),
            condition,
            iteration,
            bodyOps);

        spillCounterVar = counterVar;
        return true;
    }

    /// <summary>
    /// Разбирает заголовок Ox-цикла: <c>cmp reg, N; jl</c> или <c>and reg, reg; jg</c>.
    /// </summary>
    private static bool TryParseOxRegisterLoopHeader(
        ExprBlock header,
        out GpRegister16 counterReg,
        out ushort limit,
        out bool countDown)
    {
        counterReg = default;
        limit = 0;
        countDown = false;

        var instrs = header.BasicBlock.Instructions;
        if (instrs.Count < 2)
        {
            return false;
        }

        for (var i = 0; i < instrs.Count - 1; i++)
        {
            var test = instrs[i];
            var branch = instrs[i + 1];

            if (test.Mnemonic == Mnemonic.CMP
                && test.Operand1.Type == OperandType.Register16
                && test.Operand2.Type == OperandType.Immediate16
                && branch.Mnemonic is Mnemonic.JL or Mnemonic.JB or Mnemonic.JLE or Mnemonic.JBE)
            {
                var reg = test.Operand1.AsGpRegister16();
                if (!IsOxCounterRegister(reg))
                {
                    continue;
                }

                counterReg = reg;
                limit = (ushort)test.Operand2.Value;
                countDown = false;
                return true;
            }

            if (test.Mnemonic == Mnemonic.AND
                && test.Operand1.Type == OperandType.Register16
                && test.Operand2.Type == OperandType.Register16
                && test.Operand1.ReferToSameLocation(test.Operand2)
                && branch.Mnemonic is Mnemonic.JG or Mnemonic.JGE)
            {
                var reg = test.Operand1.AsGpRegister16();
                if (!IsOxCounterRegister(reg))
                {
                    continue;
                }

                counterReg = reg;
                limit = 0;
                countDown = true;
                return true;
            }
        }

        return false;
    }

    private static bool IsOxCounterRegister(GpRegister16 reg) =>
        Array.IndexOf(OxCounterRegisters, reg) >= 0;

    /// <summary>Ищет <c>mov reg, imm</c> перед заголовком цикла.</summary>
    private bool TryFindOxRegisterInit(GpRegister16 counterReg, int headerOffset, out ConstExpr initValue)
    {
        initValue = ConstExpr.Zero;

        foreach (var block in Blocks.OrderBy(static b => b.BasicBlock.StartOffset))
        {
            if (block.BasicBlock.StartOffset >= headerOffset)
            {
                break;
            }

            foreach (var instr in block.BasicBlock.Instructions)
            {
                if (instr.Mnemonic != Mnemonic.MOV)
                {
                    continue;
                }

                if (instr.Operand1.Type != OperandType.Register16
                    || instr.Operand1.AsGpRegister16() != counterReg
                    || instr.Operand2.Type != OperandType.Immediate16)
                {
                    continue;
                }

                initValue = new ConstExpr(instr.Operand2.Value);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Находит стековую переменную-счётчик по spill <c>mov [bp-N], reg</c> в блоке выхода.
    /// </summary>
    private bool TryFindOxCounterVariable(ExprBlock? exitStart, GpRegister16 counterReg, out Variable counterVar)
    {
        counterVar = null!;

        if (exitStart is null)
        {
            return false;
        }

        foreach (var instr in exitStart.BasicBlock.Instructions)
        {
            if (instr.Mnemonic != Mnemonic.MOV
                || instr.Operand1.Type != OperandType.Memory
                || instr.Operand1.BaseReg != AddressRegister.BP
                || instr.Operand1.IndexReg != AddressRegister.None
                || instr.Operand2.Type != OperandType.Register16
                || instr.Operand2.AsGpRegister16() != counterReg)
            {
                continue;
            }

            var local = Variables.TryGetStackLocal(instr.Operand1.Value);
            if (local is not null)
            {
                counterVar = local;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetOxLoopIteration(ExprBlock bodyStart, GpRegister16 counterReg, out bool isIncrement)
    {
        isIncrement = false;

        foreach (var instr in bodyStart.BasicBlock.Instructions)
        {
            if (instr.Mnemonic == Mnemonic.INC
                && instr.Operand1.Type == OperandType.Register16
                && instr.Operand1.AsGpRegister16() == counterReg)
            {
                isIncrement = true;
                return true;
            }

            if (instr.Mnemonic == Mnemonic.DEC
                && instr.Operand1.Type == OperandType.Register16
                && instr.Operand1.AsGpRegister16() == counterReg)
            {
                isIncrement = false;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Собирает тело цикла из инструкций: <c>add [bp-N], reg</c> (аккумулятор += счётчик).
    /// </summary>
    private List<Operation> BuildOxRegisterLoopBody(
        ExprBlock bodyStart,
        GpRegister16 counterReg,
        Variable counterVar)
    {
        var ops = new List<Operation>();

        foreach (var instr in bodyStart.BasicBlock.Instructions)
        {
            if (instr.Mnemonic is Mnemonic.INC or Mnemonic.DEC)
            {
                continue;
            }

            if (instr.Mnemonic != Mnemonic.ADD
                || instr.Operand1.Type != OperandType.Memory
                || instr.Operand1.BaseReg != AddressRegister.BP
                || instr.Operand1.IndexReg != AddressRegister.None
                || instr.Operand2.Type != OperandType.Register16
                || instr.Operand2.AsGpRegister16() != counterReg)
            {
                continue;
            }

            var accVar = Variables.TryGetStackLocal(instr.Operand1.Value);
            if (accVar is null)
            {
                continue;
            }

            ops.Add(new AddAssignOperation(accVar, counterVar));
        }

        return ops;
    }

    private static CmpExpr BuildOxLoopCondition(Variable counterVar, ushort limit, bool countDown) =>
        countDown
            ? new CmpExpr(CmpOperation.Ugt, counterVar, ConstExpr.Zero)
            : new CmpExpr(CmpOperation.Ult, counterVar, new ConstExpr(limit));
}