using UltraDecompiler.Decompilation;

namespace Tests.Expressions;

/// <summary>
/// Тесты на строковое представление WhileOperation и ForOperation.
/// </summary>
public class LoopOperationTests
{
    [Fact]
    public void WhileOperation_SimpleBody_ProducesCorrectC()
    {
        var cond = new CmpExpr(CmpOperation.Ne, new Variable(1), ConstExpr.Zero);
        var body = new List<Operation>
        {
            new StoreOperation(new Variable(2), null, new Variable(3))
        };

        var loop = new WhileOperation(cond, body);
        string result = loop.ToString();

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
        string result = loop.ToString(0);

        Assert.Contains("for (i = 0; i < 10; i = i + 1)", result);
        Assert.Contains("{", result);
    }

    [Fact]
    public void WhileOperation_EmptyBody_ProducesSemicolon()
    {
        var cond = ConstExpr.One;
        var loop = new WhileOperation(cond, Array.Empty<Operation>());

        string result = loop.ToString();

        Assert.Contains("; // пустое тело", result);
    }

    [Fact]
    public void NestedForWhile_ProducesCorrectIndentation()
    {
        // Внешний for, внутри while
        var innerCond = ConstExpr.One;
        var innerBody = new List<Operation>
        {
            new SetOperation(new Variable(5), new Variable(6))
        };
        var innerWhile = new WhileOperation(innerCond, innerBody);

        var outerBody = new List<Operation> { innerWhile };

        var outer = new ForOperation(null, null, null, outerBody);
        string result = outer.ToString(0);

        // Проверяем, что вложенный while имеет отступ 4 пробела
        Assert.Contains("    while (1)", result);
    }
}
