using UltraDecompiler.Compilation;
using UltraDecompiler.Disassembly.Graph;
using UltraDecompiler.Ir.Builder;
using UltraDecompiler.Ir.Builder.Loops;
using UltraDecompiler.PostProcessing.Abstractions;
using UltraDecompiler.PostProcessing.Profiles;

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

    /// <summary>Строит IR процедуры с <see cref="ExpressionBuilder.BuildProc"/> и IR-construction pass-ами профиля.</summary>
    protected static ExpressionBuilder BuildProcExpressions(string hex)
    {
        var graph = GetGraph(hex);
        var decompiler = ExpressionBuilder.Create(OptimizationLevel.Disabled);
        decompiler.BuildProc(graph);

        var procedure = new DisassembledProcedure
        {
            Offset = 0,
            Instructions = [],
            Name = "test_proc",
        };
        DecompilationProfileRegistry
            .GetProfile(OptimizationLevel.Disabled)
            .ApplyIrConstructionPasses(new IrConstructionContext
            {
                Builder = decompiler,
                Graph = graph,
                Procedure = procedure,
            });

        return decompiler;
    }

    /// <summary>Строит IR процедуры с <see cref="ExpressionBuilderQuickCOpt"/> (/Ox).</summary>
    protected static ExpressionBuilder BuildProcExpressionsOpt(string hex)
    {
        var graph = GetGraph(hex);
        var decompiler = ExpressionBuilder.Create(OptimizationLevel.EnabledFull);
        decompiler.BuildProc(graph);
        return decompiler;
    }
    protected static ExpressionBuilder BuildExpressions(
        string hex,
        IReadOnlyDictionary<int, string> knownProcedures,
        bool isCom = false)
    {
        var graph = GetGraph(hex);
        var decompiler = new ExpressionBuilder();
        decompiler.Build(graph, isCom);

        ProcedureStorage procedures = new();
        foreach (var kv in knownProcedures)
        {
            procedures.Add(new DisassembledProcedure()
            {
                Instructions = [],
                Name = kv.Value,
                Offset = kv.Key
            });
        }

        CallSiteResolver.ResolveBlocks(decompiler.Blocks, procedures);
        return decompiler;
    }

    /// <summary>
    /// Вариант для тестов со стеком: позволяет задать и регистры, и начальное содержимое символического стека.
    /// </summary>
    protected static ExpressionBuilder BuildExpressions(string hex, Stack<Expr> initialStack)
    {
        var graph = GetGraph(hex);
        var decompiler = new ExpressionBuilder();
        decompiler.Build(graph, initialStack);
        return decompiler;
    }

    /// <summary>
    /// Самый удобный вариант для тестов со стеком + переменными.
    /// </summary>
    protected static ExpressionBuilder BuildExpressions(string hex, Func<VariableStorage, Stack<Expr>> configure)
    {
        var graph = GetGraph(hex);
        var decompiler = new ExpressionBuilder();
        var stack = configure(decompiler.Variables);
        decompiler.Build(graph, stack);
        return decompiler;
    }

    /// <summary>
    /// Строит IR и получает плоский список операций с анализатором /Od.
    /// </summary>
    protected static IReadOnlyList<Operation> BuildOperations(string hex)
    {
        var cfg = GetGraph(hex);
        var builder = BuildExpressions(hex);
        return CreateFlattener(builder, cfg).GetAllOperations();
    }

    /// <summary>
    /// Строит IR процедуры и получает плоский список операций с анализатором /Od.
    /// </summary>
    protected static IReadOnlyList<Operation> BuildProcOperations(string hex)
    {
        var cfg = GetGraph(hex);
        var builder = BuildProcExpressions(hex);
        return CreateFlattener(builder, cfg).GetAllOperations();
    }

    /// <summary>
    /// Строит IR процедуры с /Ox и получает плоский список операций.
    /// </summary>
    protected static IReadOnlyList<Operation> BuildProcOperationsOpt(string hex)
    {
        var cfg = GetGraph(hex);
        var builder = BuildProcExpressionsOpt(hex);
        return CreateFlattener(builder, cfg, OptimizationLevel.EnabledFull).GetAllOperations();
    }
}
