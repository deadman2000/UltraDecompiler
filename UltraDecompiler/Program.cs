using System.Diagnostics;
using UltraDecompiler.Decompilation;
using UltraDecompiler.Graph;
using UltraDecompiler.Parser;

if (args.Length == 0)
{
    Console.WriteLine("Usage: UltraDecompiler <path_to_dos_exe>");
    Console.WriteLine("Example: UltraDecompiler.exe game.exe");
    return;
}

string exePath = args[0];

void ConvertDotToSvg(string dotPath, string svgPath)
{
    var proc = new Process();
    proc.StartInfo = new ProcessStartInfo("dot", $"-Tsvg {dotPath} -o {svgPath}");
    proc.Start();
    proc.WaitForExit();
}

try
{
    var parser = new DosExeParser(exePath);
    parser.PrintInfo();

    // Выбираем правильное начальное состояние регистров
    var initRegisterState = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;

    Console.WriteLine("\n=== Disassembly from entry point ===");

    var disassembler = new X86Disassembler(parser.Image);
    disassembler.Disassemble((int)parser.EntryPointOffset, initRegisterState);

    int next = 0;
    foreach (var instr in disassembler.Instructions)
    {
        if (instr.Offset < next)
        {
            Console.WriteLine($"Wrong instruction: {instr}");
            throw new Exception();
        }

        Console.WriteLine(instr.ToColoredString());
        next = instr.Offset + instr.Bytes.Length;
    }

    // === Шаг 1: Control Flow Graph ===
    Console.WriteLine("\n=== Control Flow Graph ===");
    var cfg = new ControlFlowGraph();
    cfg.Build(disassembler, (int)parser.EntryPointOffset, initRegisterState);

    var outputDir = Path.GetDirectoryName(exePath) ?? ".";

    var cfgDotPath = Path.Combine(outputDir, "asm.dot");
    var cfgSvgPath = Path.Combine(outputDir, "asm.svg");
    cfg.SaveDot(cfgDotPath);
    ConvertDotToSvg(cfgDotPath, cfgSvgPath);
    Console.WriteLine($"CFG: {cfgDotPath}, {cfgSvgPath}");

    var expressions = new ExpressionBuilder();
    expressions.Build(cfg, parser.IsCom);

    var exprDotPath = Path.Combine(outputDir, "expr.dot");
    var exprSvgPath = Path.Combine(outputDir, "expr.svg");
    expressions.SaveDot(exprDotPath);
    ConvertDotToSvg(exprDotPath, exprSvgPath);
    Console.WriteLine($"Expressions: {exprDotPath}, {exprSvgPath}");

    var operations = expressions.GetAllOperations();
    Console.WriteLine();
    foreach (var op in operations)
    {
        Console.WriteLine(op.ToCString(asStatement: true));
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}