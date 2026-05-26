using UltraDecompiler.Disassembler;
using UltraDecompiler.Graph;

namespace UltraDecompiler.Decompilation;

public class Decompiler
{
    public List<CodeBlock> Blocks { get; } = [];

    public VariableStorage Variables { get; } = new();

    public void Decompile(ControlFlowGraph graph, bool isCom = false)
    {
        Blocks.Clear();
        Variables.Clear();

        var registers = isCom
            ? RegisterExpressions.InitCom(Variables)
            : RegisterExpressions.InitExe(Variables);

        Dictionary<BasicBlock, CodeBlock> blocksMap = [];

        var queue = new Queue<BasicBlock>();
        queue.Enqueue(graph.EntryBlock);

        var visited = new HashSet<BasicBlock>();

        while (queue.Count > 0)
        {
            var block = queue.Dequeue();
            if (visited.Contains(block))
                continue;

            visited.Add(block);

            var codeBlock = GenerateCode(block, registers);
            Blocks.Add(codeBlock);
            blocksMap[block] = codeBlock;

            if (block.NextBlock != null)
                queue.Enqueue(block.NextBlock);
            if (block.ConditionalBlock != null)
                queue.Enqueue(block.ConditionalBlock);
        }

        // Связываем CodeBlock'и по ссылкам из BasicBlock
        foreach (var kvp in blocksMap)
        {
            var codeBlock = kvp.Value;
            var basicBlock = kvp.Key;

            if (basicBlock.NextBlock != null && blocksMap.TryGetValue(basicBlock.NextBlock, out var nextCode))
                codeBlock.Next = nextCode;

            if (basicBlock.ConditionalBlock != null && blocksMap.TryGetValue(basicBlock.ConditionalBlock, out var condCode))
            {
                codeBlock.ConditionalBlock = condCode;
                // TODO: полноценная поддержка условий на основе флагов (CMP + Jcc)
                codeBlock.Condition = new ConstExpr(1); // placeholder для условного перехода
            }
        }
    }

    private CodeBlock GenerateCode(BasicBlock block, RegisterExpressions registers)
    {
        var codeBlock = new CodeBlock(block)
        {
            InitRegisters = registers
        };

        foreach (var instr in block.Instructions)
        {
            switch (instr.Mnemonic)
            {
                case Mnemonic.MOV:
                    var exprSrc = GetExpression(instr.Operand2, registers);
                    if (instr.Operand1.Type == OperandType.Register16)
                    {
                        registers = registers.Set16(instr.Operand1.Value, exprSrc);
                    }
                    else if (instr.Operand1.Type == OperandType.Register8)
                    {
                        registers = registers.Set8(instr.Operand1.Value, exprSrc);
                    }
                    else if (instr.Operand1.Type == OperandType.SegmentRegister)
                    {
                        registers = registers.SetSegment(instr.Operand1.Value, exprSrc);
                    }
                    // TODO: поддержка регистров памяти, сегментных
                    break;

                case Mnemonic.LEA:
                    if (instr.Operand1.Type == OperandType.Register16)
                    {
                        // LEA загружает эффективный адрес (offset) в регистр (индексный или общий)
                        // Полная поддержка Memory будет позже; placeholder чтобы не падать и регистр поддерживался
                        var eaExpr = ConstExpr.Zero;
                        registers = registers.Set16(instr.Operand1.Value, eaExpr);
                    }
                    break;

                case Mnemonic.ADD:
                case Mnemonic.SUB:
                    HandleArithmetic(codeBlock, instr, registers, ref registers);
                    break;

                case Mnemonic.INC:
                    HandleIncDec(codeBlock, instr, registers, true, ref registers);
                    break;

                case Mnemonic.DEC:
                    HandleIncDec(codeBlock, instr, registers, false, ref registers);
                    break;

                case Mnemonic.AND:
                case Mnemonic.OR:
                case Mnemonic.XOR:
                    HandleLogical(codeBlock, instr, registers, ref registers);
                    break;

                case Mnemonic.NOT:
                    HandleUnary(codeBlock, instr, Math1Operation.Not, registers, ref registers);
                    break;

                case Mnemonic.NEG:
                    HandleUnary(codeBlock, instr, Math1Operation.Neg, registers, ref registers);
                    break;

                case Mnemonic.SAL:
                    HandleShift(codeBlock, instr, Math2Operation.Shl, registers, ref registers);
                    break;

                case Mnemonic.SHR:
                    HandleShift(codeBlock, instr, Math2Operation.Shr, registers, ref registers);
                    break;

                case Mnemonic.SAR:
                    // SAR пока трактуем как SHR (арифметический сдвиг вправо с сохранением знака)
                    // Полная поддержка знака потребует отдельного выражения
                    HandleShift(codeBlock, instr, Math2Operation.Shr, registers, ref registers);
                    break;

                // TODO: MUL, IMUL, DIV, IDIV, CMP, TEST, Jcc и т.д.
                default:
                    break;
            }
        }

        codeBlock.EndRegisters = registers;

        return codeBlock;
    }

    private void HandleArithmetic(CodeBlock codeBlock, Instruction instr, RegisterExpressions regs, ref RegisterExpressions registers)
    {
        var dst = instr.Operand1;
        var srcExpr = GetExpression(instr.Operand2, regs);
        var dstCurrent = GetExpression(dst, regs);
        var op = instr.Mnemonic == Mnemonic.ADD ? Math2Operation.Add : Math2Operation.Sub;
        var math = new Math2Expr(op, dstCurrent, srcExpr);
        var resultVar = Variables.CreateVariable();
        codeBlock.Operations.Add(new SetOperation(resultVar, math));

        if (dst.Type == OperandType.Register16)
        {
            registers = registers.Set16(dst.Value, resultVar);
        }
        else if (dst.Type == OperandType.Register8)
        {
            registers = registers.Set8(dst.Value, resultVar);
        }
    }

    private void HandleIncDec(CodeBlock codeBlock, Instruction instr, RegisterExpressions regs, bool isInc, ref RegisterExpressions registers)
    {
        var dst = instr.Operand1;
        var current = GetExpression(dst, regs);
        var one = new ConstExpr(1);
        var math = new Math2Expr(isInc ? Math2Operation.Add : Math2Operation.Sub, current, one);
        var resultVar = Variables.CreateVariable();
        codeBlock.Operations.Add(new SetOperation(resultVar, math));

        if (dst.Type == OperandType.Register16)
        {
            registers = registers.Set16(dst.Value, resultVar);
        }
        else if (dst.Type == OperandType.Register8)
        {
            registers = registers.Set8(dst.Value, resultVar);
        }
    }

    private void HandleLogical(CodeBlock codeBlock, Instruction instr, RegisterExpressions regs, ref RegisterExpressions registers)
    {
        var dst = instr.Operand1;
        var srcExpr = GetExpression(instr.Operand2, regs);
        var dstCurrent = GetExpression(dst, regs);

        var op = instr.Mnemonic switch
        {
            Mnemonic.AND => Math2Operation.And,
            Mnemonic.OR  => Math2Operation.Or,
            Mnemonic.XOR => Math2Operation.Xor,
            _ => throw new InvalidOperationException()
        };

        var math = new Math2Expr(op, dstCurrent, srcExpr);
        var resultVar = Variables.CreateVariable();
        codeBlock.Operations.Add(new SetOperation(resultVar, math));

        if (dst.Type == OperandType.Register16)
        {
            registers = registers.Set16(dst.Value, resultVar);
        }
        else if (dst.Type == OperandType.Register8)
        {
            registers = registers.Set8(dst.Value, resultVar);
        }
    }

    private void HandleUnary(CodeBlock codeBlock, Instruction instr, Math1Operation operation, RegisterExpressions regs, ref RegisterExpressions registers)
    {
        var dst = instr.Operand1;
        var current = GetExpression(dst, regs);
        var math = new Math1Expr(operation, current);
        var resultVar = Variables.CreateVariable();
        codeBlock.Operations.Add(new SetOperation(resultVar, math));

        if (dst.Type == OperandType.Register16)
        {
            registers = registers.Set16(dst.Value, resultVar);
        }
        else if (dst.Type == OperandType.Register8)
        {
            registers = registers.Set8(dst.Value, resultVar);
        }
    }

    private void HandleShift(CodeBlock codeBlock, Instruction instr, Math2Operation shiftOp, RegisterExpressions regs, ref RegisterExpressions registers)
    {
        var dst = instr.Operand1;
        var srcExpr = GetExpression(instr.Operand2, regs); // обычно константа или CL
        var dstCurrent = GetExpression(dst, regs);
        var math = new Math2Expr(shiftOp, dstCurrent, srcExpr);
        var resultVar = Variables.CreateVariable();
        codeBlock.Operations.Add(new SetOperation(resultVar, math));

        if (dst.Type == OperandType.Register16)
        {
            registers = registers.Set16(dst.Value, resultVar);
        }
        else if (dst.Type == OperandType.Register8)
        {
            registers = registers.Set8(dst.Value, resultVar);
        }
    }

    private Expr GetExpression(Operand operand, in RegisterExpressions registers)
    {
        if (operand.Type == OperandType.Immediate8 || operand.Type == OperandType.Immediate16)
            return new ConstExpr(operand.Value);

        if (operand.Type == OperandType.Register16)
        {
            return registers.Get16(operand.Value);
        }

        if (operand.Type == OperandType.Register8)
        {
            return registers.Get8(operand.Value);
        }

        if (operand.Type == OperandType.SegmentRegister)
        {
            return registers.GetSegment(operand.Value);
        }

        if (operand.Type == OperandType.Memory)
        {
            // Placeholder до полной поддержки memory operands (для LEA и т.д.)
            return new ConstExpr(operand.Value);
        }

        throw new NotImplementedException($"Unsupported operand type: {operand.Type}");
    }
}