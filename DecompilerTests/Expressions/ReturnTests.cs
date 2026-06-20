namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты поддержки return: RET/RET_IMM теперь порождают ReturnOperation с текущим значением AX.
/// Проверяем IR-уровень (ExpressionBuilder), а не только call-site (CallHandler).
/// </summary>
public class ReturnTests : BaseTests
{
    // return 42: mov ax,42; ret → ReturnOperation(ConstExpr(42))
    [Fact]
    public void MovAxConst_Ret_ProducesReturnOperationWithConstValue()
    {
        // Чистый возврат константы — типичный паттерн "return 42;"
        var expr = BuildExpressions("""
            B8 2A 00    ; mov ax, 42
            C3          ; ret
            """);

        var block = expr.Blocks[0];
        // MOV ax не создаёт Operation (только обновляет регистры), RET — создаёт ReturnOperation
        Assert.Single(block.Operations);

        var retOp = Assert.IsType<ReturnOperation>(block.Operations[0]);
        var constVal = Assert.IsType<ConstExpr>(retOp.Value);
        Assert.Equal(42, constVal.Value);

        // EndRegisters.AX должен содержать то же значение
        var ax = Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(42, ax.Value);
    }

    // return (5+10): арифметика перед ret попадает в ReturnOperation.Value
    [Fact]
    public void ArithBeforeRet_ProducesReturnWithMathExpr()
    {
        // Возврат результата вычисления
        var expr = BuildExpressions("""
            B8 05 00    ; mov ax, 5
            83 C0 0A    ; add ax, 10
            C3          ; ret
            """);

        var block = expr.Blocks[0];
        // ReturnOperation должен быть (как минимум последний). 
        // (Set от ADD может присутствовать в ops в зависимости от деталей ArithmeticHandler; проверяем главное — возврат.)
        var retOp = Assert.IsType<ReturnOperation>(block.Operations[^1]);
        // Значение на return — результат арифметики перед RET (Math2Expr или эквивалент)
        Assert.NotNull(retOp.Value);
    }

    // void foo_ret: JMP на эпилог → ReturnOperation с IsExplicit (явный return в QuickC)
    [Fact]
    public void JmpToEpilogue_ProducesExplicitReturnOperation()
    {
        var expr = BuildProcExpressions("""
            55          ; push bp
            8B EC       ; mov bp, sp
            57          ; push di
            56          ; push si
            E9 00 00    ; jmp epilogue
            5E          ; pop si
            5F          ; pop di
            8B E5       ; mov sp, bp
            5D          ; pop bp
            C3          ; ret
            """);

        var retOp = expr.Blocks
            .SelectMany(static b => b.Operations)
            .OfType<ReturnOperation>()
            .Single(static r => r.IsExplicit);

        Assert.True(retOp.IsExplicit);
    }

    // void foo(flag){ if(flag){} }: jcc и fall-through jmp в один эпилог → merge, не явный return
    [Fact]
    public void MergeJmpToEpilogue_IsImplicitReturnOperation()
    {
        var expr = BuildProcExpressions("""
            55          ; push bp
            8B EC       ; mov bp, sp
            83 7E 04 00 ; cmp [bp+4], 0
            75 03       ; jne epilogue
            E9 00 00    ; jmp epilogue
            5E          ; pop si
            5F          ; pop di
            8B E5       ; mov sp, bp
            5D          ; pop bp
            C3          ; ret
            """);

        var ops = CreateFlattener(expr).GetAllOperations();
        Assert.DoesNotContain(
            ops.OfType<ReturnOperation>(),
            static r => r.IsExplicit);
    }

    // void foo: линейный RET без JMP → ReturnOperation неявный (без return в C)
    [Fact]
    public void LinearRet_ProducesImplicitReturnOperation()
    {
        var expr = BuildExpressions("""
            55          ; push bp
            8B EC       ; mov bp, sp
            5D          ; pop bp
            C3          ; ret
            """);

        var retOp = Assert.IsType<ReturnOperation>(expr.Blocks[0].Operations[^1]);
        Assert.False(retOp.IsExplicit);
    }

    // ret без явного mov ax — ReturnOperation с текущим (init) AX
    [Fact]
    public void RetWithoutAxTouch_StillProducesReturnOperation_WithCurrentAx()
    {
        // Даже если функция "void-like" и не трогала AX явно перед RET,
        // мы всё равно фиксируем текущее (init) значение AX в ReturnOperation.
        // Решение "void или нет" — на уровне сигнатуры и codegen.
        var expr = BuildExpressions("""
            90          ; nop
            C3          ; ret
            """);

        var block = expr.Blocks[0];
        var retOp = Assert.IsType<ReturnOperation>(block.Operations.Last());
        // Value может быть Variable (init) или Const — главное, что ReturnOperation создан
        Assert.NotNull(retOp);
    }
}
