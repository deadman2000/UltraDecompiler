namespace DecompilerTests.Decompilation;

/// <summary>
/// Тесты кодогенерации для битовых полей (паттерн bits.c).
/// </summary>
public class BitsCodegenTests : BaseTests
{
    // Паттерн bits.c: из одного слова извлекаются три битовых поля и передаются в printf.
    // Регрессия: все три push не должны схлопнуться в один адрес (618, 618, 618).
    // Ожидаемый вызов: printf("%u %u %u\n", ready, mode, count) — три разных Expr.
    [Fact(Skip = "NotImplemented")]
    public void BitfieldPrintfArgs_RegisterPushesAfterFieldExtract_StayDistinct()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));
        var catalog = HeaderCatalog.Load(includeDir);
        Assert.True(catalog.TryGetProcedureSignature("printf", out var printfSig));

        var storage = new ProcedureStorage();
        storage.Add(new DisassembledProcedure
        {
            Offset = 0x3C,
            Instructions = [],
            Name = "printf",
            IsLibrary = true,
            Signature = printfSig!,
        });

        var expr = BuildExpressions("""
            55              ; push bp
            8B EC           ; mov bp, sp
            81 EC 04 00     ; sub sp, 4
            8B 46 FE        ; mov ax, [bp-2]
            25 01 00        ; and ax, 1
            50              ; push ax
            8B 46 FE        ; mov ax, [bp-2]
            D1 E8           ; shr ax, 1
            25 07 00        ; and ax, 7
            50              ; push ax
            8B 46 FE        ; mov ax, [bp-2]
            D1 E8           ; shr ax, 1
            D1 E8           ; shr ax, 1
            D1 E8           ; shr ax, 1
            D1 E8           ; shr ax, 1
            25 0F 00        ; and ax, 0Fh
            50              ; push ax
            B8 6A 02        ; mov ax, 26Ah
            50              ; push ax
            E8 0F 00        ; call 3Ch
            C3              ; ret
            """);

        CallSiteResolver.ResolveBlocks(expr.Blocks, storage);

        var callExpr = CreateFlattener(expr).GetAllOperations()
            .OfType<SetOperation>()
            .Select(s => s.Src)
            .OfType<CallExpr>()
            .First(c => c.Name == "printf");
        Assert.Equal("printf", callExpr.Name);
        Assert.Equal(4, callExpr.Args.Count);
        Assert.Equal(0x26A, Assert.IsType<ConstExpr>(callExpr.Args[0]).Value);

        // Регистровые push не должны схлопываться в одно значение (баг bits.c: 618, 618, 618).
        Assert.NotEqual(callExpr.Args[1], callExpr.Args[2]);
        Assert.NotEqual(callExpr.Args[2], callExpr.Args[3]);
        Assert.DoesNotContain(callExpr.Args, a => a is ConstExpr { Value: 0x26A } && !ReferenceEquals(a, callExpr.Args[0]));
    }
}
