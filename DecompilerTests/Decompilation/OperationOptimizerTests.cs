using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;

namespace DecompilerTests.Decompilation;

public class OperationOptimizerTests
{
    [Fact]
    public void Optimize_RemovesUnusedCopyAndCallResult()
    {
        var var10 = new Variable(10);
        var var8 = new Variable(8);
        var var11 = new Variable(11);

        var operations = new List<Operation>
        {
            new SetOperation(var10, new CallExpr("sub_0010", [new ConstExpr(10), new ConstExpr(5)])),
            new SetOperation(var8, var10),
            new SetOperation(var11, new CallExpr("printf", [new StringExpr("%d"), var8])),
            new ReturnOperation(new ConstExpr(0)),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Equal(3, optimized.Count);
        Assert.IsType<SetOperation>(optimized[0]);
        Assert.IsType<CallOperation>(optimized[1]);
        Assert.IsType<ReturnOperation>(optimized[2]);

        var call = Assert.IsType<CallOperation>(optimized[1]);
        Assert.Equal("printf", call.Name);
        Assert.Equal(var10, call.Args[1]);

        var copy = Assert.IsType<SetOperation>(optimized[0]);
        Assert.Equal(var10, copy.Dst);
        Assert.IsType<CallExpr>(copy.Src);
    }

    [Fact]
    public void Optimize_KeepsCopyWhenSourceIsRedefinedBeforeUse()
    {
        var var10 = new Variable(10);
        var var8 = new Variable(8);

        var operations = new List<Operation>
        {
            new SetOperation(var10, new ConstExpr(1)),
            new SetOperation(var8, var10),
            new SetOperation(var10, new ConstExpr(2)),
            new CallOperation("use_first", [var8]),
            new CallOperation("use_second", [var10]),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Contains(optimized, op => op is SetOperation { Dst.Number: 8 });
        var firstUse = optimized.OfType<CallOperation>().First(c => c.Name == "use_first");
        Assert.Equal(var8, firstUse.Args[0]);
    }

    [Fact]
    public void Optimize_PropagatesCopyChain()
    {
        var var1 = new Variable(1);
        var var2 = new Variable(2);
        var var3 = new Variable(3);

        var operations = new List<Operation>
        {
            new SetOperation(var1, new ConstExpr(42)),
            new SetOperation(var2, var1),
            new SetOperation(var3, var2),
            new CallOperation("use", [var3]),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        var use = Assert.IsType<CallOperation>(optimized[^1]);
        Assert.Equal(var1, use.Args[0]);
        Assert.DoesNotContain(optimized, op => op is SetOperation { Dst.Number: 2 or 3 });
    }

    [Fact]
    public void Optimize_PropagatesExpressionToReturn()
    {
        var arg0 = new Variable(0);
        var arg1 = new Variable(1);
        var var11 = new Variable(11);

        var operations = new List<Operation>
        {
            new SetOperation(var11, new Math2Expr(Math2Operation.Add, arg0, arg1)),
            new ReturnOperation(var11),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Single(optimized);
        var ret = Assert.IsType<ReturnOperation>(optimized[0]);
        var sum = Assert.IsType<Math2Expr>(ret.Value);
        Assert.Equal(Math2Operation.Add, sum.Operation);
        Assert.Equal(arg0, sum.First);
        Assert.Equal(arg1, sum.Second);
    }

    [Fact]
    public void Optimize_DoesNotPropagateExpressionToReturnWhenSourceVariableIsRedefined()
    {
        var var10 = new Variable(10);
        var var11 = new Variable(11);

        var operations = new List<Operation>
        {
            new SetOperation(var11, new Math2Expr(Math2Operation.Add, var10, new ConstExpr(1))),
            new SetOperation(var10, new ConstExpr(999)),
            new ReturnOperation(var11),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Contains(optimized, op => op is SetOperation { Dst.Number: 11 });
        var ret = Assert.IsType<ReturnOperation>(optimized[^1]);
        Assert.Equal(var11, ret.Value);
    }
}
