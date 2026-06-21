namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты INC/DEC в IR: регистры, локалы и память.
/// </summary>
public class IncDecTests : BaseTests
{
    // inc ax → IncOperation(regAX)
    [Fact]
    public void Inc_Register16_ProducesIncOperation()
    {
        var expr = BuildExpressionsRaw("40");

        var inc = Assert.IsType<IncOperation>(Assert.Single(expr.Blocks[0].Operations, op => op is IncOperation));
        Assert.IsType<VariableExpr>(inc.Target);
    }

    // dec bx → DecOperation(regBX)
    [Fact]
    public void Dec_Register16_ProducesDecOperation()
    {
        var expr = BuildExpressionsRaw("4B");

        Assert.IsType<DecOperation>(Assert.Single(expr.Blocks[0].Operations, op => op is DecOperation));
    }

    // inc word [bp-2] → var1++
    [Fact]
    public void Inc_StackLocal_ProducesIncOperation()
    {
        var expr = BuildExpressions("""
            55           ; push bp
            8B EC        ; mov bp, sp
            FF 46 FE     ; inc word [bp-2]
            """);

        var inc = Assert.IsType<IncOperation>(Assert.Single(expr.Blocks[0].Operations, op => op is IncOperation));
        Assert.True(AssignmentTarget.TryGetVariable(inc.Target, out var target));
        Assert.True(target!.IsStack);
    }

    // dec word [bp-2] → var1--
    [Fact]
    public void Dec_StackLocal_ProducesDecOperation()
    {
        var expr = BuildExpressions("""
            55           ; push bp
            8B EC        ; mov bp, sp
            FF 4E FE     ; dec word [bp-2]
            """);

        Assert.IsType<DecOperation>(Assert.Single(expr.Blocks[0].Operations, op => op is DecOperation));
    }
}
