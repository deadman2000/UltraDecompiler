using UltraDecompiler;

if (args.Length == 0)
{
    Console.WriteLine("Usage: UltraDecompiler <path_to_dos_exe>");
    Console.WriteLine("Example: UltraDecompiler.exe game.exe");
    return;
}

string exePath = args[0];

try
{
    var parser = new DosExeParser(exePath);
    parser.PrintInfo();

    Console.WriteLine("\n=== Disassembly from entry point ===");

    var disassembler = new X86Disassembler(parser.Image);
    var instructions = disassembler.Disassemble((int)parser.EntryPointOffset, maxInstructions: 150);

    foreach (var instr in instructions)
    {
        Console.WriteLine(instr.ToColoredString());
    }

    // === Шаг 1: Control Flow Graph ===
    Console.WriteLine("\n=== Control Flow Graph ===");
    var cfg = ControlFlowGraph.Build(instructions);
    cfg.Print();

    // === Шаг 2: Function Detection ===
    var functions = FunctionDetector.DetectFunctions(instructions, cfg);
    FunctionDetector.PrintFunctions(functions);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}