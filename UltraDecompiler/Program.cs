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
    var instructions = disassembler.Disassemble((int)parser.EntryPointOffset, maxInstructions: 80);

    foreach (var instr in instructions)
    {
        Console.WriteLine(instr);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}