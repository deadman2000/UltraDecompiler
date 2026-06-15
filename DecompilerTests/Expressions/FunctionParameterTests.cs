namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты восстановления параметров декомпилируемой функции (пролог + [BP+offset]).
/// </summary>
public class FunctionParameterTests : BaseTests
{
    // push bp; mov bp,sp; [bp+4],[bp+6] → arg0, arg1 в Parameters
    [Fact]
    public void StandardPrologue_TwoParameters_ExposedOnBuilder()
    {
        var expr = BuildExpressions("""
            55          ; push bp
            8B EC       ; mov bp, sp
            8B 46 04    ; mov ax, [bp+4]
            8B 5E 06    ; mov bx, [bp+6]
            """);

        Assert.Equal(2, expr.Parameters.Count);
        Assert.Equal(4, expr.Parameters[0].StackOffset);
        Assert.Equal("arg0", expr.Parameters[0].Variable.Name);
        Assert.Equal(6, expr.Parameters[1].StackOffset);
        Assert.Equal("arg1", expr.Parameters[1].Variable.Name);
    }

    // Чтения [bp+4]/[bp+6] подставляются как Variable arg0/arg1 в регистрах
    [Fact]
    public void StandardPrologue_ParameterReads_UseNamedVariables()
    {
        var expr = BuildExpressions("""
            55          ; push bp
            8B EC       ; mov bp, sp
            8B 46 04    ; mov ax, [bp+4]
            8B 5E 06    ; mov bx, [bp+6]
            """);

        var block = expr.Blocks[0];
        var ax = Assert.IsType<Variable>(block.EndRegisters.AX);
        var bx = Assert.IsType<Variable>(block.EndRegisters.BX);

        Assert.Equal("arg0", ax.Name);
        Assert.Equal("arg1", bx.Name);
    }

    // enter 4,0 + [bp+4] → один параметр arg0
    [Fact]
    public void EnterPrologue_DetectsParameters()
    {
        var expr = BuildExpressions("""
            C8 04 00 00 ; enter 4, 0
            8B 46 04    ; mov ax, [bp+4]
            """);

        Assert.Single(expr.Parameters);
        Assert.Equal(4, expr.Parameters[0].StackOffset);
        Assert.Equal("arg0", expr.Parameters[0].Variable.Name);
    }

    // [bp+4] без пролога — MemExpr, не параметр
    [Fact]
    public void NoPrologue_NoParameters()
    {
        var expr = BuildExpressions("""
            8B 46 04    ; mov ax, [bp+4]  (BP не инициализирован прологом)
            """);

        Assert.Empty(expr.Parameters);

        var ax = expr.Blocks[0].EndRegisters.AX;
        Assert.IsType<MemExpr>(ax);
    }

    // [bp-2] после sub sp,2 — локаль varN, не argN
    [Fact]
    public void LocalVariable_BpMinus2_NotTreatedAsParameter()
    {
        var expr = BuildExpressions("""
            55          ; push bp
            8B EC       ; mov bp, sp
            83 EC 02    ; sub sp, 2
            8B 46 FE    ; mov ax, [bp-2]
            """);

        Assert.Empty(expr.Parameters);

        // [bp-2] должен подставляться как локальная переменная (Variable)
        var ax = expr.Blocks[0].EndRegisters.AX;
        var localVar = Assert.IsType<Variable>(ax);
        // Локалы создаются без имени (varN), в отличие от argN для параметров
        Assert.True(string.IsNullOrEmpty(localVar.Name) || localVar.Name.StartsWith("var"),
            "Локальная переменная должна иметь имя varN или без имени");
    }

    // [bp+0] saved BP и [bp+2] return addr — не параметры вызова
    [Fact]
    public void SavedFrameSlots_BpPlus0And2_NotParameters()
    {
        var expr = BuildExpressions("""
            55          ; push bp
            8B EC       ; mov bp, sp
            8B 46 00    ; mov ax, [bp+0]  saved BP
            8B 5E 02    ; mov bx, [bp+2]  return address
            """);

        Assert.Empty(expr.Parameters);
    }

    // add ax, [bp+4] → SetOperation(..., arg0)
    [Fact]
    public void ParameterUsedInArithmetic_ProducesSetOperationWithVariable()
    {
        var expr = BuildExpressions("""
            55          ; push bp
            8B EC       ; mov bp, sp
            03 46 04    ; add ax, [bp+4]
            """);

        Assert.Single(expr.Parameters);
        Assert.Single(expr.Blocks[0].Operations);

        var setOp = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[0]);
        var add = Assert.IsType<Math2Expr>(setOp.Src);
        var param = Assert.IsType<Variable>(add.Second);
        Assert.Equal("arg0", param.Name);
    }
}
