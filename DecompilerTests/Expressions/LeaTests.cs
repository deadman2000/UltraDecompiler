namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты инструкции LEA (загрузка эффективного адреса).
/// Проверяют, что в регистр попадает вычисленный offset, а не содержимое памяти.
/// </summary>
public class LeaTests : BaseTests
{
    [Fact]
    public void Lea_Bx_SiPlusDisp_ProducesSetWithEffectiveAddress()
    {
        // LEA BX, [SI+0Ah]
        var expr = BuildExpressionsRaw("8D 5C 0A");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        ExprTestHelpers.AssertReferencesVariable(s.Dst, expr.Variables.BX);
        var addr = Assert.IsType<Math2Expr>(s.Src);
        Assert.Equal(Math2Operation.Add, addr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.SI, addr.First);
        Assert.Equal(0x0A, ((ConstExpr)addr.Second).Value);
    }

    [Fact]
    public void Lea_Bx_Ax_RegForm_ProducesSetOperation()
    {
        // LEA BX, AX (mod=3)
        var expr = BuildExpressionsRaw("8D D8");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        ExprTestHelpers.AssertReferencesVariable(s.Dst, expr.Variables.BX);
        ExprTestHelpers.AssertSameVariable(expr.Variables.AX, s.Src);
    }

    [Fact]
    public void Lea_Ax_Bx_ProducesSetWithRegisterAddress()
    {
        // LEA AX, [BX]
        var expr = BuildExpressionsRaw("8D 07");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        ExprTestHelpers.AssertReferencesVariable(s.Dst, expr.Variables.AX);
        ExprTestHelpers.AssertSameVariable(expr.Variables.BX, s.Src);
    }

    [Fact]
    public void Lea_Ax_MemoryDirect_ProducesSetWithConstAddress()
    {
        // LEA AX, [1234h]
        var expr = BuildExpressionsRaw("8D 06 34 12");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        ExprTestHelpers.AssertReferencesVariable(s.Dst, expr.Variables.AX);
        var addr = Assert.IsType<ConstExpr>(s.Src);
        Assert.Equal(0x1234, addr.Value);
    }

    [Fact]
    public void Lea_Cx_BpPlusSi_ProducesSetWithSumAddress()
    {
        // LEA CX, [BP+SI]
        var expr = BuildExpressionsRaw("8D 0A");

        var set = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<SetOperation>(set);

        ExprTestHelpers.AssertReferencesVariable(s.Dst, expr.Variables.CX);
        var addr = Assert.IsType<Math2Expr>(s.Src);
        Assert.Equal(Math2Operation.Add, addr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.BP, addr.First);
        ExprTestHelpers.AssertSameVariable(expr.Variables.SI, addr.Second);
    }

    [Fact]
    public void Lea_BpPlus4_DoesNotLoadStackParameter()
    {
        // LEA AX, [BP+4] — адрес параметра, а не его значение (в отличие от MOV)
        var expr = BuildExpressionsRaw("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            8D 46 04  ; LEA AX, [BP+4]
            """);

        var set = Assert.Single(expr.Blocks[0].Operations.OfType<SetOperation>());
        ExprTestHelpers.AssertReferencesVariable(set.Dst, expr.Variables.AX);

        var addr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, addr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.BP, addr.First);
        Assert.Equal(4, ((ConstExpr)addr.Second).Value);
    }

    [Fact]
    public void Lea_BpMinus2_DoesNotResolveStackLocal()
    {
        // LEA AX, [BP-2] — адрес локала, а не переменная varN
        var expr = BuildExpressionsRaw("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            50        ; PUSH AX
            8D 46 FE  ; LEA AX, [BP-2]
            """);

        var set = Assert.Single(expr.Blocks[0].Operations.OfType<SetOperation>());
        ExprTestHelpers.AssertReferencesVariable(set.Dst, expr.Variables.AX);

        var addr = Assert.IsType<Math2Expr>(set.Src);
        Assert.Equal(Math2Operation.Add, addr.Operation);
        ExprTestHelpers.AssertSameVariable(expr.Variables.BP, addr.First);
        Assert.Equal(-2, ((ConstExpr)addr.Second).Value);
    }
}
