namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты поддержки операций записи в память (StoreOperation).
/// </summary>
public class MemoryStoreTests : BaseTests
{
    [Fact(Skip = "NotImplemented")]
    public void Add_ToMemory_Rmw_ProducesAddAssignOperation()
    {
        // ADD [BX], 5
        var expr = BuildExpressions("83 07 05");

        var addAssign = Assert.IsType<AddAssignOperation>(Assert.Single(expr.Blocks[0].Operations));
        Assert.Equal(5, ((ConstExpr)addAssign.Value).Value);
    }

    [Fact(Skip = "NotImplemented")]
    public void Inc_Memory_Destination_ProducesIncOperation()
    {
        // INC word [SI]
        var expr = BuildExpressions("FF 04");

        var inc = Assert.IsType<IncOperation>(Assert.Single(expr.Blocks[0].Operations));
        Assert.NotNull(inc.Segment);
    }

    [Fact]
    public void Store_WithSegmentOverride_ProducesCorrectStore()
    {
        // ES: MOV [BX], AX
        var expr = BuildExpressions("26 89 07");

        var store = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<StoreOperation>(store);

        // Сегмент должен быть ES (или его символическим значением)
        Assert.NotNull(s.Segment);
    }
}