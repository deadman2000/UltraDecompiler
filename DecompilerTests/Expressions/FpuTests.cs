using UltraDecompiler.Decompilation.Operations;

namespace DecompilerTests.Expressions;

public class FpuTests : BaseTests
{
    [Fact]
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
        var ops = builder.GetAllOperations();
        // FWAIT/FPU/NOP не создают SetOperation / StoreOperation.
        Assert.DoesNotContain(ops, op => op is SetOperation);
        Assert.DoesNotContain(ops, op => op is StoreOperation);
    }
}
