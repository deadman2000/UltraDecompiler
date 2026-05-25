namespace Tests;

public class GraphTests : BaseTests
{
    [Fact]
    public void TwoBlocks()
    {
        var graph = GetGraph("""
            B8 01 00 ; mov ax, 1
            EB 01    ; jmp short +1
            90       ; nop (в следующем блоке)
            """);

        Assert.Equal(2, graph.Blocks.Count);
    }
}
