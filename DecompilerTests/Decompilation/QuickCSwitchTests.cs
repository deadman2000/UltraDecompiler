using TestSupport;
using UltraDecompiler.Ir.Switch;

namespace DecompilerTests.Decompilation;

/// <summary>Распознавание switch QuickC по ассемблерному шаблону cmp REG, imm / jne / jmp.</summary>
public sealed class QuickCSwitchTests : BaseTests
{
    // QuickC/PROGRAMS/switch.c: ручные if (cmp [mem]) + настоящий switch (cmp AX + обратные jmp).
    // Ожидаем: один SwitchOperation, ручная if-цепочка остаётся IfOperation.
    [Fact]
    public void GetAllOperations_SwitchExe_RecognizesOnlyQuickCSwitchPattern()
    {
        var builtExePath = ExeProvider.Get("switch.c", libraries: ["SLIBCE.LIB"]);
        var parser = new UltraDecompiler.Disassembly.Parser.DosExeParser(builtExePath);
        var provider = new UltraDecompiler.LibMatching.LibraryProvider(
            QuickCTestAssets.LibDirectory,
            ["SLIBCE.LIB"]);
        var initRegisters = parser.IsCom ? RegisterState.InitCom : RegisterState.InitExe;

        Assert.True(
            provider.TryResolveMain(
                parser.Image,
                parser.RelocationTable,
                initRegisters,
                (int)parser.EntryPointOffset,
                out var resolution));

        var disassembler = new X86Disassembler(parser.Image, parser.RelocationTable);
        disassembler.Disassemble(resolution.MainOffset, initRegisters);
        var cfg = new ControlFlowGraph();
        cfg.Build(disassembler, resolution.MainOffset, initRegisters);

        var patterns = QuickCSwitchDetector.Detect(cfg.Blocks);
        Assert.Single(patterns);
        Assert.Equal(0xA7, patterns[0].DispatcherStart);
        Assert.Equal(0x6A, patterns[0].EntryOffset);
        Assert.Equal(0xC2, patterns[0].MergeOffset);

        var expressions = new ExpressionBuilder();
        expressions.Build(cfg, parser.IsCom);

        var operations = expressions.GetAllOperations();
        var switchOps = operations.OfType<SwitchOperation>().ToList();
        Assert.Single(switchOps);

        var switchOp = switchOps[0];
        Assert.Equal(4, switchOp.Cases.Count);
        Assert.Equal(1, switchOp.Cases[0].Value!.Value);
        Assert.Equal(2, switchOp.Cases[1].Value!.Value);
        Assert.Equal(3, switchOp.Cases[2].Value!.Value);
        Assert.Null(switchOp.Cases[3].Value);

        var source = switchOp.ToCString(asStatement: true);
        Assert.Contains("switch (var1)", source);
        Assert.Contains("case 1:", source);
        Assert.Contains("default:", source);

        Assert.Contains(operations, static op => op is IfOperation);
    }

    // cmp [mem], imm + прямой jmp в case — не switch QuickC.
    [Fact]
    public void Detect_MemoryCompareChain_IsNotQuickCSwitch()
    {
        var hex = """
            55
            8B EC
            81 EC 02 00
            C7 46 FE 03 00
            83 7E FE 01
            75 03
            E9 10 00
            83 7E FE 02
            75 03
            E9 07 00
            B8 00 00
            C3
            """;

        var graph = GetGraph(hex);

        Assert.Empty(QuickCSwitchDetector.Detect(graph.Blocks));
        Assert.DoesNotContain(
            BuildExpressions(hex).GetAllOperations(),
            static op => op is SwitchOperation);
    }
}
