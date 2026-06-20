using TestSupport;
using UltraDecompiler.CodeGeneration;
using UltraDecompiler.Compilation;

namespace DecompilerTests.Decompilation;

/// <summary>Генерация MAKEFILE для пересборки декомпилированных исходников через QCL.</summary>
public class MakefileGeneratorTests
{
    // hello.exe: small-модель, stack check, SLIBCE.LIB, без /Gs
    // Ожидаемый фрагмент MAKEFILE: CFLAGS := /nologo /AS /Od, TARGET := HELLO.EXE
    [Fact(Skip = "NotImplemented")]
    public void FormatMakefile_HelloSmall_ContainsQuickCBuildRecipe()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var makefile = MakefileGenerator.FormatMakefile(new MakefileOptions
            {
                TargetExeFileName = "HELLO.EXE",
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
            Assert.Contains("TARGET := HELLO.EXE", makefile);
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
    [Fact(Skip = "NotImplemented")]
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

    // add.c: Makefile и каталог вывода содержат один .c, имя совпадает с EXE (s_gs_od.exe → s_gs_od.c).
    [Fact(Skip = "NotImplemented")]
    public void Decompile_AddSmall_WritesMakefileWithAllSources()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var exePath = ExeProvider.Get("add.c");
            var expectedSource = CCodeGenerator.FormatCombinedSourceFileName(Path.GetFileName(exePath));

            var decompiler = new Decompiler();
            var result = decompiler.Decompile(
                exePath,
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);

            var makefilePath = Path.Combine(outputDirectory, MakefileGenerator.FileName);
            Assert.Contains(makefilePath, result.OutputFiles);
            Assert.True(File.Exists(makefilePath));

            var makefile = File.ReadAllText(makefilePath);
            Assert.Contains($"SRCS   := {expectedSource}", makefile);
            Assert.Contains("SLIBCE.LIB", makefile);

            Assert.True(File.Exists(Path.Combine(outputDirectory, expectedSource)));
            Assert.DoesNotContain(result.OutputFiles, path => path.EndsWith("main.c", StringComparison.Ordinal));
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
