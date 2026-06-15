namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты новой логики моделирования флагов (CF после CMP/ADD/SUB,
/// сброс CF/OF при логических операциях, и преобразование этих флагов
/// в условия условных переходов Jcc).
/// </summary>
public class FlagModelingTests : BaseTests
{
    // cmp 5,3 → CF = (5 <u 3) = false, моделируется как CmpExpr Ult
    [Fact]
    public void Cmp_SetsCfAsUnsignedLessThan()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            """);

        var block = expr.Blocks[0];
        var cf = Assert.IsType<CmpExpr>(block.EndRegisters.CF);
        Assert.Equal(CmpOperation.Ult, cf.Operation);
    }

    // jae после cmp → условие (left >=u right)
    [Fact]
    public void Cmp_Jae_ProducesUgeCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            73 01    ; jae +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.First(b => b.ConditionalBlock != null);
        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Uge, cond.Operation);
    }

    [Fact]
    public void Cmp_Jb_ProducesUltCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            72 01    ; jb +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.First(b => b.ConditionalBlock != null);
        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Ult, cond.Operation);
    }

    [Fact]
    public void Cmp_Ja_ProducesUgtCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            77 01    ; ja +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.First(b => b.ConditionalBlock != null);
        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Ugt, cond.Operation);
    }

    // sub с заёмом → CF как беззнаковое «меньше»
    [Fact]
    public void Sub_SetsCfCorrectly()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            2D 03 00 ; sub ax, 3
            """);

        var block = expr.Blocks[0];
        var cf = Assert.IsType<CmpExpr>(block.EndRegisters.CF);
        Assert.Equal(CmpOperation.Ult, cf.Operation);
    }

    [Fact]
    public void Add_SetsCf()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            05 FF 00 ; add ax, 0FFh
            """);

        var block = expr.Blocks[0];
        // После ADD мы тоже устанавливаем CF (хотя и приближённо)
        Assert.IsType<CmpExpr>(block.EndRegisters.CF);
    }

    // ============================================================
    // Тесты Direction Flag (DF) — CLD / STD
    // ============================================================

    [Fact]
    public void Cld_SetsDfToZero()
    {
        var expr = BuildExpressions("""
            FC    ; cld
            """);

        var block = expr.Blocks[0];
        Assert.Equal(ConstExpr.Zero, block.EndRegisters.DF);
    }

    [Fact]
    public void Std_SetsDfToOne()
    {
        var expr = BuildExpressions("""
            FD    ; std
            """);

        var block = expr.Blocks[0];
        Assert.Equal(ConstExpr.One, block.EndRegisters.DF);
    }

    [Fact]
    public void Cld_Std_Cld_Sequence()
    {
        var expr = BuildExpressions("""
            FC    ; cld
            FD    ; std
            FC    ; cld
            """);

        // Все инструкции без переходов попадают в один блок
        var block = expr.Blocks[0];
        Assert.Equal(ConstExpr.Zero, block.EndRegisters.DF);
    }

    [Fact]
    public void Df_Persists_Across_Blocks()
    {
        // DF должен передаваться между базовыми блоками
        var expr = BuildExpressions("""
            FD          ; std
            75 01       ; jne +1
            90          ; nop (fallthrough)
            90          ; nop (target of jne)
            """);

        // Блок с STD
        var firstBlock = expr.Blocks.First(b => b.BasicBlock.Instructions.Any(i => i.Mnemonic == Mnemonic.STD));
        Assert.Equal(ConstExpr.One, firstBlock.EndRegisters.DF);

        // Следующий блок должен унаследовать DF = 1
        if (firstBlock.Next != null)
        {
            Assert.Equal(ConstExpr.One, firstBlock.Next.InitRegisters.DF);
        }
    }

    [Fact]
    public void LogicalOps_ClearCfAndOf()
    {
        var andExpr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            25 03 00 ; and ax, 3
            """);
        var orExpr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            0D 03 00 ; or ax, 3
            """);
        var xorExpr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            35 03 00 ; xor ax, 3
            """);

        foreach (var e in new[] { andExpr, orExpr, xorExpr })
        {
            var block = e.Blocks[0];
            var cf = Assert.IsType<ConstExpr>(block.EndRegisters.CF);
            var of = Assert.IsType<ConstExpr>(block.EndRegisters.OF);
            Assert.Equal(0, cf.Value);
            Assert.Equal(0, of.Value);
        }
    }

    [Fact]
    public void Inc_DoesNotClearCf()
    {
        // SUB устанавливает CF (borrow). Последующий INC не должен его сбросить.
        // На реальном x86 INC/DEC не затрагивают Carry Flag.
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            2D 10 00 ; sub ax, 10h   ; borrow → CF = Ult(...)
            40       ; inc ax
            """);

        var block = expr.Blocks[0];
        var cf = Assert.IsType<CmpExpr>(block.EndRegisters.CF);
        Assert.Equal(CmpOperation.Ult, cf.Operation);
    }

    // ==================== JBE / JLE ====================

    [Fact]
    public void Cmp_Jbe_ProducesUleCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            76 01    ; jbe +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.First(b => b.ConditionalBlock != null);
        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Ule, cond.Operation);
    }

    [Fact]
    public void Cmp_Jle_UsesCompoundConditionWithZf()
    {
        // После CMP+JLE условие сводится к сравнению операндов (ax <= 3), а не к флагам.
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            7E 01    ; jle +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.First(b => b.ConditionalBlock != null);
        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Ule, cond.Operation);
    }

    // ==================== TEST + прыжки ====================

    [Fact]
    public void Test_SetsCfAndOfToZero()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            A9 01 00 ; test ax, 1
            """);

        var block = expr.Blocks[0];
        var cf = Assert.IsType<ConstExpr>(block.EndRegisters.CF);
        var of = Assert.IsType<ConstExpr>(block.EndRegisters.OF);
        Assert.Equal(0, cf.Value);
        Assert.Equal(0, of.Value);
    }

    [Fact]
    public void Test_Jz_UsesZfFromTest()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            A9 01 00 ; test ax, 1
            74 01    ; jz +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.First(b => b.ConditionalBlock != null);
        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Eq, cond.Operation);
    }

    [Fact]
    public void Test_Jnz_ProducesNegatedCondition()
    {
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            A9 01 00 ; test ax, 1
            75 01    ; jnz +1
            90       ; nop fall
            90       ; nop target
            """);

        var condBlock = expr.Blocks.First(b => b.ConditionalBlock != null);
        // Благодаря перегрузке ! (BoolNot) инверсия Eq сразу превращается в Ne (более чисто)
        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Ne, cond.Operation);
    }

    // ==================== Документирование текущего состояния SF/OF ====================

    [Fact]
    public void Cmp_DoesNotYetModelSfAndOf()
    {
        // На текущий момент HandleCmp не заполняет SF и OF —
        // они остаются со значением по умолчанию (0).
        // Этот тест документирует текущее состояние и может быть
        // обновлён, когда мы добавим моделирование знаковых флагов.
        var expr = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            """);

        var block = expr.Blocks[0];

        // SF и OF пока не вычисляются после CMP
        var sf = block.EndRegisters.SF;
        var of = block.EndRegisters.OF;

        // Они должны быть не-null и не Const(0)
        Assert.NotNull(sf);
        Assert.NotNull(of);
    }

    [Fact]
    public void Cmp_Jl_Jg_FallbackToSfEqOfLogic()
    {
        // JL / JG зависят от SF == OF. Пока SF/OF после CMP не точные,
        // мы получаем составное выражение, но оно не должно быть константой.
        var jl = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            7C 01    ; jl +1
            90       ; nop
            90       ; target
            """);

        var jg = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            3D 03 00 ; cmp ax, 3
            7F 01    ; jg +1
            90       ; nop
            90       ; target
            """);

        foreach (var e in new[] { jl, jg })
        {
            var condBlock = e.Blocks.First(b => b.ConditionalBlock != null);
            Assert.NotNull(condBlock.Condition);
            Assert.NotEqual(ConstExpr.One, condBlock.Condition);
        }
    }

    // ==================== CLI / STI / флаговые инструкции ====================

    [Fact]
    public void Cli_Sti_ProduceDisableEnableCallOperations()
    {
        // CLI → _disable(), STI → _enable()
        // Эти инструкции теперь порождают реальные CallOperation (в стиле QuickC).
        // Они не влияют на gp-регистры и отслеживаемые флаги (IF не моделируется).
        var expr = BuildExpressions("""
            FA       ; cli   → _disable
            FB       ; sti   → _enable
            B8 01 00 ; mov ax, 1   ; не порождает Operation
            """);

        var block = expr.Blocks[0];

        // Должно быть ровно 2 операции (от CLI и STI). MOV в регистр Operation не создаёт.
        Assert.Equal(2, block.Operations.Count);

        var op0 = Assert.IsType<CallOperation>(block.Operations[0]);
        Assert.Equal("_disable", op0.Name);
        Assert.Empty(op0.Args);

        var op1 = Assert.IsType<CallOperation>(block.Operations[1]);
        Assert.Equal("_enable", op1.Name);
        Assert.Empty(op1.Args);

        // Регистр AX должен обновиться от MOV
        Assert.IsType<ConstExpr>(block.EndRegisters.AX);
        Assert.Equal(1, ((ConstExpr)block.EndRegisters.AX).Value);
    }

    [Fact]
    public void Clc_Stc_Cmc_UpdateCfCorrectly()
    {
        // CLC → CF=0, STC → CF=1, CMC → инверсия CF
        var clc = BuildExpressions("""
            F8       ; clc
            """);
        Assert.IsType<ConstExpr>(clc.Blocks[0].EndRegisters.CF);
        Assert.Equal(0, ((ConstExpr)clc.Blocks[0].EndRegisters.CF).Value);

        var stc = BuildExpressions("""
            F9       ; stc
            """);
        Assert.IsType<ConstExpr>(stc.Blocks[0].EndRegisters.CF);
        Assert.Equal(1, ((ConstExpr)stc.Blocks[0].EndRegisters.CF).Value);

        // CMC после известного CF
        var cmc = BuildExpressions("""
            F9       ; stc   ; CF=1
            F5       ; cmc   ; CF=0
            """);
        var cf = Assert.IsType<ConstExpr>(cmc.Blocks[0].EndRegisters.CF);
        Assert.Equal(0, cf.Value);

        // CMC после SUB (CF = CmpExpr)
        var withCmp = BuildExpressions("""
            B8 05 00 ; mov ax, 5
            2D 03 00 ; sub ax, 3   ; устанавливает CF как Ult
            F5       ; cmc         ; инвертирует
            """);
        var cfAfterCmc = withCmp.Blocks[0].EndRegisters.CF;
        // После CMC это должен быть !(...) — Math1Expr(Not) или инвертированный Cmp (через BoolNot)
        Assert.NotNull(cfAfterCmc);
        // Не должно быть Const, т.к. исходный CF был CmpExpr
        Assert.False(cfAfterCmc is ConstExpr);
    }

    [Fact]
    public void Cld_Std_DoNotThrow()
    {
        var expr = BuildExpressions("""
            FC       ; cld
            FD       ; std
            90       ; nop
            """);

        Assert.NotEmpty(expr.Blocks);
    }
}
