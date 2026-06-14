using Common;

namespace DecompilerTests.Expressions;

/// <summary>Символические смещения из таблицы релокаций → ImageOffsetExpr.</summary>
public class RelocationExpressionTests : BaseTests
{
    // MOV DI, 0166h с rel16 «image» → ImageOffsetExpr("image", 0x166)
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
