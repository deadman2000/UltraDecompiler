using UltraDecompiler.Ir.Helpers;

namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты CBW и CWD: знаковое расширение AL→AX и AX→DX.
/// </summary>
public class CbwCwdTests : BaseTests
{
    [Fact]
    public void Cbw_ProducesSetOperationOnAx()
    {
        var expr = BuildExpressionsRaw("98");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);
        ExprTestHelpers.AssertReferencesVariable(s.Dst, expr.Variables.AX);
    }

    [Fact]
    public void Cbw_UsesSignExtensionFormula()
    {
        var expr = BuildExpressionsRaw("98");

        var set = Assert.IsType<SetOperation>(Assert.Single(expr.Blocks[0].Operations));
        var subExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Sub, subExpr.Operation);
        Assert.Equal(0x80, ((ConstExpr)subExpr.Second).Value);

        var xorExpr = Assert.IsType<Math2Expr>(subExpr.First);
        Assert.Equal(Math2Operation.Xor, xorExpr.Operation);
        Assert.Equal(0x80, ((ConstExpr)xorExpr.Second).Value);
    }

    [Theory]
    [InlineData(0x7F, 0x007F)]
    [InlineData(0x80, unchecked((short)0xFF80))]
    [InlineData(0xFF, unchecked((short)0xFFFF))]
    public void Cbw_SignExtensionFormula(int al, int expectedAx)
    {
        var ax = new ConstExpr(al)
            .Calculate(Math2Operation.Xor, new ConstExpr(0x80))
            .Calculate(Math2Operation.Sub, new ConstExpr(0x80));

        var constExpr = Assert.IsType<ConstExpr>(ax);
        Assert.Equal(expectedAx, (short)(constExpr.Value & 0xFFFF));
    }

    [Fact]
    public void Cwd_ProducesSetOperationOnDx()
    {
        var expr = BuildExpressionsRaw("99");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);
        ExprTestHelpers.AssertReferencesVariable(s.Dst, expr.Variables.DX);
    }

    [Fact]
    public void Cwd_UsesSignExtensionFormula()
    {
        var expr = BuildExpressionsRaw("99");

        var set = Assert.IsType<SetOperation>(Assert.Single(expr.Blocks[0].Operations));
        var shrExpr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Shr, shrExpr.Operation);
        Assert.Equal(16, ((ConstExpr)shrExpr.Second).Value);

        var subExpr = Assert.IsType<Math2Expr>(shrExpr.First);
        Assert.Equal(Math2Operation.Sub, subExpr.Operation);
        Assert.Equal(0x8000, ((ConstExpr)subExpr.Second).Value);

        var xorExpr = Assert.IsType<Math2Expr>(subExpr.First);
        Assert.Equal(Math2Operation.Xor, xorExpr.Operation);
        Assert.Equal(0x8000, ((ConstExpr)xorExpr.Second).Value);
    }

    [Theory]
    [InlineData(0x7FFF, 0)]
    [InlineData(0x8000, unchecked((short)0xFFFF))]
    [InlineData(0xFFFF, unchecked((short)0xFFFF))]
    public void Cwd_SignExtensionFormula(int ax, int expectedDx)
    {
        var dx = new ConstExpr(ax)
            .Calculate(Math2Operation.Xor, new ConstExpr(0x8000))
            .Calculate(Math2Operation.Sub, new ConstExpr(0x8000))
            .Calculate(Math2Operation.Shr, new ConstExpr(16));

        var constExpr = Assert.IsType<ConstExpr>(dx);
        Assert.Equal(expectedDx, (short)(constExpr.Value & 0xFFFF));
    }
}
