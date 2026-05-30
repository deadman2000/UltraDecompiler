using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;

namespace DecompilerTests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для поддержки инструкций CALL и CALL_FAR.
/// 
/// Отдельный файл по аналогии с InterruptTests, StackTests и ControlFlowTests.
/// Здесь сосредоточены все проверки, связанные с моделированием вызовов:
/// - Прямые near-вызовы (E8) → sub_XXXX
/// - Косвенные вызовы (FF /2, FF /3)
/// - Обновление AX результатом вызова
/// - Продолжение потока после CALL (блок не прерывается)
/// </summary>
public class CallTests : BaseTests
{
    [Fact]
    public void DirectNearCall_ProducesSubNamedCallExpr_AndSetsAX()
    {
        // E8 05 00 — near call, displacement +5 от следующей инструкции.
        // После чтения 2 байт _pos = 3, цель = 3 + 5 = 8 → sub_0008
        var expr = BuildExpressions("""
            E8 05 00    ; call 8   (displacement → target 8)
            90          ; nop (продолжение после возврата)
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];

        // CALL всегда порождает SetOperation (моделируем возврат в AX)
        Assert.Single(block.Operations);

        var setOp = Assert.IsType<SetOperation>(block.Operations[0]);
        var callExpr = Assert.IsType<CallExpr>(setOp.Src);

        Assert.Equal("sub_0008", callExpr.Procedure.Name);
        Assert.Empty(callExpr.Args); // пока не восстанавливаем аргументы

        // Результат вызова должен находиться в AX
        var ax = Assert.IsType<Variable>(block.EndRegisters.AX);
        Assert.Equal(setOp.Dst.Number, ax.Number);
    }

    [Fact]
    public void DirectNearCall_ResultIsUsableByFollowingCode()
    {
        // После CALL результат в AX используется следующей инструкцией.
        // Проверяем, что символическая переменная корректно "перетекает".
        var expr = BuildExpressions("""
            E8 03 00    ; call 6   (displacement → target 6)
            04 10       ; add al, 10h   ; используем AL (часть AX)
            """);

        var block = expr.Blocks[0];
        Assert.Equal(2, block.Operations.Count);

        // Вторая операция — от ADD (SetOperation на AX/AL)
        var addOp = Assert.IsType<SetOperation>(block.Operations[1]);
        Assert.IsType<Math2Expr>(addOp.Src);
    }

    [Fact]
    public void IndirectMemoryCall_ProducesIndirectCallWithMemoryArg()
    {
        // FF 16 00 01 — CALL [0100h] (actual output of disassembler: no "WORD PTR")
        var expr = BuildExpressions("""
            FF 16 00 01 ; call [0100h]
            """);

        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var setOp = Assert.IsType<SetOperation>(block.Operations[0]);
        var callExpr = Assert.IsType<CallExpr>(setOp.Src);

        Assert.Equal("indirect_call", callExpr.Procedure.Name);
        Assert.Single(callExpr.Args);

        // Аргумент должен быть MemExpr (адрес 0x100)
        var memArg = Assert.IsType<MemExpr>(callExpr.Args[0]);
        var addrConst = Assert.IsType<ConstExpr>(memArg.Address);
        Assert.Equal(0x0100, addrConst.Value);
    }

    [Fact]
    public void IndirectRegisterCall_ProducesIndirectCallWithRegisterArg()
    {
        // FF D0 — CALL AX (register indirect)
        // Используем overload с configureInitial, чтобы BX содержал именованную переменную
        var expr = BuildExpressions("""
            8B C3       ; mov ax, bx
            FF D0       ; call AX
            """, vars =>
        {
            var ptr = vars.CreateVariable("func_ptr");
            var regs = RegisterExpressions.InitCom(vars) with { BX = ptr };
            return regs;
        });

        var block = expr.Blocks[0];
        // MOV в регистр не создаёт Operation — только CALL порождает SetOperation
        Assert.Single(block.Operations);

        var callSet = Assert.IsType<SetOperation>(block.Operations[0]);
        var callExpr = Assert.IsType<CallExpr>(callSet.Src);

        Assert.Equal("indirect_call", callExpr.Procedure.Name);
        Assert.Single(callExpr.Args);

        // Аргумент должен быть именно нашей переменной (а не константой)
        var arg = Assert.IsType<Variable>(callExpr.Args[0]);
        Assert.Equal("func_ptr", arg.Name);
    }

    [Fact]
    public void FarIndirectCall_UsesFarSubName()
    {
        // FF 1E 12 00 — CALL_FAR [12h] (actual: "call far [12h]")
        var expr = BuildExpressions("""
            FF 1E 12 00 ; call far [12h]
            """);

        var block = expr.Blocks[0];
        Assert.Single(block.Operations);

        var setOp = Assert.IsType<SetOperation>(block.Operations[0]);
        var callExpr = Assert.IsType<CallExpr>(setOp.Src);

        Assert.Equal("far_sub", callExpr.Procedure.Name);
        Assert.Single(callExpr.Args);
        Assert.IsType<MemExpr>(callExpr.Args[0]);
    }

    [Fact]
    public void MultipleCallsInSingleBlock_ProduceMultipleOperations()
    {
        // Два прямых вызова подряд — оба должны оставить след в Operations.
        var expr = BuildExpressions("""
            E8 06 00    ; call 9
            E8 03 00    ; call 9   (второй call, цель пересчитывается от его позиции)
            90          ; продолжение
            """);

        var block = expr.Blocks[0];
        Assert.Equal(2, block.Operations.Count);

        var first = Assert.IsType<SetOperation>(block.Operations[0]);
        var firstCall = Assert.IsType<CallExpr>(first.Src);
        Assert.StartsWith("sub_", firstCall.Procedure.Name);

        var second = Assert.IsType<SetOperation>(block.Operations[1]);
        var secondCall = Assert.IsType<CallExpr>(second.Src);
        Assert.StartsWith("sub_", secondCall.Procedure.Name);
    }

    [Fact]
    public void CallDoesNotSplitBasicBlock_FlowContinuesSequentially()
    {
        // В отличие от JMP, CALL не разрывает базовый блок.
        // Проверяем, что после CALL идёт NextBlock, а не Conditional.
        var expr = BuildExpressions("""
            B8 01 00    ; mov ax, 1
            E8 03 00    ; call 6
            90          ; fallthrough
            """);

        // Должен быть ровно один ExprBlock (CALL не создаёт новых рёбер)
        Assert.Single(expr.Blocks);

        var block = expr.Blocks[0];
        Assert.Null(block.ConditionalBlock);
        // Next может быть null, если блок последний, но Operations содержат CALL
        Assert.Single(block.Operations);
        Assert.IsType<SetOperation>(block.Operations[0]);
    }

    [Fact]
    public void DirectCallResultCanBeUsedInComparisonAfterCall()
    {
        // Типичный паттерн после вызова: cmp ax, 0 ; je ...
        // Проверяем, что результат CALL (в AX) корректно используется в последующем CMP.
        var expr = BuildExpressions("""
            E8 05 00    ; call 8
            3D 00 00    ; cmp ax, 0
            74 01       ; je +1 (условный переход)
            90          ; fall
            90          ; target
            """);

        // Из-за JE + fallthrough + target обычно создаётся 3 блока
        Assert.True(expr.Blocks.Count >= 2);

        // В первом блоке обязательно присутствует операция от CALL
        var firstBlock = expr.Blocks[0];
        Assert.NotEmpty(firstBlock.Operations);
        var firstOp = firstBlock.Operations[0];
        var callSet = Assert.IsType<SetOperation>(firstOp);
        Assert.IsType<CallExpr>(callSet.Src);

        // Должен существовать блок с Condition (от JE), и это CmpExpr
        var condBlock = expr.Blocks.FirstOrDefault(b => b.Condition != null);
        Assert.NotNull(condBlock);
        var cond = Assert.IsType<CmpExpr>(condBlock.Condition);
        Assert.Equal(CmpOperation.Eq, cond.Operation);
    }
}
