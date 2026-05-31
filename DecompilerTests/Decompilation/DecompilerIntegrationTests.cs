using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

public class DecompilerIntegrationTests
{
    [Fact]
    public void Decompile_HelloSmall_FindsMainPrintfAndWritesCFile()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var decompiler = new Decompiler();
            var result = decompiler.Decompile(
                QuickCTestAssets.ProgramsPathOf("HELLO_S.EXE"),
                QuickCTestAssets.LibDirectory,
                outputDirectory);

            Assert.True(result.Success);
            Assert.Equal("SLIBCE.LIB", result.SelectedLibraryFileName);
            Assert.Equal(0x10, result.MainOffset);

            Assert.True(result.Procedures.TryGet(0x10, out var main));
            Assert.NotNull(main);
            Assert.False(main!.IsLibrary);
            Assert.Equal("_main", main.Name);

            var printfProcedure = result.Procedures.All.FirstOrDefault(p =>
                p.IsLibrary && p.Name == "_printf");
            Assert.NotNull(printfProcedure);

            Assert.Contains(result.OutputFiles, path => path.EndsWith("_main.c", StringComparison.Ordinal));
            var mainSource = File.ReadAllText(result.OutputFiles.First(path => path.EndsWith("_main.c", StringComparison.Ordinal)));
            Assert.Contains("_printf(", mainSource);
            Assert.Contains("void _main(void)", mainSource);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Decompile_MissingLibraryDirectory_Throws()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        try
        {
            var decompiler = new Decompiler();

            Assert.Throws<DirectoryNotFoundException>(() => decompiler.Decompile(
                QuickCTestAssets.ProgramsPathOf("HELLO_S.EXE"),
                Path.Combine(outputDirectory, "missing-libs"),
                outputDirectory));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
