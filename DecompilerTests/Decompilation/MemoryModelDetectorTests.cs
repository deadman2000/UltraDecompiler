using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

public class MemoryModelDetectorTests
{
    [Theory]
    [InlineData("SLIBC.LIB", MemoryModel.Small)]
    [InlineData("SLIBCE.LIB", MemoryModel.Small)]
    [InlineData("SLIBFP.LIB", MemoryModel.Small)]
    [InlineData("CLIBC.LIB", MemoryModel.Compact)]
    [InlineData("CLIBCE.LIB", MemoryModel.Compact)]
    [InlineData("CLIBFP.LIB", MemoryModel.Compact)]
    [InlineData("MLIBC.LIB", MemoryModel.Medium)]
    [InlineData("MLIBCE.LIB", MemoryModel.Medium)]
    [InlineData("LLIBC.LIB", MemoryModel.Large)]
    [InlineData("LLIBCE.LIB", MemoryModel.Large)]
    [InlineData("87.LIB", MemoryModel.Unknown)]
    [InlineData("GRAPHICS.LIB", MemoryModel.Unknown)]
    public void DetectFromLibraryFileName_RecognizesQuickCLibraries(string fileName, MemoryModel expected) =>
        Assert.Equal(expected, MemoryModelDetector.DetectFromLibraryFileName(fileName));

    [Theory]
    [InlineData("HELLO_S.EXE", MemoryModel.Small)]
    [InlineData("HELLO_C.EXE", MemoryModel.Compact)]
    [InlineData("HELLO_M.EXE", MemoryModel.Medium)]
    [InlineData("HELLO_L.EXE", MemoryModel.Large)]
    public void Decompile_HelloMemoryModels_DetectsMemoryModel(string exeFileName, MemoryModel expected)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var decompiler = new Decompiler();
            var result = decompiler.Decompile(
                QuickCTestAssets.ProgramsPathOf(exeFileName),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            Assert.Equal(expected, result.CompilerOptions.MemoryModel);
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
