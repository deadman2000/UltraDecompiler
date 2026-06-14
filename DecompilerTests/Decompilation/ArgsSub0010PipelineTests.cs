using TestSupport;
using UltraDecompiler.PostProcessing.Epilogue;
using UltraDecompiler.PostProcessing.Normalization;
using UltraDecompiler.PostProcessing.Stack;
using Operation = UltraDecompiler.Ir.Operations.Operation;

namespace DecompilerTests.Decompilation;

/// <summary>Декомпиляция <c>sub_0010</c> из <c>args.c</c> после постпроцессинга.</summary>
public sealed class ArgsSub0010PipelineTests
{
    [Fact]
    public void Sub0010_AfterPostProcessing_IsCompactPredicate()
    {
        var procedure = LoadSub0010Procedure();
        var operations = procedure.Expressions!.GetAllOperations().ToList();
        operations = StackCheckDetector.RemoveChkstkCalls(operations).ToList();
        operations = OperationOptimizer.Optimize(operations).ToList();
        operations = IfElseReturnFlattener.Flatten(operations).ToList();
        operations = VoidReturnNormalizer.Normalize(procedure, operations).ToList();
        operations = PointerCompareSimplifier.Simplify(operations).ToList();
        operations = IfEarlyReturnInverter.Invert(operations).ToList();
        operations = ReturnBranchSwapper.Swap(operations).ToList();
        operations = BooleanPredicateReturnNormalizer.Normalize(procedure, operations).ToList();
        var text = Dump(operations);

        Assert.Contains("return", text);
        Assert.Contains("*arg0 == '-'", text);
        Assert.Contains("arg0[1] == arg1", text);
        Assert.Contains("arg0[2] == 0", text);
    }

    private static string Dump(IReadOnlyList<Operation> operations) =>
        string.Join('\n', operations.Select(static op => op.ToCString().Trim()));

    private static DisassembledProcedure LoadSub0010Procedure()
    {
        var exePath = ExeProvider.Get("args.c", libraries: ["SLIBCE.LIB"]);
        var outputDir = Path.Combine(Path.GetTempPath(), "udc_sub10_" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                exePath,
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDir);
            return result.Procedures.All.First(static p => p.Name == "sub_0010");
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

}
