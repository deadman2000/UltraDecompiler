using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;

namespace Tests.Expressions;

/// <summary>
/// Тесты ExpressionBuilder для поддержки INT прерываний.
/// Все тесты для INT размещены в этом отдельном файле (как требуется планом).
/// </summary>
public class InterruptTests : BaseTests
{
    [Fact]
    public void Int21h_ProducesDosPrintStringCallExpr()
    {
        // MOV AH, 09h ; INT 21h  (PrintString — non-exit DOS service)
        // AH=09 распознаётся → dos_print_string (см. msdos.h)
        var expr = BuildExpressions("""
            B4 09          ; mov ah, 9
            8C D3          ; mov bx, ds   (just to have another instr)
            CD 21          ; int 21h
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];

        // dos_print_string объявлена как void в msdos.h → CallOperation, без SetOperation
        Assert.Single(block.Operations);

        var callOp = Assert.IsType<CallOperation>(block.Operations[0]);
        Assert.Equal("dos_print_string", callOp.Procedure.Name);

        // Поскольку функция void — мы не перезаписываем AX искусственным результатом
        // (AX может быть испорчен, но мы не создаём для него новую Variable от этого вызова)
    }

    [Fact]
    public void Int10h_ProducesInt86CallExpr()
    {
        // Простой вызов BIOS (INT 10h)
        var expr = BuildExpressions("""
            B4 02          ; mov ah, 2
            CD 10          ; int 10h
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];

        // Для не-21h прерываний (INT 10h и др.) по умолчанию считаем void → CallOperation
        Assert.Single(block.Operations);

        var callOp = Assert.IsType<CallOperation>(block.Operations[0]);
        Assert.Equal("int86", callOp.Procedure.Name);
        Assert.Single(callOp.Args);
        Assert.IsType<ConstExpr>(callOp.Args[0]);

        // Для void-вызовов мы не создаём resultVar и не трогаем AX принудительно
    }

    [Fact]
    public void Int21h_AfterCall_ContinuesWithFurtherInstructions()
    {
        // AH=01 (character input) пока не имеет специализированной обёртки →
        // fallback на intdos (как в оригинальном QuickC <dos.h>).
        var expr = BuildExpressions("""
            B4 01          ; mov ah, 1
            CD 21          ; int 21h      ; non-exit (read char)
            04 30          ; add al, 30h
            """);

        Assert.Single(expr.Blocks);
        var block = expr.Blocks[0];

        // AH=01 → fallback на "intdos" (не void) → SetOperation
        // + последующая арифметика (ADD) → минимум 2 операции
        Assert.True(block.Operations.Count >= 2, "Interrupt should be followed by further operations (ADD after INT 21h)");

        // Проверяем, что после INT есть арифметика
        var lastOp = block.Operations[^1];
        Assert.IsType<SetOperation>(lastOp);
    }

    [Fact]
    public void Int21h_AH3D_ProducesDosOpenCallExpr()
    {
        // MOV AH,3Dh ; MOV AL,0 ; MOV DX,offset ; INT 21h  → dos_open
        var expr = BuildExpressions("""
            B4 3D          ; mov ah, 3Dh
            B0 00          ; mov al, 0
            BA 00 01       ; mov dx, 0100h
            CD 21          ; int 21h
            """);

        var block = expr.Blocks[0];
        // dos_open возвращает handle / ошибку → SetOperation с CallExpr
        Assert.Single(block.Operations);

        var setOp = Assert.IsType<SetOperation>(block.Operations[0]);
        var callExpr = Assert.IsType<CallExpr>(setOp.Src);

        Assert.Equal("dos_open", callExpr.Procedure.Name);
        Assert.NotEmpty(callExpr.Args);   // должно содержать хотя бы имя файла + режим

        // Результат должен попасть в AX
        var ax = Assert.IsType<Variable>(block.EndRegisters.AX);
        Assert.Equal(setOp.Dst.Number, ax.Number);
    }

    [Fact]
    public void Int21h_AH3F_ProducesDosReadCallExpr()
    {
        // MOV AH,3Fh ; MOV BX,handle ; MOV CX,512 ; MOV DX,buffer ; INT 21h → dos_read
        var expr = BuildExpressions("""
            B4 3F          ; mov ah, 3Fh
            BB 05 00       ; mov bx, 5
            B9 00 02       ; mov cx, 512
            BA 00 02       ; mov dx, 0200h
            CD 21          ; int 21h
            """);

        var block = expr.Blocks[0];
        // dos_read возвращает количество прочитанных байт / ошибку → SetOperation
        Assert.Single(block.Operations);

        var setOp = Assert.IsType<SetOperation>(block.Operations[0]);
        var callExpr = Assert.IsType<CallExpr>(setOp.Src);

        Assert.Equal("dos_read", callExpr.Procedure.Name);
        Assert.True(callExpr.Args.Count >= 3, "dos_read должен получать handle + buffer + count");

        var ax = Assert.IsType<Variable>(block.EndRegisters.AX);
        Assert.Equal(setOp.Dst.Number, ax.Number);
    }
}
