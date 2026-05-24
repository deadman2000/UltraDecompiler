using UltraDecompiler.Disassembler;
using UltraDecompiler.Graph;
using UltraDecompiler.Parser;

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
    var cfg = new ControlFlowGraph();
    cfg.Build(disassembler, (int)parser.EntryPointOffset);

    cfg.SaveDot(Path.Combine(Path.GetDirectoryName(exePath) ?? ".", "cfg.dot"));
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}