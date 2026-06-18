namespace DecompilerTests.Expressions;

/// <summary>do-while (bottom-tested) цикл QuickC /Od — эталон dowhl.c (main @ 0x10).</summary>
public class DoWhileRecognitionTests : BaseTests
{
    // Байты main из DOWHL.EXE /Od (смещения 0x10–0x4E).
    [Fact]
    public void DoWhile_ProducesForLoop()
    {
        var builder = BuildProcExpressions("""
            55 8B EC 81 EC 04 00 57 56
            C7 46 FE 03 00 C7 46 FC 00 00
            8B 46 FE 01 46 FC 83 6E FE 01
            83 7E FE 00 7E 03 E9 ED FF
            FF 76 FC B8 6A 02 50 E8 00 00
            83 C4 04 B8 00 00 E9 00 00
            5E 5F 8B E5 5D C3
            """);

        var ops = builder.GetAllOperations();
        var loop = Assert.Single(ops.OfType<ForOperation>());
        Assert.IsType<SetOperation>(loop.Init);
        Assert.IsType<DecOperation>(loop.Iteration);
        Assert.Contains(loop.Body, op => op is SetOperation);
    }
}