namespace DecompilerTests.Expressions;

/// <summary>
/// Распознавание циклов QuickC /Ox со счётчиком в регистре SI (forlp.c).
/// </summary>
public sealed class OxLoopRecognitionTests : BaseTests
{
    // QuickC/PROGRAMS/forlp.c — sum_for: acc на [bp-4], счётчик в SI, spill в [bp-2].
    // Ожидаем: var2=0; for (var1=0; var1&lt;5; var1++) var2 += var1; return var2.
    [Fact]
    public void OxRegisterCounterLoop_SumFor_RecognizedAsFor()
    {
        var builder = BuildProcExpressionsOpt("""
            55                ; push bp
            8B EC             ; mov bp, sp
            81 EC 04 00       ; sub sp, 4
            56                ; push si
            C7 46 FC 00 00    ; var2 = 0
            BE 00 00          ; si = 0
            EB 04             ; jmp header
            01 76 FC          ; body: var2 += si
            46                ; inc si
            83 FE 05          ; header: cmp si, 5
            7C F7             ; jl body
            89 76 FE          ; spill si → [bp-2]
            8B 46 FC          ; return var2
            5E                ; pop si
            8B E5             ; mov sp, bp
            5D                ; pop bp
            C3                ; ret
            """);

        var loop = Assert.Single(builder.GetAllOperations().OfType<ForOperation>());
        _ = Assert.IsType<SetOperation>(loop.Init);
        Assert.IsType<IncOperation>(loop.Iteration);
        Assert.IsType<CmpExpr>(loop.Condition);

        Assert.IsType<AddAssignOperation>(Assert.Single(loop.Body));
    }

    // QuickC/PROGRAMS/forlp.c — countdown_for: for (i=3; i&gt;0; i--) acc += i.
    [Fact]
    public void OxRegisterCounterLoop_CountdownFor_RecognizedAsFor()
    {
        var builder = BuildProcExpressionsOpt("""
            55                ; push bp
            8B EC             ; mov bp, sp
            81 EC 04 00       ; sub sp, 4
            56                ; push si
            C7 46 FC 00 00    ; var2 = 0
            BE 03 00          ; si = 3
            EB 04             ; jmp header
            01 76 FC          ; body: var2 += si
            4E                ; dec si
            23 F6             ; header: and si, si
            7F F8             ; jg body (rel8 = 19-27 = -8)
            89 76 FE          ; spill si → [bp-2]
            8B 46 FC          ; return var2
            5E                ; pop si
            8B E5             ; mov sp, bp
            5D                ; pop bp
            C3                ; ret
            """);

        var loop = Assert.Single(builder.GetAllOperations().OfType<ForOperation>());
        Assert.IsType<DecOperation>(loop.Iteration);

        var condition = Assert.IsType<CmpExpr>(loop.Condition);
        Assert.Equal(CmpOperation.Ugt, condition.Operation);
    }
}