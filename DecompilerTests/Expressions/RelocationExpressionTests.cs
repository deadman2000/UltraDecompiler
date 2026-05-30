using DecompilerTests;
using UltraDecompiler.Decompilation;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Graph;
using UltraDecompiler.Parser;

namespace DecompilerTests.Expressions;

public class RelocationExpressionTests : BaseTests
{
    [Fact]
    public void BuildExpressions_EmitsNamedImageOffsets()
    {
        // MOV DI, 0166h — релок на смещении 1
        byte[] raw = [0xBF, 0x66, 0x01];
        var relocs = new RelocationEntry[] { new() { Offset = 1, Segment = 0, OffsetName = "image" } };
        var disassembler = new X86Disassembler(raw, new RelocationTable("", relocs));
        disassembler.Disassemble(0);

        var graph = new ControlFlowGraph();
        graph.Build(disassembler, 0);

        var builder = new ExpressionBuilder();
        builder.Build(graph, isCom: false);

        var di = builder.Blocks[0].EndRegisters.Get16(GpRegister16.DI);
        var offset = Assert.IsType<ImageOffsetExpr>(di);
        Assert.Equal("image", offset.BaseName);
        Assert.Equal(0x0166, offset.Value);
        Assert.Equal("image + 0x0166", offset.ToString());
    }
}
