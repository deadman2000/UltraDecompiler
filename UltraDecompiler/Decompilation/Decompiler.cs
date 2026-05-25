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

        while (queue.Count > 0)
        {
            var block = queue.Dequeue();

            if (blocksMap.ContainsKey(block))
                continue;

            var codeBlock = GenerateCode(block, registers);
            Blocks.Add(codeBlock);
            blocksMap[block] = codeBlock;
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
            if (instr.Mnemonic == Mnemonic.MOV)
            {
                var exprSrc = GetExpression(instr.Operand2, registers);

                if (instr.Operand1.Type == OperandType.Register16)
                {
                    if (instr.Operand1.Value == 0)
                        registers = registers with { AX = exprSrc };
                    else if (instr.Operand1.Value == 3)
                        registers = registers with { BX = exprSrc };
                }

                var variable = Variables.CreateVariable();

                codeBlock.Operations.Add(new SetOperation(variable, exprSrc));
            }

            if (instr.Mnemonic == Mnemonic.ADD)
            {
                var first = GetExpression(instr.Operand1, registers);
                var second = GetExpression(instr.Operand2, registers);
                var add = new Math2Expr(Math2Operation.Add, first, second);

                var result = Variables.CreateVariable();
                codeBlock.Operations.Add(new SetOperation(result, add));
                registers = registers with { AX = result };
            }
        }

        codeBlock.EndRegisters = registers;

        return codeBlock;
    }

    private Expr GetExpression(Operand operand, in RegisterExpressions registers)
    {
        if (operand.Type == OperandType.Immediate8 || operand.Type == OperandType.Immediate16)
            return new ConstExpr(operand.Value);

        if (operand.Type == OperandType.Register16)
        {
            if (operand.Value == 0)
                return registers.AX;
            if (operand.Value == 3)
                return registers.BX;
        }

        throw new NotImplementedException();
    }
}
