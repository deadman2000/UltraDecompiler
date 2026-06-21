using McMaster.Extensions.CommandLineUtils;
using UltraDecompiler.Common;

namespace Tools.Commands;

internal static class SandboxCommand
{
    public static void Configure(CommandLineApplication root)
    {
        root.Command("sandbox", cmd =>
        {
            cmd.OnExecute(() =>
            {
                return Execute();
            });
        });
    }

    private static int Execute()
    {
        var image = HexConverter.FromHexString("B8 05 00");

        var disassembler = new X86Disassembler(image);

        var cfg = new ControlFlowGraph();
        cfg.Build(disassembler, 0, RegisterState.Unknown);

        var expressions = ExpressionBuilder.Create(cfg, OptimizationLevel.Disabled);
        expressions.Build();

        Console.WriteLine();

        return 0;
    }
}
