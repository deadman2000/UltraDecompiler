namespace DecompilerTests.Decompilation;

/// <summary>Диагностический прогон всех PROGRAMS/*.c — печатает сводку в output теста.</summary>
[Trait("Tool", "DosBox")]
public sealed class QuickCProgramProbeTests
{
    [Fact]
    public void ProbeAllPrograms_PrintSummary()
    {
        if (!DosBoxQuickCAssets.IsDosBoxAvailable || !DosBoxQuickCAssets.IsQuickCToolchainAvailable)
        {
            return;
        }

        var results = QuickCProgramProbe.RunAll()
            .OrderBy(static r => r.SourceFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = results.Select(static r =>
            $"{r.SourceFileName,-12} {r.Stage,-12} {(r.Stage == QuickCProgramProbe.ProbeStage.Pass ? "" : r.Detail)}");

        var report = string.Join(Environment.NewLine, lines);
        var reportPath = Path.Combine(QuickCTestAssets.ProgramsDirectory, "ROUNDTRIP_PROBE.txt");
        File.WriteAllText(reportPath, report, System.Text.Encoding.UTF8);
        Assert.True(true, report);
    }
}
