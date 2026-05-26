using UltraDecompiler.Decompilation;

namespace Tests.Registers;

/// <summary>
/// Тесты внутренней логики RegisterExpressions (Decompilation).
/// </summary>
public class ExpressionsTests : BaseTests
{
    [Fact]
    public void RegisterExpressions_8bit_SetGet_Logic()
    {
        // Тест новой логики 8-битных регистров в Decompilation.RegisterExpressions
        var regs = RegisterExpressions.InitZero();

        // Установка 16-bit AX
        var axExpr = new ConstExpr(0x1234);
        regs = regs.Set16(0, axExpr);
        Assert.Null(regs.AH);
        Assert.Null(regs.AL);
        Assert.Equal(axExpr, regs.Get16(0));

        // Установка AH - разбиваем X на AL = X & 0xff, X=null
        var ahExpr = new ConstExpr(0x12);
        regs = regs.Set8(4, ahExpr); // AH=4
        Assert.Equal(ahExpr, regs.AH);
        Assert.NotNull(regs.AL); // должна быть LowByte из прежнего X
        Assert.Null(regs.AX);
        Assert.Equal(ahExpr, regs.Get8(4));

        // Установка AL - оба установлены, Get16 = (AH<<8)|AL
        var alExpr = new ConstExpr(0x34);
        regs = regs.Set8(0, alExpr); // AL=0
        Assert.Equal(alExpr, regs.AL);
        var combined = regs.Get16(0);
        Assert.NotNull(combined);
        // проверка типа выражения
        Assert.Contains("<< 8", combined.ToString());
        Assert.Contains("| ", combined.ToString());

        // Установка обратно AX - H/L в null
        var newAx = new ConstExpr(0x5678);
        regs = regs.Set16(0, newAx);
        Assert.Null(regs.AH);
        Assert.Null(regs.AL);
        Assert.Equal(newAx, regs.Get16(0));
    }

    [Fact]
    public void RegisterExpressions_IndexRegisters_SetGet()
    {
        // Тест поддержки индексных регистров (SP, BP, SI, DI) в RegisterExpressions
        var regs = RegisterExpressions.InitZero();

        var spExpr = new ConstExpr(0x1234);
        regs = regs.Set16(4, spExpr);
        Assert.Equal(spExpr, regs.SP);
        Assert.Equal(spExpr, regs.Get16(4));

        var bpExpr = new ConstExpr(0x5678);
        regs = regs.Set16(5, bpExpr);
        Assert.Equal(bpExpr, regs.BP);
        Assert.Equal(bpExpr, regs.Get16(5));

        var siExpr = new ConstExpr(0x9ABC);
        regs = regs.Set16(6, siExpr);
        Assert.Equal(siExpr, regs.SI);
        Assert.Equal(siExpr, regs.Get16(6));

        var diExpr = new ConstExpr(0xDEF0);
        regs = regs.Set16(7, diExpr);
        Assert.Equal(diExpr, regs.DI);
        Assert.Equal(diExpr, regs.Get16(7));
    }

    [Fact]
    public void RegisterExpressions_SegmentRegisters_SetGet()
    {
        // Тест поддержки сегментных регистров (ES, CS, SS, DS) в RegisterExpressions
        var regs = RegisterExpressions.InitZero();

        var dsExpr = new ConstExpr(0x1000);
        regs = regs.SetSegment(3, dsExpr);
        Assert.Equal(dsExpr, regs.DS);
        Assert.Equal(dsExpr, regs.GetSegment(3));

        var esExpr = new ConstExpr(0x2000);
        regs = regs.SetSegment(0, esExpr);
        Assert.Equal(esExpr, regs.ES);
        Assert.Equal(esExpr, regs.GetSegment(0));

        var csExpr = new ConstExpr(0xF000);
        regs = regs.SetSegment(1, csExpr);
        Assert.Equal(csExpr, regs.CS);
        Assert.Equal(csExpr, regs.GetSegment(1));

        // Проверка совместимости: общие регистры не сломаны
        Assert.Equal(ConstExpr.Zero, regs.AX); // init zero
    }
}
