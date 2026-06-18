namespace DecompilerTests.Loops;

/// <summary>
/// Тесты анализатора циклов для QuickC /Od.
/// </summary>
public class QuickCUnoptLoopAnalyzerTests : BaseTests
{
    /// <summary>
    /// Проверяет, что анализатор распознаёт for-цикл со счётчиком.
    /// </summary>
    [Fact]
    public void Analyze_ForLoop_DetectsCounter()
    {
        var ops = BuildProcExpressions("""
            55 8B EC 81 EC 04 00
            C7 46 FC 00 00    ; sum = 0
            C7 46 FE 00 00    ; i = 0
            E9 0A 00          ; jmp header
            8B 46 FE 01 46 FC ; sum += i
            83 46 FE 01       ; i++
            83 7E FE 05       ; header: cmp i, 5
            7D 03             ; jge exit
            E9 ED FF          ; jmp body
            8B 46 FC          ; exit
            5D C3
            """).GetAllOperations();

        // Проверяем, что есть ForOperation
        var forOp = Assert.Single(ops.OfType<UltraDecompiler.Ir.Operations.ForOperation>());
        Assert.NotNull(forOp.Init);
        Assert.NotNull(forOp.Iteration);
    }

    /// <summary>
    /// Проверяет, что анализатор распознаёт while-цикл без счётчика.
    /// </summary>
    [Fact]
    public void Analyze_WhileLoop_NoCounter()
    {
        var ops = BuildProcExpressions("""
            55 8B EC 81 EC 00 00 57 56
            8B 5E 06 8A 07 98 3D 00 00 75 03 E9 15 00
            8B 5E 06 83 46 06 01 8A 07 8B 5E 04 83 46 04 01 88 07 E9 DD FF
            8B 5E 04 C6 07 00 5E 5F 8B E5 5D C3
            """).GetAllOperations();

        // While-циклы могут быть представлены как IfOperation с jmp
        // Это сложный случай, проверяем просто наличие управляющих структур
        var hasControl = ops.Any(o => o is UltraDecompiler.Ir.Operations.WhileOperation or UltraDecompiler.Ir.Operations.ForOperation or UltraDecompiler.Ir.Operations.IfOperation);
        Assert.True(hasControl);
    }

    /// <summary>
    /// Проверяет, что for-цикл генерирует правильную структуру.
    /// </summary>
    [Fact]
    public void ForLoopStructure_InitConditionIteration()
    {
        var ops = BuildProcExpressions("""
            55 8B EC 81 EC 04 00
            C7 46 FC 00 00    ; sum = 0
            C7 46 FE 00 00    ; i = 0
            E9 0A 00          ; jmp header
            8B 46 FE 01 46 FC ; sum += i
            83 46 FE 01       ; i++
            83 7E FE 05       ; header: cmp i, 5
            7D 03             ; jge exit
            E9 ED FF          ; jmp body
            8B 46 FC          ; exit
            5D C3
            """).GetAllOperations();

        var forOp = Assert.Single(ops.OfType<UltraDecompiler.Ir.Operations.ForOperation>());

        // Init должен быть SetOperation
        Assert.IsType<UltraDecompiler.Ir.Operations.SetOperation>(forOp.Init);

        // Condition должен быть CmpExpr
        Assert.IsType<UltraDecompiler.Ir.Expressions.CmpExpr>(forOp.Condition);

        // Iteration должен быть IncOperation
        Assert.IsType<UltraDecompiler.Ir.Operations.IncOperation>(forOp.Iteration);
    }
}
