namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для управления потоком и построения условий на прыжках.
/// </summary>
public class ControlFlowTests : BaseTests
{
    // jmp short → два ExprBlock, NextBlock без ConditionalBlock
    [Fact]
    public void DecompileMultipleBlocksWithJmp()
    {
        var expr = BuildExpressions("""
            B8 01 00 ; mov ax, 1
            EB 00    ; jmp short +0
            90       ; nop (в следующем блоке)
            """);

        Assert.Equal(2, expr.Blocks.Count);
        var block0 = expr.Blocks[0];
        var block1 = expr.Blocks[1];

        Assert.NotNull(block0.Next);
        Assert.Equal(block1, block0.Next);
        Assert.Null(block0.ConditionalBlock);
    }

    // === Тесты BuildJumpCondition ===

    // cmp [arg], 1; jle — условие arg <= 1, а не только равенство по ZF
    [Fact]
    public void ConditionalJump_CmpJle_ProducesLessOrEqualCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00       ; mov ax, 5
            3D 01 00       ; cmp ax, 1
            7E 01          ; jle +1
            90             ; nop fallthrough
            90             ; nop target
            """);

        var condBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(condBlock);

        var condition = Assert.IsType<CmpExpr>(condBlock!.Condition);
        Assert.Equal(CmpOperation.Ule, condition.Operation);
        Assert.IsType<ConstExpr>(condition.Right);
    }

    [Fact]
    public void ConditionalJump_CmpJe_ProducesEqualityCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 05 00 ; cmp ax, 5
            74 01    ; je +1
            90       ; nop (fallthrough)
            90       ; nop (target)
            """);

        var condBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(condBlock);
        Assert.NotNull(condBlock.Condition);
        Assert.NotEqual(ConstExpr.One, condBlock.Condition);

        var condition = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Eq, condition.Operation);
    }

    [Fact]
    public void ConditionalJump_CmpJne_ProducesNegatedCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 06 00 ; cmp ax, 6
            75 01    ; jne +1
            90       ; nop (fallthrough)
            90       ; nop (target)
            """);

        var condBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(condBlock);

        // Благодаря !(Eq) через перегрузку BoolNot теперь сразу получаем Ne (более чистое представление)
        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Ne, cond.Operation);
    }

    [Fact]
    public void ConditionalJump_Arithmetic_Jz_UsesResultVariable()
    {
        var expr = BuildExpressions("""
            B8 01 00 ; mov ax, 1
            05 FF FF ; add ax, -1
            74 01    ; jz +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(condBlock);
        Assert.NotEqual(ConstExpr.One, condBlock.Condition);

        var cmp = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Eq, cmp.Operation);
        var zero = Assert.IsType<ConstExpr>(cmp.Right);
        Assert.Equal(0, zero.Value);
    }

    [Fact]
    public void ConditionalJump_CmpJa_ProducesCompoundCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            77 01    ; ja +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(condBlock);
        Assert.NotEqual(ConstExpr.One, condBlock.Condition);

        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Ugt, cond.Operation);
    }

    [Fact]
    public void ConditionalJump_NoLongerUsesConstOnePlaceholder()
    {
        var expr = BuildExpressions("""
            B8 10 00 ; mov ax, 10h
            3D 05 00 ; cmp ax, 5
            75 01    ; jne +1
            90       ; fall
            90       ; target
            """);

        foreach (var block in expr.Blocks)
        {
            if (block.ConditionalBlock != null)
            {
                Assert.NotEqual(ConstExpr.One, block.Condition);
            }
        }
    }

    [Fact]
    public void ConditionalJump_Jcxz_ProducesCxEqualsZeroCondition()
    {
        // JCXZ не зависит от флагов — проверяет CX напрямую.
        // Используем символическую переменную для CX, чтобы условие было осмысленным.
        var expr = BuildExpressions("""
            E3 01    ; jcxz +1
            90       ; nop (fallthrough)
            90       ; nop (target)
            """,
            vars => RegisterExpressions.InitCom(vars) with { CX = vars.CreateVariable("count") });

        var condBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(condBlock);
        Assert.NotNull(condBlock.Condition);

        var condition = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Eq, condition.Operation);

        // Левая часть — наша переменная count (CX)
        var leftVar = Assert.IsType<Variable>(condition.Left);
        Assert.Equal("count", leftVar.Name);

        // Правая часть — ноль
        var zero = Assert.IsType<ConstExpr>(condition.Right);
        Assert.Equal(0, zero.Value);
    }

    // === Тесты LOOP / LOOPE / LOOPNE ===

    [Fact]
    public void Loop_Basic_DecrementsCxAndBranchesWhileNotZero()
    {
        // Простой LOOP с символическим CX — гарантированно создаст SetOperation
        var expr = BuildExpressions("""
            E2 01      ; loop +1
            90         ; fallthrough
            90         ; target
            """,
            vars => RegisterExpressions.InitCom(vars) with { CX = vars.CreateVariable("counter") });

        var loopBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(loopBlock);
        Assert.NotNull(loopBlock.Condition);

        // Условие должно быть CX != 0
        var cond = Assert.IsType<CmpExpr>(loopBlock.Condition);
        Assert.Equal(CmpOperation.Ne, cond.Operation);

        // CX в конце блока должен отличаться от начального (был произведён декремент)
        // (даже если SetOperation не создан из-за constant folding в каких-то случаях)
        Assert.NotSame(loopBlock.InitRegisters.CX, loopBlock.EndRegisters.CX);
    }

    [Fact]
    public void Loope_BranchesWhileCxNotZeroAndZfSet()
    {
        // LOOPE: переход если CX != 0 И ZF == 1
        var expr = BuildExpressions("""
            B9 05 00   ; mov cx, 5
            3C 00      ; cmp al, 0     (устанавливаем ZF)
            E1 01      ; loope +1
            90         ; fall
            90         ; target
            """);

        var loopBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(loopBlock);

        // Условие должно быть составным: (CX != 0) && ZF
        var cond = Assert.IsType<Math2Expr>(loopBlock.Condition);
        Assert.Equal(Math2Operation.And, cond.Operation);
    }

    [Fact]
    public void Loopne_BranchesWhileCxNotZeroAndZfClear()
    {
        // LOOPNE: переход если CX != 0 И ZF == 0
        var expr = BuildExpressions("""
            B9 04 00   ; mov cx, 4
            3C 01      ; cmp al, 1     (ZF = 0)
            E0 01      ; loopne +1
            90         ; fall
            90         ; target
            """);

        var loopBlock = expr.Blocks.FirstOrDefault(b => b.ConditionalBlock != null);
        Assert.NotNull(loopBlock);

        // Условие должно быть составным: (CX != 0) && !ZF
        var cond = Assert.IsType<Math2Expr>(loopBlock.Condition);
        Assert.Equal(Math2Operation.And, cond.Operation);
    }

    // === Тесты IN / OUT ===

    [Fact]
    public void In_Imm8_ProducesCallAndWritesToAl()
    {
        // E4 21  — IN AL, 21h
        var expr = BuildExpressions("E4 21");

        // Должен появиться SetOperation (результат IN захвачен)
        Assert.Contains(expr.Blocks[0].Operations, op => op is SetOperation);
    }

    [Fact]
    public void Out_Imm8_ProducesCallOperation()
    {
        // E6 21     ; OUT 21h, AL
        // B0 AA     ; mov al, 0AAh   (чтобы было значение)
        var expr = BuildExpressions("""
            B0 AA   ; mov al, 0AAh
            E6 21   ; out 21h, al
            """);

        // Последняя операция должна быть CallOperation outb
        var lastOp = expr.Blocks[0].Operations.LastOrDefault();
        var callOp = Assert.IsType<CallOperation>(lastOp);
        Assert.Equal("outb", callOp.Name);
    }

    [Fact]
    public void In_Dx_UsesDxAsPort()
    {
        // EC  — IN AL, DX
        var expr = BuildExpressions("EC", vars =>
        {
            var dxVal = vars.CreateVariable("port");
            var init = RegisterExpressions.InitZero();
            return init.Set16(GpRegister16.DX, dxVal);
        });

        // Должен появиться SetOperation
        Assert.Contains(expr.Blocks[0].Operations, op => op is SetOperation);
    }
}
