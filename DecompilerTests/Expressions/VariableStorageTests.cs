using UltraDecompiler.Decompilation;

namespace DecompilerTests.Expressions;

/// <summary>
/// Прямые тесты на новый API VariableStorage для поддержки PSP и известных областей памяти.
/// </summary>
public class VariableStorageTests
{
    [Fact]
    public void GetOrCreatePspBase_CreatesOnceAndCaches()
    {
        var storage = new VariableStorage();

        var first = storage.PspBase;
        var second = storage.PspBase;

        Assert.Same(first, second);
        Assert.Equal("_psp", first.Name);
        Assert.NotNull(storage.PspBase);
        Assert.Same(first, storage.PspBase);
    }

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

    [Fact]
    public void GetOrCreatePspField_UnknownOffset_CreatesGenericName()
    {
        var storage = new VariableStorage();

        var unknown = storage.GetOrCreatePspField(0x17);

        Assert.Equal("Psp.Field_17", unknown.Name);
    }

    [Fact]
    public void ActivateStackLocals_CreatesVariablesInDeclarationOrder()
    {
        var storage = new VariableStorage();

        var locals = storage.ActivateStackLocals([-50, -20]);

        Assert.Equal(2, locals.Count);
        Assert.True(locals[0].Number < locals[1].Number);
        Assert.Same(locals[0], storage.TryGetStackLocal(-20));
        Assert.Same(locals[1], storage.TryGetStackLocal(-50));
        Assert.Equal(-50, storage.StackLocals[0].Offset);
        Assert.Equal(-20, storage.StackLocals[1].Offset);
    }

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

    [Fact]
    public void TryGetKnownMemoryVariable_ReturnsNull_WhenNoPspBase()
    {
        var storage = new VariableStorage();
        var pspVar = new Variable { Name = "_psp" }; // искусственная база

        var result = storage.TryGetKnownMemoryVariable(new ConstExpr(0x2C), pspVar);
        Assert.Null(result);
    }

    [Fact]
    public void TryGetKnownMemoryVariable_ReturnsNull_WhenSegmentDoesNotMatchAndAddressDoesNotContainBase()
    {
        var storage = new VariableStorage();
        var otherVar = new Variable { Name = "other" };

        var result = storage.TryGetKnownMemoryVariable(new ConstExpr(0x2C), otherVar);
        Assert.Null(result);
    }

    [Fact]
    public void TryGetKnownMemoryVariable_Succeeds_DirectConstWithMatchingSegment()
    {
        var storage = new VariableStorage();
        var psp = storage.PspBase;

        var result = storage.TryGetKnownMemoryVariable(new ConstExpr(0x2C), psp);

        Assert.NotNull(result);
        Assert.Equal("Psp.EnvironmentSegment", result.Name);
    }

    [Fact]
    public void TryGetKnownMemoryVariable_Succeeds_AddressIsPspPlusConst()
    {
        var storage = new VariableStorage();
        var psp = storage.PspBase;

        var address = new Math2Expr(Math2Operation.Add, psp, new ConstExpr(0x80));
        var result = storage.TryGetKnownMemoryVariable(address, psp);

        Assert.NotNull(result);
        Assert.Equal("Psp.CommandTailLength", result.Name);
    }

    [Fact]
    public void TryGetKnownMemoryVariable_ReturnsNull_ForUnknownOffset()
    {
        var storage = new VariableStorage();
        var psp = storage.PspBase;

        var result = storage.TryGetKnownMemoryVariable(new ConstExpr(0x17), psp);
        Assert.Null(result);
    }

    [Fact]
    public void TryGetKnownMemoryVariable_ReturnsNull_WhenNoConstantOffset()
    {
        var storage = new VariableStorage();
        var psp = storage.PspBase;
        var bx = new Variable { Name = "BX" };

        // [BX] без константы относительно PSP
        var result = storage.TryGetKnownMemoryVariable(bx, psp);
        Assert.Null(result);
    }
}