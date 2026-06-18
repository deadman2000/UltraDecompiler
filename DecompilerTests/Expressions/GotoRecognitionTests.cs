namespace DecompilerTests.Expressions;

/// <summary>
/// Распознавание goto/меток QuickC /Od — эталон QuickC/PROGRAMS/jmp.c.
/// </summary>
public class GotoRecognitionTests : BaseTests
{
    // jmp.c: goto start; start: x=42; if (x>0) goto done; x=0; done: — байты main из JMP.EXE /Od.
    [Fact]
    public void JmpMain_ProducesGotoAndLabels()
    {
        var builder = BuildProcExpressions("""
            55 8B EC 81 EC 02 00 57 56
            C7 46 FE 00 00 E9 00 00
            C7 46 FE 2A 00
            83 7E FE 00 7F 03 E9 03 00
            E9 05 00
            C7 46 FE 00 00
            FF 76 FE B8 6A 02 50 E8 00 00
            83 C4 04 B8 00 00 E9 00 00
            5E 5F 8B E5 5D C3
            """);

        var ops = builder.GetAllOperations();
        var all = ExpressionBuilder.EnumerateNested(ops).ToList();

        Assert.Equal(2, all.OfType<GotoOperation>().Count());
        Assert.Contains(all, op => op is LabelOperation);

        var ifOp = Assert.Single(ops.OfType<IfOperation>());
        Assert.IsType<GotoOperation>(Assert.Single(ifOp.ThenBody));
        Assert.Contains(ifOp.ElseBody!, op => op is SetOperation);
    }
}