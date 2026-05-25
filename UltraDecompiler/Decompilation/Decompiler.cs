using UltraDecompiler.Disassembler;
using UltraDecompiler.Graph;

namespace UltraDecompiler.Decompilation;

public class Decompiler
{
    public List<CodeBlock> Blocks { get; } = [];

    public VariableStorage Variables { get; } = new();

    public void Decompile(ControlFlowGraph graph)
    {
        Blocks.Clear();
        Variables.Clear();

        var registers = RegisterExpressions.InitZero();

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
                    // TODO: поддержка 8-битных регистров, памяти, сегментных
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

                // TODO: MUL, IMUL, DIV, IDIV, AND, OR, XOR, CMP, Jcc и т.д. по 8086 instruction set
                default:
                    break;
            }
        }

        codeBlock.EndRegisters = registers;

        return codeBlock;
    }

    private RegisterExpressions UpdateRegister(RegisterExpressions regs, int regValue, Expr expr)
    {
        return regValue switch
        {
            0 => regs with { AX = expr }, // AX
            1 => regs with { CX = expr }, // CX
            2 => regs with { DX = expr }, // DX
            3 => regs with { BX = expr }, // BX
            // 4=SP, 5=BP, 6=SI, 7=DI
            _ => regs
        };
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

        throw new NotImplementedException($"Unsupported operand type: {operand.Type}");
    }
}