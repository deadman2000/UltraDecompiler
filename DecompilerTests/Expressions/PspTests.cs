using UltraDecompiler.Decompilation;

namespace Tests.Expressions;

public class PspTests : BaseTests
{
    [Fact]
    public void PspRecognition_DirectEnvironmentSegment()
    {
        // MOV AX, [002Ch]  — при DS = PSP
        var expr = BuildExpressions("8B 06 2C 00");   // MOV AX, [002Ch]

        var ax = expr.Blocks[0].EndRegisters.AX;

        // Должны получить красивую именованную переменную вместо MemExpr
        var pspField = Assert.IsType<Variable>(ax);
        Assert.Equal("Psp.EnvironmentSegment", pspField.Name);
    }

    [Fact]
    public void PspRecognition_CommandTailLength_And_Tail()
    {
        // MOV AL, [0x80]; MOV CL, [0x81]
        var expr = BuildExpressions("""
            A0 80 00   ; mov al, [0080h]
            8A 0E 81 00; mov cl, [0081h]
            """);

        var al = Assert.IsType<Variable>(expr.Blocks[0].EndRegisters.AL);
        var cl = Assert.IsType<Variable>(expr.Blocks[0].EndRegisters.CL);

        Assert.Equal("Psp.CommandTailLength", al.Name);
        Assert.Equal("Psp.CommandTail", cl.Name);
    }

    [Fact]
    public void PspRecognition_ViaRegister_BxLoadedWithKnownOffset()
    {
        // MOV BX, 2Ch; MOV AX, [BX]
        var expr = BuildExpressions("""
            BB 2C 00   ; mov bx, 002Ch
            8B 07      ; mov ax, [bx]
            """);

        var ax = expr.Blocks[0].EndRegisters.AX;
        var pspField = Assert.IsType<Variable>(ax);
        Assert.Equal("Psp.EnvironmentSegment", pspField.Name);
    }

    [Fact]
    public void PspRecognition_ComMode_AlsoWorks()
    {
        // Для .COM PSP-база тоже должна работать (DS = PSP)
        var expr = BuildExpressions("8B 06 2C 00", isCom: true);  // MOV AX, [2Ch]

        var ax = expr.Blocks[0].EndRegisters.AX;
        var pspField = Assert.IsType<Variable>(ax);
        Assert.Equal("Psp.EnvironmentSegment", pspField.Name);
    }

    [Fact]
    public void PspRecognition_UnknownOffset_DoesNotReplace()
    {
        // Обращение к неизвестному смещению PSP не должно подменяться
        var expr = BuildExpressions("8B 06 17 00"); // MOV AX, [0017h]

        var ax = expr.Blocks[0].EndRegisters.AX;
        // Должны получить обычный MemExpr
        Assert.IsType<MemExpr>(ax);
    }
}
