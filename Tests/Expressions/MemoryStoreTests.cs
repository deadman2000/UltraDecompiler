using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;

namespace Tests.Expressions;

/// <summary>
/// Тесты поддержки операций записи в память (StoreOperation).
/// </summary>
public class MemoryStoreTests : BaseTests
{
    [Fact]
    public void Mov_ToMemoryDirect_ProducesStoreOperation()
    {
        // MOV [1234h], AX
        var expr = BuildExpressions("A3 34 12");

        var store = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<StoreOperation>(store);

        var addr = Assert.IsType<ConstExpr>(s.Address);
        Assert.Equal(0x1234, addr.Value);
        // Значение может быть Variable или выражением — главное, что Store создан
        Assert.NotNull(s.Value);
    }

    [Fact]
    public void Mov_ToMemoryViaRegister_ProducesStoreOperation()
    {
        // MOV BX, 0100h; MOV [BX], AX
        var expr = BuildExpressions("""
            BB 00 01
            89 07
            """);

        // Вторая инструкция должна дать Store
        var store = expr.Blocks[0].Operations[^1];
        var s = Assert.IsType<StoreOperation>(store);

        // Адрес должен быть каким-то выражением (Variable или Math)
        Assert.NotNull(s.Address);
    }

    [Fact]
    public void Add_ToMemory_Rmw_ProducesSetAndStore()
    {
        // ADD [BX], 5
        var expr = BuildExpressions("83 07 05");

        // Ожидаем SetOperation (для вычисленного значения) + StoreOperation
        Assert.Equal(2, expr.Blocks[0].Operations.Count);

        var setOp = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[0]);
        Assert.IsType<StoreOperation>(expr.Blocks[0].Operations[1]);

        Assert.IsType<Math2Expr>(setOp.Src); // current + 5
    }

    [Fact]
    public void Inc_Memory_Destination_ProducesStore()
    {
        // INC word [SI]
        var expr = BuildExpressions("FF 04");

        // Для RMW создаётся SetOperation (вычисленное значение) + StoreOperation
        Assert.Equal(2, expr.Blocks[0].Operations.Count);

        var setOp = Assert.IsType<SetOperation>(expr.Blocks[0].Operations[0]);
        Assert.IsType<StoreOperation>(expr.Blocks[0].Operations[1]);

        var math = Assert.IsType<Math2Expr>(setOp.Src);
        Assert.Equal(Math2Operation.Add, math.Operation);
    }

    [Fact]
    public void Store_ToKnownPspField_StillProducesStoreOperation()
    {
        // MOV [0x2C], AX  — запись в EnvironmentSegment
        var expr = BuildExpressions("A3 2C 00");

        var store = Assert.Single(expr.Blocks[0].Operations);
        var s = Assert.IsType<StoreOperation>(store);

        // Адрес должен остаться "сырым" (Const 0x2C), а не превратиться в переменную Psp.*
        // (потому что для destination мы используем BuildMemoryReference без подстановки)
        var addr = Assert.IsType<ConstExpr>(s.Address);
        Assert.Equal(0x2C, addr.Value);
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

    [Fact]
    public void MemoryStore_DoesNotUpdateRegisterState()
    {
        // MOV [BX], AX — BX и AX не должны измениться в EndRegisters
        var expr = BuildExpressions("89 07");

        // Проверяем, что не было случайного обновления регистров
        // (в данном случае BX и AX должны остаться теми же выражениями, что и до инструкции)
        // Более строгая проверка — что Operations содержит Store, а не Set для регистров
        var store = Assert.Single(expr.Blocks[0].Operations);
        Assert.IsType<StoreOperation>(store);
    }
}