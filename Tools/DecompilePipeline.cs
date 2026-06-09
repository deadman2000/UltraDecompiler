using System.Diagnostics;
using System.Text;
using UltraDecompiler.Compilation;
using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Graph;
using UltraDecompiler.Parser;
using UltraDecompiler.PostProcessing;

namespace Tools;

/// <summary>Общий пайплайн: дизассемблирование → CFG → ExpressionBuilder.</summary>
internal static class DecompilePipeline
{
    /// <summary>
    /// Дизассемблирует образ от <paramref name="startOffset"/>, строит CFG и IR,
    /// сохраняет DOT/SVG и выводит операции в консоль.
    /// </summary>
    public static int Run(DosExeParser parser, int startOffset, string? outputDir = null)
    {
        var initRegisterState = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;

        Console.WriteLine($"\n=== Disassembly from offset 0x{startOffset:X} ===");

        var disassembler = new X86Disassembler(parser.Image, parser.RelocationTable);
        disassembler.Disassemble(startOffset, initRegisterState);

        var next = 0;
        foreach (var instr in disassembler.Instructions)
        {
            if (instr.Offset < next)
            {
                Console.WriteLine($"Wrong instruction: {instr}");
                return 1;
            }

            Console.WriteLine(instr.ToColoredString());
            next = instr.Offset + instr.Bytes.Length;
        }

        Console.WriteLine("\n=== Control Flow Graph ===");
        var cfg = new ControlFlowGraph();
        cfg.Build(disassembler, startOffset, initRegisterState);

        var expressions = new ExpressionBuilder();
        expressions.Build(cfg, parser.IsCom);

        var operations = expressions.GetAllOperations();
        var compilerOptions = new CompilerOptions
        {
            StackCheckingEnabled = StackCheckDetector.AnalyzeFromOperations(operations),
        };
        var filteredOperations = OperationOptimizer.Optimize(
            StackCheckDetector.RemoveChkstkCalls(operations));

        Console.WriteLine(compilerOptions);
        Console.WriteLine();

        foreach (var op in filteredOperations)
        {
            var line = new StringBuilder();
            op.AppendToCString(line, asStatement: true);
            Console.Write(line);
        }

        if (outputDir != null)
        {
            var cfgDotPath = Path.Combine(outputDir, "asm.dot");
            var cfgSvgPath = Path.Combine(outputDir, "asm.svg");
            cfg.SaveDot(cfgDotPath);
            ConvertDotToSvg(cfgDotPath, cfgSvgPath);
            Console.WriteLine($"CFG: {cfgDotPath}, {cfgSvgPath}");

            var exprDotPath = Path.Combine(outputDir, "expr.dot");
            var exprSvgPath = Path.Combine(outputDir, "expr.svg");
            expressions.SaveDot(exprDotPath);
            ConvertDotToSvg(exprDotPath, exprSvgPath);
            Console.WriteLine($"Expressions: {exprDotPath}, {exprSvgPath}");
        }

        return 0;
    }

    private static void ConvertDotToSvg(string dotPath, string svgPath)
    {
        var proc = new Process();
        proc.StartInfo = new ProcessStartInfo("dot", $"-Tsvg \"{dotPath}\" -o \"{svgPath}\"")
        {
            UseShellExecute = false,
        };
        proc.Start();
        proc.WaitForExit();
    }
}
