using System.Diagnostics;
using System.Text;
using UltraDecompiler.CodeGeneration.Rendering;
using UltraDecompiler.Compilation;
using UltraDecompiler.PostProcessing.Abstractions;
using UltraDecompiler.PostProcessing.Profiles;
using UltraDecompiler.PostProcessing.Stack;

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
        var profile = DecompilationProfileRegistry.GetProfile(compilerOptions.OptimizationLevel);
        var diagnosticCtx = new PostProcessContext
        {
            Procedure = new DisassembledProcedure { Offset = startOffset, Instructions = [], Name = "diagnostic" },
            Storage = new ProcedureStorage(),
            HeaderCatalog = HeaderCatalog.Empty,
            Image = parser.Image,
        };

        foreach (var pass in profile.GetDiagnosticPasses())
        {
            operations = pass.Apply(diagnosticCtx, operations);
        }

        Console.WriteLine(compilerOptions);
        Console.WriteLine();

        foreach (var op in operations)
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
