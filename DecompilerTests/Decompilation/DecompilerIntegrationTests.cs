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
            Assert.Contains("SLIBCE.LIB", result.LinkedLibraryFileNames);
            Assert.Equal(0x10, result.MainOffset);

            Assert.True(result.Procedures.TryGet(0x10, out var main));
            Assert.NotNull(main);
            Assert.False(main!.IsLibrary);
            Assert.Equal("main", main.Name);

            Assert.True(result.Procedures.TryGet(0x5C4, out var printfProcedure));
            Assert.NotNull(printfProcedure);
            Assert.True(printfProcedure!.IsLibrary);
            Assert.Equal("printf", printfProcedure.Name);
            Assert.Equal("printf", printfProcedure.LibraryMatch?.ModuleName);

            Assert.Contains(result.OutputFiles, path => path.EndsWith("main.c", StringComparison.Ordinal));
            var mainSource = File.ReadAllText(result.OutputFiles.First(path => path.EndsWith("main.c", StringComparison.Ordinal)));
            Assert.Contains("printf(", mainSource);
            Assert.Contains("void main(void)", mainSource);
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
