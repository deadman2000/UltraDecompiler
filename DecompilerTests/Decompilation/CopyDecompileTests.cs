using TestSupport;

namespace DecompilerTests.Decompilation;

/// <summary>Интеграционные тесты декомпиляции <c>copy.c</c> (указатели, while, строковый литерал).</summary>
public sealed class CopyDecompileTests
{
    // Исходник copy.c: char buf[20], int a=10, char buf2[30], int b=8; copy/copy2; printf("%s\n", buf).
    // Ожидаемый фрагмент main.c:
    //   char var1[20]; int var2; char var3[30]; int var4;
    //   var2 = 10; var4 = 8;
    //   sub_0010(var1, "test"); sub_0010(var3, "test3"); sub_0048(var3, "test3");
    [Fact]
    public void Decompile_Copy_EmitsCharArrayLocal()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("copy.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory,
                libraryFileNames: ["SLIBCE.LIB"]);

            Assert.True(result.Success);

            var mainSource = DecompileTestHelper.ReadPrimarySource(result);
            Assert.Contains("char var1[20];", mainSource);
            Assert.Contains("int var2;", mainSource);
            Assert.Contains("char var3[30];", mainSource);
            Assert.Contains("int var4;", mainSource);
            Assert.DoesNotContain("char var2[", mainSource);
            Assert.DoesNotContain("char var4[", mainSource);
            Assert.Contains("var2 = 10;", mainSource);
            Assert.Contains("var4 = 8;", mainSource);
            Assert.Contains("sub_0010(var1, \"test\")", mainSource);
            Assert.Contains("sub_0010(var3, \"test3\")", mainSource);
            Assert.Contains("sub_0048(var3, \"test3\")", mainSource);
            Assert.Contains("printf(\"%s\\n\", var1)", mainSource);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    // Функция copy2 в copy.c (sub_0048) должна декомпилироваться в цикл по указателям:
    //   while (*arg1 != 0) { *arg0 = *arg1; arg0++; arg1++; }
    //   *arg0 = 0;
    [Fact]
    public void Decompile_Copy2_EmitsPointerLoopWithoutTempLocals()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("copy.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory,
                libraryFileNames: ["SLIBCE.LIB"]);

            Assert.True(result.Success);

            var copySource = DecompileTestHelper.ReadPrimarySource(result);
            Assert.Contains("void sub_0048(char* arg0, char* arg1)", copySource);
            Assert.Contains("while (*arg1 != 0)", copySource);
            Assert.Contains("*arg0 = *arg1", copySource);
            Assert.Contains("arg0++", copySource);
            Assert.Contains("arg1++", copySource);
            Assert.Contains("*arg0 = 0", copySource);
            Assert.DoesNotContain("temp1", copySource);
            Assert.DoesNotContain("temp2", copySource);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    // Полный сценарий copy.c: сигнатура sub_*(char*, char*), цикл strcpy в отдельном .c,
    // в main — массив char (не char*) и литерал "test", а не числовой адрес near-DGROUP.
    [Fact]
    public void Decompile_CopySmall_EmitsPointerLoopAndStringLiteral()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "UltraDecompilerTests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = new Decompiler().Decompile(
                ExeProvider.Get("copy.c"),
                QuickCTestAssets.LibDirectory,
                QuickCTestAssets.IncludeDirectory,
                outputDirectory,
                libraryFileNames: ["SLIBCE.LIB"]);

            Assert.True(result.Success);

            var copyProc = result.Procedures.All.FirstOrDefault(p => p.Name.StartsWith("sub_", StringComparison.Ordinal) && !p.IsLibrary);
            Assert.NotNull(copyProc);
            Assert.Equal(CType.Void, copyProc!.Signature.ReturnType);
            Assert.Equal(2, copyProc.Signature.Parameters.Count);
            Assert.True(copyProc.Signature.Parameters[0].Type.IsCharPtr);
            Assert.True(copyProc.Signature.Parameters[1].Type.IsCharPtr);

            var copySource = DecompileTestHelper.ReadPrimarySource(result);
            Assert.Contains("while (*arg1 != 0)", copySource);
            // copy.c в исходнике использует *dst++ = *src++; QuickC генерирует post-increment ASM.
            Assert.Contains("*arg0++ = *arg1++", copySource);
            Assert.Contains("*arg0 = 0", copySource);
            Assert.DoesNotContain("_psp:[", copySource);
            Assert.DoesNotContain("varSS:[", copySource);

            var mainSource = DecompileTestHelper.ReadPrimarySource(result);
            Assert.Contains("char var1[20];", mainSource);
            Assert.DoesNotContain("char* var1", mainSource);
            Assert.Contains("sub_0010(var1, \"test\")", mainSource);
            Assert.Contains("printf(\"%s\\n\", var1)", mainSource);
            Assert.DoesNotContain("printf(\"%s\\n\", 655", mainSource);
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
