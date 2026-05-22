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
    disassembler.DataSegmentBase = parser.DataSegmentBase;
    disassembler.Disassemble((int)parser.EntryPointOffset);

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
    var cfg = ControlFlowGraph.Build(disassembler.Instructions);
    cfg.Print();

    // === Шаг 2: Function Detection ===
    var functions = FunctionDetector.DetectFunctions(disassembler.Instructions, cfg);
    FunctionDetector.PrintFunctions(functions);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}