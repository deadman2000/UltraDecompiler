namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты генерации строк
/// </summary>
public class FormattingTests : BaseTests
{
    [Fact]
    public void ToString_AddInsideBitAnd_NeedsParens()
    {
        // (a + b) & c   — Add имеет более высокий приоритет, чем &, без скобок строка "a + b & c" неверно сгруппируется (в C + сильнее &)
        var a = new Variable(1) { Name = "a" };
        var b = new Variable(2) { Name = "b" };
        var c = new Variable(3) { Name = "c" };

        var add = new Math2Expr(Math2Operation.Add, a, b);
        var andExpr = new Math2Expr(Math2Operation.And, add, c);
        Assert.Equal("(a + b) & c", andExpr.ToString());
    }

    [Fact]
    public void ToString_AndInsideCmp_NeedsParens()
    {
        // (var & mask) >= val
        var v = new Variable(4) { Name = "var4" };
        var mask = new ConstExpr(255);
        var and = new Math2Expr(Math2Operation.And, v, mask);
        var cmp = new CmpExpr(CmpOperation.Uge, and, new ConstExpr(2));
        Assert.Equal("(var4 & 255) >= 2", cmp.ToString());
    }

    [Fact]
    public void ToString_ShiftInsideAdd_NeedsParens()
    {
        // (x << 2) + 1
        var x = new Variable(0) { Name = "x" };
        var shl = new Math2Expr(Math2Operation.Shl, x, new ConstExpr(2));
        var add = new Math2Expr(Math2Operation.Add, shl, new ConstExpr(1));
        Assert.Equal("(x << 2) + 1", add.ToString());
    }

    [Fact]
    public void ToString_ConservativeParensOnPrecChange()
    {
        // При смешении операторов разных приоритетов (Add под Cmp) мы консервативно ставим скобки,
        // чтобы гарантировать правильную группировку при любом сочетании (простота + корректность).
        // В C "a + b == c" и так сгруппируется верно, но мы выводим "(a + b) == c".
        var a = new Variable(1) { Name = "a" };
        var b = new Variable(2) { Name = "b" };
        var c = new Variable(3) { Name = "c" };
        var add = new Math2Expr(Math2Operation.Add, a, b);
        var cmp = new CmpExpr(CmpOperation.Eq, add, c);
        Assert.Equal("(a + b) == c", cmp.ToString());
    }

    [Fact]
    public void ToString_BitwiseNotOnVariable_UsesTilde()
    {
        var v = new Variable(1) { Name = "x" };
        var not = new Math1Expr(Math1Operation.Not, v);
        Assert.Equal("~x", not.ToString());
    }

    [Fact]
    public void ToString_UnaryOnComplex_NeedsParens()
    {
        // !(a == 1)
        var a = new Variable(1) { Name = "a" };
        var cmp = new CmpExpr(CmpOperation.Eq, a, new ConstExpr(1));
        var not = new Math1Expr(Math1Operation.Not, cmp);
        Assert.Equal("!(a == 1)", not.ToString());
    }
}
