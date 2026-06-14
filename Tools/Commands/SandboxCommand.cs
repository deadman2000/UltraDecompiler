using McMaster.Extensions.CommandLineUtils;

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
        var image = HexConverter.FromHexString("11 56 FC"); // adc word ptr [bp - 4], dx

        var disassembler = new X86Disassembler(image);

        var cfg = new ControlFlowGraph();
        cfg.Build(disassembler, 0, RegisterState.Unknown);

        var expressions = new ExpressionBuilder();
        RegisterExpressions registers = RegisterExpressions.InitZero() with { BP = new Variable(0) { Name = "x" } };
        expressions.Build(cfg, registers, []);

        Console.WriteLine();

        return 0;
    }
}
