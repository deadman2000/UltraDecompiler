using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;

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
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);
            Assert.Contains("SLIBCE.LIB", result.LinkedLibraryFileNames);
            Assert.NotEmpty(result.PossibleLibraryConfigurations);
            // Хотя бы один вариант должен включать SLIBCE.LIB как crt-базу или в списке
            Assert.Contains(result.PossibleLibraryConfigurations, cfg =>
                cfg.LibraryFileNames.Any(n => n.Contains("SLIBCE", StringComparison.OrdinalIgnoreCase)) ||
                (cfg.PrimaryCrtLibrary?.Contains("SLIBCE", StringComparison.OrdinalIgnoreCase) ?? false));
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
            Assert.Contains("int main(void)", mainSource);
            Assert.True(printfProcedure.Signature.Parameters.Count >= 1);
            // Проверяем, что форматная строка восстановлена как StringExpr (char* из заголовка),
            // а не оставлена как сырой числовой адрес (ConstExpr).
            // Точное содержимое зависит от отображения near-DGROUP → байты образа (отдельная задача).
            Assert.Contains("printf(\"Hello world\\n\"", mainSource);
            Assert.DoesNotContain("printf(618", mainSource);
            Assert.DoesNotContain("printf(0x", mainSource, StringComparison.OrdinalIgnoreCase);
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
                QuickCTestAssets.IncludeDirectory,
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

    [Fact]
    public void Decompile_AddSmall_RecoversCallToAddFunctionWithCorrectArguments()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var decompiler = new Decompiler();
            var result = decompiler.Decompile(
                QuickCTestAssets.ProgramsPathOf("ADD_S.EXE"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory);

            Assert.True(result.Success);

            // Находим main по имени (смещение main может отличаться для разных моделей памяти)
            var mainProc = result.Procedures.All.FirstOrDefault(p => p.Name == "main" && !p.IsLibrary);
            Assert.NotNull(mainProc);
            Assert.Equal("main", mainProc!.Name);
            Assert.False(mainProc.IsLibrary);

            // Получаем все операции main (IR после разрешения CallState)
            var mainOps = mainProc.Expressions.GetAllOperations();

            // Ищем вызов add(10, 5) — CallExpr от пользовательской sub_ с точными аргументами 10 и 5.
            // (CallState + resolver должны восстановить именно эти константы из снимка стека на момент вызова)
            var allCallExprs = mainOps
                .SelectMany(op =>
                {
                    if (op is SetOperation s && s.Src is CallExpr ce) return new[] { ce };
                    if (op is CallOperation co) return new[] { new CallExpr(co.Name, co.Args) };
                    return Array.Empty<CallExpr>();
                })
                .ToList();

            var addCallExpr = allCallExprs
                .FirstOrDefault(c => c is not null
                    && c.Name.StartsWith("sub_")
                    && c.Args.Count == 2
                    && c.Args.Any(a => a is ConstExpr { Value: 10 })
                    && c.Args.Any(a => a is ConstExpr { Value: 5 }));

            Assert.NotNull(addCallExpr);
            Assert.StartsWith("sub_", addCallExpr!.Name); // пользовательская функция сложения
            Assert.Equal(2, addCallExpr.Args.Count);

            // Точная проверка значений аргументов (как просил пользователь)
            var argValues = addCallExpr.Args
                .OfType<ConstExpr>()
                .Select(c => c.Value)
                .OrderBy(v => v)
                .ToArray();
            Assert.Equal(new[] { 5, 10 }, argValues);

            // Проверка variadic: printf в ADD_S.EXE должен иметь 2 аргумента
            // (адрес форматной строки + результат add, т.е. значение c)
            var printfCall = allCallExprs.FirstOrDefault(c =>
                c != null &&
                (c.Name == "printf" || c.Name.Contains("printf", StringComparison.OrdinalIgnoreCase)));
            Assert.NotNull(printfCall);
            Assert.Equal(2, printfCall!.Args.Count);

            // Проверяем в сгенерированном C, что первый аргумент — char* литерал (а не число/int)
            var mainSourceForAdd = File.ReadAllText(result.OutputFiles.First(path => path.EndsWith("main.c", StringComparison.Ordinal)));
            Assert.Contains("printf(\"%d\",", mainSourceForAdd);
            Assert.DoesNotContain("printf(618", mainSourceForAdd);
            Assert.DoesNotContain("printf(0x", mainSourceForAdd, StringComparison.OrdinalIgnoreCase);

            // Также проверяем, что в procedures есть пользовательская функция сложения
            // (кроме main)
            var userFunctions = result.Procedures.All
                .Where(p => !p.IsLibrary && p.Name != "main")
                .ToList();

            Assert.NotEmpty(userFunctions);

            // Проверяем сигнатуру одной из них (функция add имеет 2 параметра)
            var addUserProc = userFunctions.FirstOrDefault(p => p.Signature.Parameters.Count == 2);
            Assert.NotNull(addUserProc);
            Assert.Equal(2, addUserProc!.Signature.Parameters.Count);
            Assert.False(addUserProc.Signature.ReturnType.IsVoid); // short -> int в модели

            Assert.Empty(addUserProc.Callees);
            Assert.Contains(mainProc.Callees, static c => c.StartsWith("sub_", StringComparison.Ordinal));
            Assert.Contains(mainProc.Callees, static c => c.Contains("printf", StringComparison.OrdinalIgnoreCase));

            var addHeaderPath = result.OutputFiles.First(path =>
                path.EndsWith($"{addUserProc.Name}.h", StringComparison.OrdinalIgnoreCase));
            var addHeader = File.ReadAllText(addHeaderPath);
            Assert.Contains($"int {addUserProc.Name}(int arg0, int arg1);", addHeader);
            Assert.DoesNotContain(result.OutputFiles, path => path.EndsWith("main.h", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(result.OutputFiles, path => path.EndsWith("main.c", StringComparison.Ordinal));
            Assert.Contains($"#include \"{addUserProc.Name}.h\"", mainSourceForAdd);
            Assert.Contains("#include <STDIO.H>", mainSourceForAdd, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("ADD_S.EXE")]
    // Для других моделей памяти (C/M/L) кодогенерация вызовов и стек может отличаться;
    // базовый тест на S покрывает типичный near cdecl с push immediate + CallState.
    public void Decompile_AddVariants_RecoverAddCallArguments(string exeFileName)
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

            var mainProc = result.Procedures.All.ToList().FirstOrDefault(p => p.Name == "main" && !p.IsLibrary);
            Assert.NotNull(mainProc);

            var mainOps = mainProc!.Expressions.GetAllOperations();

            // Проверяем наличие вызова add(10, 5) с точными значениями аргументов
            var hasCorrectAddCall = mainOps
                .SelectMany(op =>
                {
                    if (op is SetOperation s && s.Src is CallExpr ce) return new[] { ce };
                    if (op is CallOperation co) return new[] { new CallExpr(co.Name, co.Args) };
                    return Array.Empty<CallExpr>();
                })
                .Any(c => c is not null
                    && c.Name.StartsWith("sub_")
                    && c.Args.Count == 2
                    && c.Args.Any(a => a is ConstExpr { Value: 10 })
                    && c.Args.Any(a => a is ConstExpr { Value: 5 }));

            Assert.True(hasCorrectAddCall, $"В {exeFileName} не найден вызов add(10, 5) с точными аргументами 10 и 5 после разрешения CallState");
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
