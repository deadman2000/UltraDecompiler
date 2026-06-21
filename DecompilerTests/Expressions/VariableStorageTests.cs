namespace DecompilerTests.Expressions;

/// <summary>
/// Прямые тесты на новый API VariableStorage для поддержки PSP и известных областей памяти.
/// </summary>
public class VariableStorageTests
{
    // PspBase — синглтон _psp с IsInternal
    [Fact]
    public void GetOrCreatePspBase_CreatesOnceAndCaches()
    {
        var storage = new VariableStorage();

        var first = storage.PspBase;
        var second = storage.PspBase;

        Assert.Same(first, second);
        Assert.Equal("_psp", first.Name);
        Assert.True(first.IsInternal);
        Assert.Equal(0, first.Number);
        Assert.NotNull(storage.PspBase);
        Assert.Same(first, storage.PspBase);
    }

    // 0x2C → EnvironmentSegment, 0x80 → CommandTailLength, кэш по смещению
    [Fact]
    public void GetOrCreatePspField_CachesPerOffset()
    {
        var storage = new VariableStorage();

        var env1 = storage.GetOrCreatePspField(0x2C);
        var env2 = storage.GetOrCreatePspField(0x2C);
        var tail = storage.GetOrCreatePspField(0x80);

        Assert.Same(env1, env2);
        Assert.NotSame(env1, tail);
        Assert.Equal("Psp.EnvironmentSegment", env1.Name);
        Assert.Equal("Psp.CommandTailLength", tail.Name);
    }

    // Неизвестное поле → Psp.Field_17
    [Fact]
    public void GetOrCreatePspField_UnknownOffset_CreatesGenericName()
    {
        var storage = new VariableStorage();

        var unknown = storage.GetOrCreatePspField(0x17);

        Assert.Equal("Psp.Field_17", unknown.Name);
    }

    // ActivateStackLocals([-50,-20]) → var1/var2 в порядке объявления
    [Fact]
    public void ActivateStackLocals_CreatesVariablesInDeclarationOrder()
    {
        var storage = new VariableStorage();

        var locals = storage.ActivateStackLocals([-50, -20]);

        Assert.Equal(2, locals.Count);
        Assert.True(locals[0].IsStack);
        Assert.True(locals[1].IsStack);
        Assert.Equal(1, locals[0].Number);
        Assert.Equal(2, locals[1].Number);
        Assert.Equal("var1", locals[0].ToString());
        Assert.Equal("var2", locals[1].ToString());
        Assert.Same(locals[0], storage.TryGetStackLocal(-20));
        Assert.Same(locals[1], storage.TryGetStackLocal(-50));
        Assert.Equal(-50, storage.StackLocals[0].Offset);
        Assert.Equal(-20, storage.StackLocals[1].Offset);
    }

    // var1 и temp1 — разные счётчики имён
    [Fact]
    public void CreateTempVariable_UsesSeparateDisplayCounter()
    {
        var storage = new VariableStorage();

        var stack = storage.CreateStackVariable();
        var temp = storage.CreateTempVariable();

        Assert.True(stack.IsStack);
        Assert.True(temp.IsTemp);
        Assert.Equal("var1", stack.ToString());
        Assert.Equal("temp1", temp.ToString());
        Assert.Equal(1, stack.Number);
        Assert.Equal(1, temp.Number);
        Assert.NotSame(stack, temp);
    }

    // Clear() сбрасывает кэш PSP-переменных
    [Fact]
    public void Clear_ResetsPspState()
    {
        var storage = new VariableStorage();

        var psp1 = storage.PspBase;
        var env = storage.GetOrCreatePspField(0x2C);

        storage.Clear();

        var psp2 = storage.PspBase;
        Assert.NotSame(psp1, psp2);
        Assert.Equal("_psp", psp2.Name);

        // После Clear новое поле должно быть новой переменной
        var env2 = storage.GetOrCreatePspField(0x2C);
        Assert.NotSame(env, env2);
    }

    // Без инициализированного PspBase — подстановка полей невозможна
    [Fact]
    public void TryGetKnownMemoryVariable_ReturnsNull_WhenNoPspBase()
    {
        var storage = new VariableStorage();
        var pspVar = new Variable(Name: "_psp", IsInternal: true);

        var result = storage.TryGetKnownMemoryVariable(new ConstExpr(0x2C), pspVar.ToGet());
        Assert.Null(result);
    }

    // Сегмент не PSP и адрес не _psp+const → null
    [Fact]
    public void TryGetKnownMemoryVariable_ReturnsNull_WhenSegmentDoesNotMatchAndAddressDoesNotContainBase()
    {
        var storage = new VariableStorage();
        var otherVar = new Variable(Name: "other");

        var result = storage.TryGetKnownMemoryVariable(new ConstExpr(0x2C), otherVar.ToGet());
        Assert.Null(result);
    }

    // [_psp+0x2C] → Psp.EnvironmentSegment
    [Fact]
    public void TryGetKnownMemoryVariable_Succeeds_DirectConstWithMatchingSegment()
    {
        var storage = new VariableStorage();
        var psp = storage.PspBase;

        var result = storage.TryGetKnownMemoryVariable(new ConstExpr(0x2C), psp.ToGet());

        Assert.NotNull(result);
        Assert.Equal("Psp.EnvironmentSegment", result.Name);
    }

    // _psp + 0x80 → CommandTailLength
    [Fact]
    public void TryGetKnownMemoryVariable_Succeeds_AddressIsPspPlusConst()
    {
        var storage = new VariableStorage();
        var psp = storage.PspBase;

        var address = new Math2Expr(Math2Operation.Add, psp.ToGet(), new ConstExpr(0x80));
        var result = storage.TryGetKnownMemoryVariable(address, psp.ToGet());

        Assert.NotNull(result);
        Assert.Equal("Psp.CommandTailLength", result.Name);
    }

    // 0x17 — вне каталога известных полей
    [Fact]
    public void TryGetKnownMemoryVariable_ReturnsNull_ForUnknownOffset()
    {
        var storage = new VariableStorage();
        var psp = storage.PspBase;

        var result = storage.TryGetKnownMemoryVariable(new ConstExpr(0x17), psp.ToGet());
        Assert.Null(result);
    }

    // [BX] без константного смещения от PSP
    [Fact]
    public void TryGetKnownMemoryVariable_ReturnsNull_WhenNoConstantOffset()
    {
        var storage = new VariableStorage();
        var psp = storage.PspBase;
        var bx = new Variable(Name: "BX");

        // [BX] без константы относительно PSP
        var result = storage.TryGetKnownMemoryVariable(bx.ToGet(), psp.ToGet());
        Assert.Null(result);
    }
}