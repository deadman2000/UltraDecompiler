using UltraDecompiler.Decompilation;
using UltraDecompiler.Disassembler;

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
        regs = regs.Set16(GpRegister16.AX, axExpr);
        Assert.Null(regs.AH);
        Assert.Null(regs.AL);
        Assert.Equal(axExpr, regs.Get16(GpRegister16.AX));

        // Установка AH - разбиваем X на AL = X & 0xff, X=null
        var ahExpr = new ConstExpr(0x12);
        regs = regs.Set8(GpRegister8.AH, ahExpr);
        Assert.Equal(ahExpr, regs.AH);
        Assert.NotNull(regs.AL); // должна быть LowByte из прежнего X
        Assert.Null(regs.AX);
        Assert.Equal(ahExpr, regs.Get8(GpRegister8.AH));

        // Установка AL - оба установлены, Get16 = (AH<<8)|AL
        var alExpr = new ConstExpr(0x34);
        regs = regs.Set8(GpRegister8.AL, alExpr);
        Assert.Equal(alExpr, regs.AL);
        var combined = regs.Get16(GpRegister16.AX);
        Assert.NotNull(combined);
        // С folding'ом (Calculate в Get16) для констант получаем чистый ConstExpr
        var c = Assert.IsType<ConstExpr>(combined);
        Assert.Equal(0x1234, c.Value);

        // Установка обратно AX - H/L в null
        var newAx = new ConstExpr(0x5678);
        regs = regs.Set16(GpRegister16.AX, newAx);
        Assert.Null(regs.AH);
        Assert.Null(regs.AL);
        Assert.Equal(newAx, regs.Get16(GpRegister16.AX));
    }

    [Fact]
    public void RegisterExpressions_IndexRegisters_SetGet()
    {
        // Тест поддержки индексных регистров (SP, BP, SI, DI) в RegisterExpressions
        var regs = RegisterExpressions.InitZero();

        var spExpr = new ConstExpr(0x1234);
        regs = regs.Set16(GpRegister16.SP, spExpr);
        Assert.Equal(spExpr, regs.SP);
        Assert.Equal(spExpr, regs.Get16(GpRegister16.SP));

        var bpExpr = new ConstExpr(0x5678);
        regs = regs.Set16(GpRegister16.BP, bpExpr);
        Assert.Equal(bpExpr, regs.BP);
        Assert.Equal(bpExpr, regs.Get16(GpRegister16.BP));

        var siExpr = new ConstExpr(0x9ABC);
        regs = regs.Set16(GpRegister16.SI, siExpr);
        Assert.Equal(siExpr, regs.SI);
        Assert.Equal(siExpr, regs.Get16(GpRegister16.SI));

        var diExpr = new ConstExpr(0xDEF0);
        regs = regs.Set16(GpRegister16.DI, diExpr);
        Assert.Equal(diExpr, regs.DI);
        Assert.Equal(diExpr, regs.Get16(GpRegister16.DI));
    }

    [Fact]
    public void RegisterExpressions_SegmentRegisters_SetGet()
    {
        // Тест поддержки сегментных регистров (ES, CS, SS, DS) в RegisterExpressions
        var regs = RegisterExpressions.InitZero();

        var dsExpr = new ConstExpr(0x1000);
        regs = regs.SetSegment(CpuSegmentRegister.DS, dsExpr);
        Assert.Equal(dsExpr, regs.DS);
        Assert.Equal(dsExpr, regs.GetSegment(CpuSegmentRegister.DS));

        var esExpr = new ConstExpr(0x2000);
        regs = regs.SetSegment(CpuSegmentRegister.ES, esExpr);
        Assert.Equal(esExpr, regs.ES);
        Assert.Equal(esExpr, regs.GetSegment(CpuSegmentRegister.ES));

        var csExpr = new ConstExpr(0xF000);
        regs = regs.SetSegment(CpuSegmentRegister.CS, csExpr);
        Assert.Equal(csExpr, regs.CS);
        Assert.Equal(csExpr, regs.GetSegment(CpuSegmentRegister.CS));

        // Проверка совместимости: общие регистры не сломаны
        Assert.Equal(ConstExpr.Zero, regs.AX); // init zero
    }
}
