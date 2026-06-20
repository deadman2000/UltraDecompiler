namespace DecompilerTests.Expressions;

/// <summary>Тесты подстановки аргументов CALL по сигнатуре из ProcedureStorage.</summary>
public class CallArgumentsTests : BaseTests
{
    // push fmt; push val; call printf → CallExpr("printf", [0x1000, 0x1234])
    [Fact(Skip = "NotImplemented")]
    public void DirectCall_WithPrintfSignature_PassesStackArguments()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));
        var catalog = HeaderCatalog.Load(includeDir);
        Assert.True(catalog.TryGetProcedureSignature("printf", out var printfSig));
        Assert.NotNull(printfSig);
        Assert.True(printfSig!.IsVariadic);
        Assert.True(printfSig.Parameters.Count >= 1, "printf должен иметь хотя бы 1 параметр (формат)");
        var firstParamType = printfSig.Parameters[0].Type;
        var firstTypeStr = firstParamType.ToString();
        Assert.Equal("char*", firstTypeStr);
        Assert.Equal(CTypeKind.Pointer, firstParamType.Kind);

        var storage = new ProcedureStorage();
        storage.Add(new DisassembledProcedure
        {
            Offset = 0xE,
            Instructions = [],
            Name = "printf",
            IsLibrary = true,
            Signature = printfSig!,
        });

        var expr = BuildExpressions("""
            68 34 12    ; push 1234h
            68 00 10    ; push 1000h
            E8 05 00    ; call 8
            90
            """, storage);

        var setOp = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[0]);
        var callExpr = Assert.IsType<CallExpr>(setOp.Src);
        Assert.Equal("printf", callExpr.Name);
        Assert.Equal(2, callExpr.Args.Count);

        var fmt = Assert.IsType<ConstExpr>(callExpr.Args[0]);
        Assert.Equal(0x1000, fmt.Value);
        var value = Assert.IsType<ConstExpr>(callExpr.Args[1]);
        Assert.Equal(0x1234, value.Value);
    }

    // Паттерн bits.c: mov/push между push'ами — аргументы printf не схлопываются (1, 5, 10)
    [Fact(Skip = "NotImplemented")]
    public void DirectCall_PrintfWithRegisterPushes_PreservesComputedArgumentExpressions()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));
        var catalog = HeaderCatalog.Load(includeDir);
        Assert.True(catalog.TryGetProcedureSignature("printf", out var printfSig));
        Assert.NotNull(printfSig);

        var storage = new ProcedureStorage();
        storage.Add(new DisassembledProcedure
        {
            Offset = 0x18,
            Instructions = [],
            Name = "printf",
            IsLibrary = true,
            Signature = printfSig!,
        });

        // Паттерн QuickC (bits.c): mov ax, val; push ax — между push и call AX перезаписывается.
        var expr = BuildExpressions("""
            B8 0A 00    ; mov ax, 10
            50          ; push ax
            B8 05 00    ; mov ax, 5
            50          ; push ax
            B8 01 00    ; mov ax, 1
            50          ; push ax
            B8 6A 02    ; mov ax, 26Ah
            50          ; push ax
            E8 05 00    ; call 8
            90
            """, storage);

        var setOp = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[0]);
        var callExpr = Assert.IsType<CallExpr>(setOp.Src);
        Assert.Equal("printf", callExpr.Name);
        Assert.Equal(4, callExpr.Args.Count);

        Assert.IsType<ConstExpr>(callExpr.Args[0]);
        Assert.Equal(0x26A, ((ConstExpr)callExpr.Args[0]).Value);

        var ready = Assert.IsType<ConstExpr>(callExpr.Args[1]);
        Assert.Equal(1, ready.Value);

        var mode = Assert.IsType<ConstExpr>(callExpr.Args[2]);
        Assert.Equal(5, mode.Value);

        var count = Assert.IsType<ConstExpr>(callExpr.Args[3]);
        Assert.Equal(10, count.Value);
    }

    // void perror(msg) — один строковый аргумент с стека
    [Fact(Skip = "NotImplemented")]
    public void DirectCall_VoidPerror_EmitsCallOperationWithoutSet()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));
        var catalog = HeaderCatalog.Load(includeDir);
        Assert.True(catalog.TryGetProcedureSignature("perror", out var perrorSig));
        Assert.NotNull(perrorSig);
        Assert.True(perrorSig!.ReturnType.IsVoid);

        var storage = new ProcedureStorage();
        storage.Add(new DisassembledProcedure
        {
            Offset = 0x9,
            Instructions = [],
            Name = "perror",
            IsLibrary = true,
            Signature = perrorSig,
        });

        var expr = BuildExpressions("""
            68 00 20    ; push string
            E8 03 00    ; call 9
            90
            """, storage);

        var setOp = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[0]);
        var callExpr = Assert.IsType<CallExpr>(setOp.Src);
        Assert.Equal("perror", callExpr.Name);
        Assert.Single(callExpr.Args);
    }

    protected static ExpressionBuilder BuildExpressions(string hex, ProcedureStorage procedures, bool isCom = false)
    {
        var graph = GetGraph(hex);
        var decompiler = new ExpressionBuilder();
        decompiler.Build(graph, isCom);
        CallSiteResolver.ResolveBlocks(decompiler.Blocks, procedures);
        return decompiler;
    }
}
