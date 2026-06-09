using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;

namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты на строковое представление IfOperation, WhileOperation и ForOperation.
/// </summary>
public class LoopOperationTests
{
    [Fact]
    public void IfOperation_ThenOnly_ProducesCorrectC()
    {
        var cond = new CmpExpr(CmpOperation.Eq, new Variable(1), ConstExpr.Zero);
        var thenBody = new List<Operation>
        {
            new SetOperation(new Variable(2), new Variable(3))
        };

        var ifOp = new IfOperation(cond, thenBody);
        string result = ifOp.ToCString();

        Assert.Contains("if (var1 == 0)", result);
        Assert.Contains("var2 = var3;", result);
        Assert.DoesNotContain("else", result);
    }

    [Fact]
    public void IfOperation_WithElse_ProducesCorrectC()
    {
        var cond = new CmpExpr(CmpOperation.Ne, new Variable(0) { Name = "x" }, ConstExpr.Zero);
        var thenBody = new List<Operation> { new SetOperation(new Variable(1), ConstExpr.One) };
        var elseBody = new List<Operation> { new SetOperation(new Variable(1), ConstExpr.Zero) };

        var ifOp = new IfOperation(cond, thenBody, elseBody);
        string result = ifOp.ToCString();

        Assert.Contains("if (x != 0)", result);
        Assert.Contains("else", result);
        Assert.Contains("var1 = 1;", result);
        Assert.Contains("var1 = 0;", result);
    }

    [Fact]
    public void IfOperation_EmptyThen_ProducesSemicolon()
    {
        var ifOp = new IfOperation(ConstExpr.One, Array.Empty<Operation>());
        string result = ifOp.ToCString();

        Assert.Contains("; // empty body", result);
    }

    [Fact]
    public void IfOperation_EmptyElse_OmitsElseBranch()
    {
        var thenBody = new List<Operation> { new SetOperation(new Variable(1), ConstExpr.One) };
        var ifOp = new IfOperation(ConstExpr.One, thenBody, Array.Empty<Operation>());
        string result = ifOp.ToCString();

        Assert.Contains("var1 = 1;", result);
        Assert.DoesNotContain("else", result);
        Assert.DoesNotContain("; // empty body", result);
    }

    [Fact]
    public void WhileOperation_SimpleBody_ProducesCorrectC()
    {
        var cond = new CmpExpr(CmpOperation.Ne, new Variable(1), ConstExpr.Zero);
        var body = new List<Operation>
        {
            new StoreOperation(new Variable(2), null, new Variable(3))
        };

        var loop = new WhileOperation(cond, body);
        string result = loop.ToCString();

        Assert.Contains("while (", result);
        Assert.Contains("while (var1 != 0)", result);
        Assert.Contains("[var2] = var3;", result);
    }

    [Fact]
    public void ForOperation_ClassicCounter_ProducesCorrectC()
    {
        // for (i = 0; i < 10; i++) { ... }
        var init = new SetOperation(new Variable(0) { Name = "i" }, ConstExpr.Zero);
        var cond = new CmpExpr(CmpOperation.Ult, new Variable(0) { Name = "i" }, new ConstExpr(10));
        var iter = new SetOperation(new Variable(0) { Name = "i" },
            new Math2Expr(Math2Operation.Add, new Variable(0) { Name = "i" }, ConstExpr.One));

        var body = new List<Operation>
        {
            new StoreOperation(new Variable(1), null, new Variable(2))
        };

        var loop = new ForOperation(init, cond, iter, body);
        string result = loop.ToCString(0);

        Assert.Contains("for (i = 0; i < 10; i = i + 1)", result);
        Assert.Contains("{", result);
    }

    [Fact]
    public void WhileOperation_EmptyBody_ProducesSemicolon()
    {
        var cond = ConstExpr.One;
        var loop = new WhileOperation(cond, Array.Empty<Operation>());

        string result = loop.ToCString();

        Assert.Contains("; // empty body", result);
    }

    [Fact]
    public void NestedIfInWhile_ProducesCorrectIndentation()
    {
        var innerIf = new IfOperation(
            ConstExpr.One,
            [new SetOperation(new Variable(5), new Variable(6))]);

        var loop = new WhileOperation(ConstExpr.One, [innerIf]);
        string result = loop.ToCString(0);

        Assert.Contains("    if (1)", result);
        Assert.Contains("        var5 = var6;", result);
    }

    [Fact]
    public void IfOperation_WithIndent_AllLinesIndented()
    {
        var ifOp = new IfOperation(
            ConstExpr.One,
            [new SetOperation(new Variable(1), ConstExpr.Zero)]);

        string result = ifOp.ToCString(indent: 1);
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.All(lines, line => Assert.StartsWith("    ", line));
    }

    [Fact]
    public void NestedForWhile_InnerBodyHasCorrectIndentation()
    {
        var innerCond = ConstExpr.One;
        var innerBody = new List<Operation>
        {
            new SetOperation(new Variable(5), new Variable(6))
        };
        var innerWhile = new WhileOperation(innerCond, innerBody);
        var outer = new ForOperation(null, null, null, [innerWhile]);

        string result = outer.ToCString(0);

        Assert.Contains("    while (1)", result);
        Assert.Contains("        var5 = var6;", result);
    }
}
