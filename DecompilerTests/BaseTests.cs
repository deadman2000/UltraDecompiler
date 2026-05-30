using UltraDecompiler.Decompilation;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Graph;

namespace DecompilerTests;

public abstract class BaseTests
{
    protected static List<Instruction> Disassemble(string hex)
    {
        var disassembler = new X86Disassembler(hex.FromHex());
        disassembler.Disassemble(0, RegisterState.Unknown);
        return disassembler.Instructions;
    }

    protected static ControlFlowGraph GetGraph(string hex)
    {
        var disassembler = new X86Disassembler(hex.FromHex());
        disassembler.Disassemble(0, RegisterState.Unknown);

        var graph = new ControlFlowGraph();
        graph.Build(disassembler, 0);
        return graph;
    }

    protected static ExpressionBuilder BuildExpressions(string hex)
    {
        return BuildExpressions(hex, isCom: false);
    }

    protected static ExpressionBuilder BuildExpressions(string hex, bool isCom)
    {
        var graph = GetGraph(hex);
        var decompiler = new ExpressionBuilder();
        decompiler.Build(graph, isCom);
        return decompiler;
    }

    /// <summary>
    /// Строит выражения с явно заданным начальным состоянием регистров.
    /// Удобно для тестов, где нужно проверить работу с символическими переменными.
    /// </summary>
    protected static ExpressionBuilder BuildExpressions(string hex, RegisterExpressions initialRegisters)
    {
        var graph = GetGraph(hex);
        var decompiler = new ExpressionBuilder();
        decompiler.Build(graph, initialRegisters, []);
        return decompiler;
    }

    /// <summary>
    /// Самый удобный вариант для тестов с переменными:
    /// позволяет создать переменные через хранилище билдера и сразу собрать начальные регистры.
    /// </summary>
    protected static ExpressionBuilder BuildExpressions(string hex, Func<VariableStorage, RegisterExpressions> configureInitial)
    {
        var graph = GetGraph(hex);
        var decompiler = new ExpressionBuilder();
        var initial = configureInitial(decompiler.Variables);
        decompiler.Build(graph, initial, []);
        return decompiler;
    }

    /// <summary>
    /// Вариант для тестов со стеком: позволяет задать и регистры, и начальное содержимое символического стека.
    /// </summary>
    protected static ExpressionBuilder BuildExpressions(string hex, RegisterExpressions initialRegisters, Stack<Expr> initialStack)
    {
        var graph = GetGraph(hex);
        var decompiler = new ExpressionBuilder();
        decompiler.Build(graph, initialRegisters, initialStack);
        return decompiler;
    }

    /// <summary>
    /// Самый удобный вариант для тестов со стеком + переменными.
    /// </summary>
    protected static ExpressionBuilder BuildExpressions(string hex, Func<VariableStorage, (RegisterExpressions regs, Stack<Expr> stack)> configure)
    {
        var graph = GetGraph(hex);
        var decompiler = new ExpressionBuilder();
        var (regs, stack) = configure(decompiler.Variables);
        decompiler.Build(graph, regs, stack);
        return decompiler;
    }
}
