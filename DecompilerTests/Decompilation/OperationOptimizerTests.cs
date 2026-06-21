using UltraDecompiler.PostProcessing.Normalization;

namespace DecompilerTests.Decompilation;

/// <summary>Оптимизация IR: удаление лишних копий, свёртка temp и безопасная подстановка.</summary>
public class OperationOptimizerTests
{
    // var10 = sub(10,5); var8 = var10; printf("%d", var8) → printf("%d", var10), копия var8 убирается
    [Fact]
    public void Optimize_RemovesUnusedCopyAndCallResult()
    {
        var var10 = new Variable(10);
        var var8 = new Variable(8);
        var var11 = new Variable(11);

        var operations = new List<Operation>
        {
            new SetOperation(var10.ToSet(), new CallExpr("sub_0010", [new ConstExpr(10), new ConstExpr(5)])),
            new SetOperation(var8.ToSet(), var10.ToGet()),
            new SetOperation(var11.ToSet(), new CallExpr("printf", [new StringExpr("%d"), var8.ToGet()])),
            new ReturnOperation(new ConstExpr(0)),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Equal(3, optimized.Count);
        Assert.IsType<SetOperation>(optimized[0]);
        Assert.IsType<CallOperation>(optimized[1]);
        Assert.IsType<ReturnOperation>(optimized[2]);

        var call = Assert.IsType<CallOperation>(optimized[1]);
        Assert.Equal("printf", call.Name);
        ExprTestHelpers.AssertSameVariable(var10, call.Args[1]);

        var copy = Assert.IsType<SetOperation>(optimized[0]);
        Assert.Same(var10, Assert.IsType<VariableExpr>(copy.Dst).Var);
        Assert.IsType<CallExpr>(copy.Src);
    }

    // var8 = var10; var10 = 2; use(var8) — копию нельзя убирать, var8 ещё хранит старое значение var10
    [Fact]
    public void Optimize_KeepsCopyWhenSourceIsRedefinedBeforeUse()
    {
        var var10 = new Variable(10);
        var var8 = new Variable(8);

        var operations = new List<Operation>
        {
            new SetOperation(var10.ToSet(), new ConstExpr(1)),
            new SetOperation(var8.ToSet(), var10.ToGet()),
            new SetOperation(var10.ToSet(), new ConstExpr(2)),
            new CallOperation("use_first", [var8.ToGet()]),
            new CallOperation("use_second", [var10.ToGet()]),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Contains(optimized, op => op is SetOperation { Dst: VariableExpr { Var.Number: 8 } });
        var firstUse = optimized.OfType<CallOperation>().First(c => c.Name == "use_first");
        ExprTestHelpers.AssertSameVariable(var8, firstUse.Args[0]);
    }

    // var1=42; var2=var1; var3=var2; use(var3) → use(var1), промежуточные копии удаляются
    [Fact]
    public void Optimize_PropagatesCopyChain()
    {
        var var1 = new Variable(1);
        var var2 = new Variable(2);
        var var3 = new Variable(3);

        var operations = new List<Operation>
        {
            new SetOperation(var1.ToSet(), new ConstExpr(42)),
            new SetOperation(var2.ToSet(), var1.ToGet()),
            new SetOperation(var3.ToSet(), var2.ToGet()),
            new CallOperation("use", [var3.ToGet()]),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        var use = Assert.IsType<CallOperation>(optimized[^1]);
        ExprTestHelpers.AssertSameVariable(var1, use.Args[0]);
        Assert.DoesNotContain(optimized, op => op is SetOperation { Dst: VariableExpr { Var.Number: 2 or 3 } });
    }

    // temp = sub_0010(5); printf("%d", temp) → printf("%d", sub_0010(5))
    [Fact]
    public void Optimize_PropagatesCallResultToPrintf()
    {
        var temp = new Variable(1, IsTemp: true);

        var operations = new List<Operation>
        {
            new SetOperation(temp.ToSet(), new CallExpr("sub_0010", [new ConstExpr(5)])),
            new CallOperation("printf", [new StringExpr("%d\n"), temp.ToGet()]),
            new ReturnOperation(new ConstExpr(0)),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Equal(2, optimized.Count);
        var call = Assert.IsType<CallOperation>(optimized[0]);
        Assert.Equal("printf", call.Name);
        Assert.IsType<CallExpr>(call.Args[1]);
        Assert.DoesNotContain(optimized, op => op is SetOperation { Dst: VariableExpr { Var.IsTemp: true } });
    }

    // temp1=a%b; temp2=a/b; printf(..., temp1, temp2) → printf(..., a%b, a/b)
    [Fact]
    public void Optimize_PropagatesExpressionToCallArguments()
    {
        var a = new Variable(1);
        var b = new Variable(2);
        var tempRem = new Variable(1, IsTemp: true);
        var tempQuot = new Variable(2, IsTemp: true);

        var operations = new List<Operation>
        {
            new SetOperation(tempRem.ToSet(), new Math2Expr(Math2Operation.Mod, a.ToGet(), b.ToGet())),
            new SetOperation(tempQuot.ToSet(), new Math2Expr(Math2Operation.Div, a.ToGet(), b.ToGet())),
            new CallOperation("printf", [new StringExpr("%d %d\n"), tempRem.ToGet(), tempQuot.ToGet()]),
            new ReturnOperation(new ConstExpr(0)),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Equal(2, optimized.Count);
        var call = Assert.IsType<CallOperation>(optimized[0]);
        Assert.Equal("printf", call.Name);
        Assert.IsType<Math2Expr>(call.Args[1]);
        Assert.IsType<Math2Expr>(call.Args[2]);
        Assert.DoesNotContain(optimized, op => op is SetOperation { Dst: VariableExpr { Var.IsTemp: true } });
    }

    // return (arg0 + arg1) без промежуточной локали var11
    [Fact]
    public void Optimize_PropagatesExpressionToReturn()
    {
        var arg0 = new Variable(0);
        var arg1 = new Variable(1);
        var var11 = new Variable(11);

        var operations = new List<Operation>
        {
            new SetOperation(var11.ToSet(), new Math2Expr(Math2Operation.Add, arg0.ToGet(), arg1.ToGet())),
            new ReturnOperation(var11.ToGet()),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Single(optimized);
        var ret = Assert.IsType<ReturnOperation>(optimized[0]);
        var sum = Assert.IsType<Math2Expr>(ret.Value);
        Assert.Equal(Math2Operation.Add, sum.Operation);
        ExprTestHelpers.AssertSameVariable(arg0, sum.First);
        ExprTestHelpers.AssertSameVariable(arg1, sum.Second);
    }

    [Fact]
    public void Optimize_DoesNotPropagateCopyAcrossLocalReassignment()
    {
        // Паттерн bits.c: один локал переприсваивается при записи в битовые поля.
        var storage = new Variable(5);
        var tempReady = new Variable(8);
        var tempMode = new Variable(9);
        var tempCount = new Variable(11);

        var operations = new List<Operation>
        {
            new SetOperation(tempReady.ToSet(), new ConstExpr(1)),
            new SetOperation(storage.ToSet(), tempReady.ToGet()),
            new SetOperation(tempMode.ToSet(), new Math2Expr(Math2Operation.Or,
                new Math2Expr(Math2Operation.And, storage.ToGet(), new ConstExpr(65521)),
                new ConstExpr(10))),
            new SetOperation(storage.ToSet(), tempMode.ToGet()),
            new SetOperation(tempCount.ToSet(), new Math2Expr(Math2Operation.Or,
                new Math2Expr(Math2Operation.And, storage.ToGet(), new ConstExpr(65295)),
                new ConstExpr(160))),
            new SetOperation(storage.ToSet(), tempCount.ToGet()),
            new SetOperation(new Variable(20).ToSet(), new CallExpr("printf", [
                new StringExpr("%u %u %u\n"),
                new Math2Expr(Math2Operation.And, storage.ToGet(), new ConstExpr(1)),
                new Math2Expr(Math2Operation.And,
                    new Math2Expr(Math2Operation.Shr, storage.ToGet(), new ConstExpr(1)),
                    new ConstExpr(7)),
                new Math2Expr(Math2Operation.And,
                    new Math2Expr(Math2Operation.Shr, storage.ToGet(), new ConstExpr(4)),
                    new ConstExpr(15)),
            ])),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        var printfArgs = optimized
            .SelectMany(static op => op switch
            {
                SetOperation { Src: CallExpr call } => call.Args,
                CallOperation call => call.Args,
                _ => [],
            })
            .ToList();

        // Не подставляем устаревшее значение после ready (var8) в mode/count.
        Assert.DoesNotContain("var8", printfArgs[2].ToString());
        Assert.DoesNotContain("var8", printfArgs[3].ToString());
        Assert.Equal("(var11 >> 1) & 7", printfArgs[2].ToString());
    }

    // a = a - 1 вместо temp = a-1; a = temp (паттерн dec)
    [Fact]
    public void Optimize_FoldsTempLocalPlusMinusOneIntoSelfAssign()
    {
        var local = new Variable(8, Name: "a");
        var temp = new Variable(10, Name: "t");

        var operations = new List<Operation>
        {
            new SetOperation(temp.ToSet(), new Math2Expr(Math2Operation.Sub, local.ToGet(), ConstExpr.One)),
            new SetOperation(local.ToSet(), temp.ToGet()),
            new ReturnOperation(local.ToGet()),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.DoesNotContain(optimized, op => op is SetOperation { Dst: VariableExpr { Var.Number: 10 } });
        var ret = Assert.IsType<ReturnOperation>(optimized[^1]);
        var math = Assert.IsType<Math2Expr>(ret.Value);
        Assert.Equal(Math2Operation.Sub, math.Operation);
        ExprTestHelpers.AssertSameVariable(local, math.First);
    }

    // Присваивание стековой локали без дальнейшего использования сохраняется (может быть side-effect)
    [Fact]
    public void Optimize_KeepsUnusedStackLocalAssignment()
    {
        var stackLocal = new Variable(1, IsStack: true);

        var operations = new List<Operation>
        {
            new SetOperation(stackLocal.ToSet(), new ConstExpr(10)),
            new ReturnOperation(ConstExpr.Zero),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Contains(optimized, op => op is SetOperation { Dst: VariableExpr { Var.IsStack: true } });
    }

    // Копирование со стековой локали в temp не разворачивается — стек может перезаписаться
    [Fact]
    public void Optimize_DoesNotPropagateCopyFromStackLocal()
    {
        var stackLocal = new Variable(1, IsStack: true);
        var temp = new Variable(1, IsTemp: true);

        var operations = new List<Operation>
        {
            new SetOperation(stackLocal.ToSet(), new ConstExpr(42)),
            new SetOperation(temp.ToSet(), stackLocal.ToGet()),
            new CallOperation("use", [temp.ToGet()]),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Contains(optimized, op => op is SetOperation { Dst: VariableExpr { Var.IsStack: true } });
    }

    // var11 = var10+1; var10 = 999; return var11 — нельзя подставлять (var10+1) в return
    [Fact]
    public void Optimize_DoesNotPropagateExpressionToReturnWhenSourceVariableIsRedefined()
    {
        var var10 = new Variable(10);
        var var11 = new Variable(11);

        var operations = new List<Operation>
        {
            new SetOperation(var11.ToSet(), new Math2Expr(Math2Operation.Add, var10.ToGet(), new ConstExpr(1))),
            new SetOperation(var10.ToSet(), new ConstExpr(999)),
            new ReturnOperation(var11.ToGet()),
        };

        var optimized = OperationOptimizer.Optimize(operations);

        Assert.Contains(optimized, op => op is SetOperation { Dst: VariableExpr { Var.Number: 11 } });
        var ret = Assert.IsType<ReturnOperation>(optimized[^1]);
        ExprTestHelpers.AssertSameVariable(var11, ret.Value!);
    }
}
