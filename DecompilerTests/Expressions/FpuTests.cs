namespace DecompilerTests.Expressions;

/// <summary>FPU-инструкции пока игнорируются в IR (не порождают Set/Store).</summary>
public class FpuTests : BaseTests
{
    // fwait; faddp; nop; ret — без NotImplementedException и без side-effect операций
    [Fact(Skip = "NotImplemented")]
    public void Fwait_And_Fpu_Are_Treated_As_Nop_In_ExpressionBuilder()
    {
        // FWAIT + FPU (типичный thunk) + RET — не должны бросать NotImplementedException
        // и не должны порождать операций (кроме возможных от других инструкций).
        var builder = BuildExpressions("""
            9B          ; fwait
            DE C1       ; faddp  (fpu)
            90          ; nop
            C3          ; ret
            """);

        // Успешно дошли сюда — без exception.
        var ops = CreateFlattener(builder).GetAllOperations();
        // FWAIT/FPU/NOP не создают SetOperation / StoreOperation.
        Assert.DoesNotContain(ops, op => op is SetOperation);
        Assert.DoesNotContain(ops, op => op is StoreOperation);
    }
}
