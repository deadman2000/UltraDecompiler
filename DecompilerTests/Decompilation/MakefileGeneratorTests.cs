using TestSupport;
using UltraDecompiler.CodeGeneration;
using UltraDecompiler.Compilation;
using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

/// <summary>Генерация MAKEFILE для пересборки декомпилированных исходников через QCL.</summary>
public class MakefileGeneratorTests
{
    // hello_s.exe: small-модель, stack check, SLIBCE.LIB, без /Gs
    // Ожидаемый фрагмент MAKEFILE: CFLAGS := /nologo /AS /Od, TARGET := HELLO_S.EXE
    [Fact]
    public void FormatMakefile_HelloSmall_ContainsQuickCBuildRecipe()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var makefile = MakefileGenerator.FormatMakefile(new MakefileOptions
            {
                TargetExeFileName = "HELLO_S.EXE",
                SourceFileNames = ["main.c"],
                CompilerOptions = new CompilerOptions
                {
                    MemoryModel = MemoryModel.Small,
                    StackCheckingEnabled = true,
                },
                LibraryFileNames = ["SLIBCE.LIB"],
                OutputDirectory = outputDirectory,
            });

            Assert.Contains("CFLAGS := /nologo /AS /Od", makefile);
            Assert.Contains("TARGET := HELLO_S.EXE", makefile);
            Assert.Contains("SRCS   := main.c", makefile);
            Assert.Contains("SLIBCE.LIB", makefile);
            Assert.Contains("QCL.EXE", makefile);
            Assert.DoesNotContain("/Gs", makefile);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    // Compact-модель без проверки стека → флаг /Gs в CFLAGS
    [Fact]
    public void FormatMakefile_StackCheckDisabled_AddsGsFlag()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var makefile = MakefileGenerator.FormatMakefile(new MakefileOptions
            {
                TargetExeFileName = "APP.EXE",
                SourceFileNames = ["main.c"],
                CompilerOptions = new CompilerOptions
                {
                    MemoryModel = MemoryModel.Compact,
                    StackCheckingEnabled = false,
                },
                LibraryFileNames = ["CLIBC.LIB"],
                OutputDirectory = outputDirectory,
            });

            Assert.Contains("CFLAGS := /nologo /AC /Gs /Od", makefile);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    // add.c: MAKEFILE должен перечислить main.c, sub_0010.c и SLIBCE.LIB
    [Fact]
    public void Decompile_AddSmall_WritesMakefileWithAllSources()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var decompiler = new Decompiler();
            var result = decompiler.Decompile(
                ExeProvider.Get("add.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);

            var makefilePath = Path.Combine(outputDirectory, MakefileGenerator.FileName);
            Assert.Contains(makefilePath, result.OutputFiles);
            Assert.True(File.Exists(makefilePath));

            var makefile = File.ReadAllText(makefilePath);
            Assert.Contains("main.c", makefile);
            Assert.Contains("sub_0010.c", makefile);
            Assert.Contains("SLIBCE.LIB", makefile);
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
