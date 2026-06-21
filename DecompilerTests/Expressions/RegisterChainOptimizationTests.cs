using TestSupport;

namespace DecompilerTests.Expressions;

/// <summary>Тесты оптимизации цепочек присваиваний через регистры.</summary>
public sealed class RegisterChainOptimizationTests : BaseTests
{
    /// <summary>
    /// Проверяет, что цепочка regAX = const; regDX = regAX оптимизируется в regDX = const.
    /// </summary>
    [Fact]
    public void RegisterChain_Const_Copy_Optimizes()
    {
        // regAX = 1; regDX = regAX → regDX = 1
        var builder = BuildExpressionsChains("""
            B8 01 00  ; MOV AX, 1
            8B D0     ; MOV DX, AX
            C3        ; RET
            """);

        // После оптимизации должно остаться только присваивание regDX = 1
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();

        Assert.Single(sets);
        Assert.True(AssignmentTarget.ReferencesVariable(sets[0].Dst, builder.Variables.DX));
        Assert.Equal(1, ((ConstExpr)sets[0].Src).Value);
    }

    /// <summary>
    /// Проверяет, что цепочка regAX = BX; regDX = regAX оптимизируется в regDX = BX.
    /// </summary>
    [Fact]
    public void RegisterChain_Var_Copy_Optimizes()
    {
        // regAX = BX; regDX = regAX → regDX = BX
        var builder = BuildExpressionsChains("""
            8B C3     ; MOV AX, BX
            8B D0     ; MOV DX, AX
            C3        ; RET
            """);

        // После оптимизации: regDX = BX
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();

        Assert.Single(sets);
        Assert.True(AssignmentTarget.ReferencesVariable(sets[0].Dst, builder.Variables.DX));
        ExprTestHelpers.AssertSameVariable(builder.Variables.BX, sets[0].Src);
    }

    /// <summary>
    /// Проверяет, что цепочка regAX = var1; return regAX оптимизируется в return var1.
    /// </summary>
    [Fact]
    public void RegisterChain_Return_Var_Optimizes()
    {
        // regAX = BX; return regAX → return BX
        var builder = BuildExpressionsChains("""
            8B C3     ; MOV AX, BX
            C3        ; RET
            """);

        // После оптимизации: return BX (без присваивания regAX)
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var returns = builder.Blocks[0].Operations.OfType<ReturnOperation>().ToList();

        Assert.Empty(sets);
        Assert.Single(returns);
        ExprTestHelpers.AssertSameVariable(builder.Variables.BX, returns[0].Value!);
    }

    /// <summary>
    /// Проверяет, что цепочка regAX = const; return regAX оптимизируется в return const.
    /// </summary>
    [Fact]
    public void RegisterChain_Return_Const_Optimizes()
    {
        // regAX = 42; return regAX → return 42
        var builder = BuildExpressionsChains("""
            B8 2A 00  ; MOV AX, 42
            C3        ; RET
            """);

        // После оптимизации: return 42 (без присваивания regAX)
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var returns = builder.Blocks[0].Operations.OfType<ReturnOperation>().ToList();

        Assert.Empty(sets);
        Assert.Single(returns);
        Assert.Equal(42, ((ConstExpr)returns[0].Value!).Value);
    }

    /// <summary>
    /// Проверяет, что цепочка НЕ оптимизируется, если регистр используется в выражении.
    /// </summary>
    [Fact]
    public void RegisterChain_UsedInExpression_NoOptimize()
    {
        // regAX = 1; regBX = 2; regAX = regAX + regBX
        var builder = BuildExpressionsChains("""
            B8 01 00  ; MOV AX, 1
            BB 02 00  ; MOV BX, 2
            01 D8     ; ADD AX, BX
            C3        ; RET
            """);

        // Оптимизация не должна применяться, т.к. regAX используется в выражении
        // (количество операций может быть > 3 из-за особенностей дизассемблера)
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.True(sets.Count >= 3);

        // Проверяем, что ADD AX, BX остался как операция с Math2Expr
        var addOps = sets.Where(s => s.Src is Math2Expr).ToList();
        Assert.NotEmpty(addOps);
    }

    /// <summary>
    /// Проверяет, что цепочка оптимизируется, даже если регистр используется несколько раз.
    /// </summary>
    [Fact]
    public void RegisterChain_MultipleUses_Optimizes()
    {
        // regAX = 1; regBX = regAX; regCX = regAX
        // regAX используется дважды, оптимизация применяет к обоим использованиям
        var builder = BuildExpressionsChains("""
            B8 01 00  ; MOV AX, 1
            8B D8     ; MOV BX, AX
            8B C8     ; MOV CX, AX
            C3        ; RET
            """);

        // После оптимизации: regBX = 1, regCX = 1 (regAX = 1 удалено)
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();
        Assert.Equal(2, sets.Count);

        var bxSet = sets.FirstOrDefault(s => AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.BX));
        var cxSet = sets.FirstOrDefault(s => AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.CX));

        Assert.NotNull(bxSet);
        Assert.Equal(1, ((ConstExpr)bxSet!.Src).Value);
        Assert.NotNull(cxSet);
        Assert.Equal(1, ((ConstExpr)cxSet!.Src).Value);
    }

    /// <summary>
    /// Проверяет обработку переопределения регистра.
    /// </summary>
    [Fact]
    public void RegisterChain_Redefinition_NoOptimize()
    {
        // regAX = 1; regAX = 2; regBX = regAX; return regAX
        // regAX = 1 не оптимизируется (переопределение regAX = 2)
        // regAX = 2 оптимизируется в regBX = 2 и return 2
        var builder = BuildExpressionsChains("""
            B8 01 00  ; MOV AX, 1
            B8 02 00  ; MOV AX, 2
            8B D8     ; MOV BX, AX
            C3        ; RET
            """);

        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var returns = builder.Blocks[0].Operations.OfType<ReturnOperation>().ToList();

        // Проверяем, что есть операция BX = 2
        var bxSet = sets.FirstOrDefault(s =>
            AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.BX));
        Assert.NotNull(bxSet);
        Assert.Equal(2, ((ConstExpr)bxSet!.Src).Value);

        // Проверяем, что return оптимизирован в return 2
        Assert.Single(returns);
        Assert.Equal(2, ((ConstExpr)returns[0].Value!).Value);

        // Проверяем, что осталось regAX = 1 (не оптимизировано из-за переопределения)
        var axSet = sets.FirstOrDefault(s =>
            AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.AX));
        Assert.NotNull(axSet);
        Assert.Equal(1, ((ConstExpr)axSet!.Src).Value);
    }

    #region Тесты со стековыми переменными

    /// <summary>
    /// Проверяет, что цепочка regAX = [BP-2]; regBX = regAX оптимизируется в regBX = var1.
    /// </summary>
    [Fact]
    public void RegisterChain_StackVar_Copy_Optimizes()
    {
        // regAX = var1; regBX = regAX → regBX = var1
        var builder = BuildExpressionsChains("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 02  ; SUB SP, 2
            8B 46 FE  ; MOV AX, [BP-2]
            8B D8     ; MOV BX, AX
            C3        ; RET
            """);

        // После оптимизации: regBX = var1
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();

        var bxSet = sets.FirstOrDefault(s =>
            AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.BX));
        Assert.NotNull(bxSet);
        ExprTestHelpers.AssertSameVariable(builder.Variables.StackLocals[0].Variable, bxSet!.Src);
    }

    /// <summary>
    /// Проверяет, что цепочка regAX = 42; [BP-2] = regAX оптимизируется в [BP-2] = 42.
    /// </summary>
    [Fact]
    public void RegisterChain_Const_ToStackVar_Optimizes()
    {
        // regAX = 42; var1 = regAX → var1 = 42
        var builder = BuildExpressionsChains("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 02  ; SUB SP, 2
            B8 2A 00  ; MOV AX, 42
            89 46 FE  ; MOV [BP-2], AX
            C3        ; RET
            """);

        // После оптимизации: var1 = 42
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();

        // Проверяем, что есть присваивание стековой переменной со значением 42
        var stackSet = sets.FirstOrDefault(s =>
            s.Dst is VariableExpr { Var.IsStack: true });
        Assert.NotNull(stackSet);
        Assert.Equal(42, ((ConstExpr)stackSet!.Src).Value);

        // Проверяем, что присваивание regAX удалено
        var axSets = sets.Where(s =>
            AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.AX)).ToList();
        Assert.Empty(axSets);
    }

    /// <summary>
    /// Проверяет, что цепочка regAX = [BP-2]; [BP-4] = regAX оптимизируется в [BP-4] = var1.
    /// </summary>
    [Fact]
    public void RegisterChain_StackVar_ToStackVar_Optimizes()
    {
        // var1 = regAX; var2 = regAX → var2 = var1
        var builder = BuildExpressionsChains("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 04  ; SUB SP, 4
            8B 46 FE  ; MOV AX, [BP-2]
            89 46 FC  ; MOV [BP-4], AX
            C3        ; RET
            """);

        // После оптимизации: var2 = var1
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();

        // Проверяем, что есть присваивание стековой переменной из другой стековой
        var stackSet = sets.FirstOrDefault(s =>
            s.Dst is VariableExpr { Var.IsStack: true } &&
            s.Src is VariableExpr { Var.IsStack: true });
        Assert.NotNull(stackSet);

        // Проверяем, что присваивание regAX удалено
        var axSets = sets.Where(s =>
            AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.AX)).ToList();
        Assert.Empty(axSets);
    }

    /// <summary>
    /// Проверяет, что цепочка с переопределением и стековыми переменными обрабатывается корректно.
    /// </summary>
    [Fact]
    public void RegisterChain_StackVar_Redefinition_Optimizes()
    {
        // regAX = [BP-2]; regAX = 42; [BP-4] = regAX; return regAX
        // regAX = var1 не оптимизируется (переопределение regAX = 42)
        // regAX = 42 оптимизируется в [BP-4] = 42 и return 42
        var builder = BuildExpressionsChains("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 04  ; SUB SP, 4
            8B 46 FE  ; MOV AX, [BP-2]
            B8 2A 00  ; MOV AX, 42
            89 46 FC  ; MOV [BP-4], AX
            C3        ; RET
            """);

        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var returns = builder.Blocks[0].Operations.OfType<ReturnOperation>().ToList();

        // Проверяем, что есть операция [BP-4] = 42
        var stackSet = sets.FirstOrDefault(s =>
            s.Dst is VariableExpr { Var.IsStack: true } &&
            s.Src is ConstExpr { Value: 42 });
        Assert.NotNull(stackSet);

        // Проверяем, что return оптимизирован в return 42
        Assert.Single(returns);
        Assert.Equal(42, ((ConstExpr)returns[0].Value!).Value);

        // Проверяем, что осталось regAX = var1 (не оптимизировано из-за переопределения)
        var axSets = sets.Where(s =>
            AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.AX)).ToList();
        Assert.NotEmpty(axSets);
    }

    /// <summary>
    /// Проверяет, что цепочка с несколькими использованиями стековой переменной оптимизируется.
    /// </summary>
    [Fact]
    public void RegisterChain_StackVar_MultipleUses_Optimizes()
    {
        // regAX = [BP-2]; [BP-4] = regAX; [BP-6] = regAX
        // [BP-4] = var1; [BP-6] = var1
        var builder = BuildExpressionsChains("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 06  ; SUB SP, 6
            8B 46 FE  ; MOV AX, [BP-2]
            89 46 FC  ; MOV [BP-4], AX
            89 46 FA  ; MOV [BP-6], AX
            C3        ; RET
            """);

        // После оптимизации: [BP-4] = var1, [BP-6] = var1
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();

        // Проверяем, что есть 2 присваивания стековых переменных из var1
        var stackSets = sets.Where(s =>
            s.Dst is VariableExpr { Var.IsStack: true } &&
            s.Src is VariableExpr { Var.IsStack: true }).ToList();

        Assert.Equal(2, stackSets.Count);

        // Проверяем, что присваивание regAX удалено
        var axSets = sets.Where(s =>
            AssignmentTarget.ReferencesVariable(s.Dst, builder.Variables.AX)).ToList();
        Assert.Empty(axSets);
    }

    /// <summary>
    /// Проверяет, что цепочка regAX = [BP-2]; return regAX оптимизируется в return var1.
    /// </summary>
    [Fact]
    public void RegisterChain_StackVar_Return_Optimizes()
    {
        // regAX = var1; return regAX → return var1
        var builder = BuildExpressionsChains("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 02  ; SUB SP, 2
            8B 46 FE  ; MOV AX, [BP-2]
            C3        ; RET
            """);

        // После оптимизации: return var1 (без присваивания regAX)
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var returns = builder.Blocks[0].Operations.OfType<ReturnOperation>().ToList();

        Assert.Empty(sets);
        Assert.Single(returns);
        ExprTestHelpers.AssertSameVariable(builder.Variables.StackLocals[0].Variable, returns[0].Value!);
    }

    /// <summary>
    /// Проверяет, что цепочка regAX = 42; return regAX оптимизируется в return 42.
    /// </summary>
    [Fact]
    public void RegisterChain_Const_Return_Optimizes()
    {
        // regAX = 42; return regAX → return 42
        var builder = BuildExpressionsChains("""
            55        ; PUSH BP
            8B EC     ; MOV BP, SP
            83 EC 02  ; SUB SP, 2
            B8 2A 00  ; MOV AX, 42
            C3        ; RET
            """);

        // После оптимизации: return 42 (без присваивания regAX)
        var sets = builder.Blocks[0].Operations.OfType<SetOperation>().ToList();
        var returns = builder.Blocks[0].Operations.OfType<ReturnOperation>().ToList();

        Assert.Empty(sets);
        Assert.Single(returns);
        Assert.Equal(42, ((ConstExpr)returns[0].Value!).Value);
    }

    #endregion

    #region Тесты на основе incdec.c

    /// <summary>
    /// Интеграционный тест: декомпиляция incdec.c должна содержать правильные инкременты/декременты.
    /// Проверяет порядок и типы операций в соответствии с исходным кодом:
    ///   int a = 10;
    ///   a = a + 1;      // явное сложение
    ///   a++;            // пост-инкремент
    ///   a += 1;         // сложение с присваиванием
    ///   b = ++a;        // пре-инкремент с присваиванием
    ///   b = a++;        // пост-инкремент с присваиванием
    ///   a = a - 1;      // явное вычитание
    ///   a--;            // пост-декремент
    ///   a -= 1;         // вычитание с присваиванием
    ///   return b;
    /// </summary>
    [Fact]
    public void IncDec_IncdecC_IntegrationTest()
    {
        var procedures = DecompileTestHelper.GetExampleIR("incdec.c");
        var proc = Assert.Single(procedures);
        Assert.NotNull(proc.Expressions);
        var block = Assert.Single(proc.Expressions.Blocks);
        
        var ops = block.Operations.ToList();
        
        // 0: a = 10 (инициализация)
        var op0 = Assert.IsType<SetOperation>(ops[0]);
        Assert.True(AssignmentTarget.TryGetVariable(op0.Dst, out var v0) && v0.Name == "var1");
        Assert.IsType<ConstExpr>(op0.Src);

        // 1: a = a + 1 (явное сложение)
        var op1 = Assert.IsType<SetOperation>(ops[1]);
        Assert.True(AssignmentTarget.TryGetVariable(op1.Dst, out var v1) && v1.Name == "var1");
        var add1 = Assert.IsType<Math2Expr>(op1.Src);
        Assert.Equal(Math2Operation.Add, add1.Operation);

        // 2: a++ (пост-инкремент как отдельная операция)
        var op2 = Assert.IsType<IncOperation>(ops[2]);
        Assert.True(AssignmentTarget.TryGetVariable(op2.Target, out var v2) && v2.Name == "var1");

        // 3: a += 1 (сложение с присваиванием → IncOperation)
        var op3 = Assert.IsType<IncOperation>(ops[3]);
        Assert.True(AssignmentTarget.TryGetVariable(op3.Target, out var v3) && v3.Name == "var1");

        // 4: b = ++a (пре-инкремент в присваивании)
        var op4 = Assert.IsType<SetOperation>(ops[4]);
        Assert.True(AssignmentTarget.TryGetVariable(op4.Dst, out var v4) && v4.Name == "var2");
        var preInc = Assert.IsType<Math1Expr>(op4.Src);
        Assert.Equal(Math1Operation.PreIncrement, preInc.Operation);

        // 5: b = a++ (пост-инкремент в присваивании)
        var op5 = Assert.IsType<SetOperation>(ops[5]);
        Assert.True(AssignmentTarget.TryGetVariable(op5.Dst, out var v5) && v5.Name == "var2");
        var postInc = Assert.IsType<Math1Expr>(op5.Src);
        Assert.Equal(Math1Operation.PostIncrement, postInc.Operation);

        // 6: a = a - 1 (явное вычитание)
        var op6 = Assert.IsType<SetOperation>(ops[6]);
        Assert.True(AssignmentTarget.TryGetVariable(op6.Dst, out var v6) && v6.Name == "var1");
        var sub1 = Assert.IsType<Math2Expr>(op6.Src);
        Assert.Equal(Math2Operation.Sub, sub1.Operation);

        // 7: a-- (пост-декремент как отдельная операция)
        var op7 = Assert.IsType<DecOperation>(ops[7]);
        Assert.True(AssignmentTarget.TryGetVariable(op7.Target, out var v7) && v7.Name == "var1");

        // 8: a -= 1 (вычитание с присваиванием → DecOperation)
        var op8 = Assert.IsType<DecOperation>(ops[8]);
        Assert.True(AssignmentTarget.TryGetVariable(op8.Target, out var v8) && v8.Name == "var1");

        // 9: return b
        var op9 = Assert.IsType<ReturnOperation>(ops[9]);
        Assert.IsType<VariableExpr>(op9.Value);
    }

    #endregion

    private static ExpressionBuilder BuildExpressionsChains(string hex)
    {
        var builder = BuildExpressionsRaw(hex);
        builder.OptimizeEpilogue();
        builder.OptimizeRegisterChains();
        return builder;
    }
}

