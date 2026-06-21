using UltraDecompiler.Compilation;
using UltraDecompiler.Ir.Builder.Loops;

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

    /// <summary>
    /// Создаёт OperationFlattener с анализатором циклов по умолчанию (/Od).
    /// </summary>
    protected static OperationFlattener CreateFlattener(ExpressionBuilder builder, ControlFlowGraph? cfg = null)
    {
        var loopAnalyzer = LoopAnalyzerFactory.Create(OptimizationLevel.Disabled);
        var cfgBlocks = cfg is not null ? (IReadOnlyList<BasicBlock>)cfg.Blocks : Array.Empty<BasicBlock>();
        return new OperationFlattener(builder, cfgBlocks, loopAnalyzer);
    }

    /// <summary>
    /// Создаёт OperationFlattener с анализатором циклов для заданного уровня оптимизации.
    /// </summary>
    protected static OperationFlattener CreateFlattener(ExpressionBuilder builder, ControlFlowGraph? cfg, OptimizationLevel optimization)
    {
        var loopAnalyzer = LoopAnalyzerFactory.Create(optimization);
        var cfgBlocks = cfg is not null ? (IReadOnlyList<BasicBlock>)cfg.Blocks : Array.Empty<BasicBlock>();
        return new OperationFlattener(builder, cfgBlocks, loopAnalyzer);
    }

    protected static ExpressionBuilder BuildExpressionsRaw(string hex)
    {
        var graph = GetGraph(hex);
        var builder = ExpressionBuilder.Create(graph, OptimizationLevel.Disabled);
        builder.Build();
        return builder;
    }

    /// <summary>Строит IR процедуры с <see cref="ExpressionBuilder.BuildProc"/> и IR-construction pass-ами профиля.</summary>
    protected static ExpressionBuilder BuildExpressions(string hex)
    {
        var graph = GetGraph(hex);
        var builder = ExpressionBuilder.Create(graph, OptimizationLevel.Disabled);
        builder.Build();
        builder.Optimize();
        return builder;
    }

    /// <summary>Строит IR процедуры с <see cref="ExpressionBuilderQuickCOpt"/> (/Ox).</summary>
    protected static ExpressionBuilder BuildExpressionsOpt(string hex)
    {
        var graph = GetGraph(hex);
        var builder = ExpressionBuilder.Create(graph, OptimizationLevel.EnabledFull);
        builder.Build();
        builder.Optimize();
        return builder;
    }

    /// <summary>
    /// Строит IR и получает плоский список операций с анализатором /Od.
    /// </summary>
    protected static IReadOnlyList<Operation> BuildOperationsRaw(string hex)
    {
        var cfg = GetGraph(hex);
        var builder = BuildExpressionsRaw(hex);
        return CreateFlattener(builder, cfg).GetAllOperations();
    }

    /// <summary>
    /// Строит IR процедуры и получает плоский список операций с анализатором /Od.
    /// </summary>
    protected static IReadOnlyList<Operation> BuildOperations(string hex)
    {
        var cfg = GetGraph(hex);
        var builder = BuildExpressions(hex);
        return CreateFlattener(builder, cfg).GetAllOperations();
    }

    /// <summary>
    /// Строит IR процедуры с /Ox и получает плоский список операций.
    /// </summary>
    protected static IReadOnlyList<Operation> BuildOperationsOpt(string hex)
    {
        var graph = GetGraph(hex);
        var builder = ExpressionBuilder.Create(graph, OptimizationLevel.EnabledFull);
        builder.Build();

        return CreateFlattener(builder, graph, OptimizationLevel.EnabledFull).GetAllOperations();
    }
}
