using UltraDecompiler.CodeGeneration;
using UltraDecompiler.Decompilation;
using UltraDecompiler.Decompilation.Operations;
using UltraDecompiler.Headers;
using UltraDecompiler.PostProcessing;

namespace DecompilerTests.Decompilation;

/// <summary>Типизация указателей: malloc/strcpy и кодогенерация по паттерну alloc.c.</summary>
public class PointerCodegenTests
{
    // STDLIB.H: void *malloc(unsigned size) — нужен для вывода void* у результата malloc
    [Fact]
    public void Load_QuickCInclude_ParsesMallocReturnTypeAsVoidPtr()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));

        var catalog = HeaderCatalog.Load(includeDir);

        Assert.True(catalog.TryGetSignature("malloc", out var signature));
        Assert.NotNull(signature);
        Assert.Equal(CTypeKind.Pointer, signature!.ReturnType.Kind);
        Assert.Equal(CTypeKind.Void, signature.ReturnType.Pointee?.Kind);
        Assert.Equal("void*", signature.ReturnType.ToString());
    }

    // var = malloc(32) → тип локали void*, а не int
    [Fact]
    public void VariableTypeInferrer_MallocAssignment_InfersVoidPtr()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));
        var catalog = HeaderCatalog.Load(includeDir);

        var ptrVar = new Variable(9);
        var storage = new ProcedureStorage();
        storage.Add(new DisassembledProcedure
        {
            Offset = 0x100,
            Instructions = [],
            Name = "malloc",
            IsLibrary = true,
            Signature = catalog.All["malloc"],
        });

        var operations = new List<Operation>
        {
            new SetOperation(ptrVar, new CallExpr("malloc", [new ConstExpr(32)])),
        };

        VariableTypeInferrer.Infer(operations, storage, catalog);

        Assert.Equal("void*", ptrVar.Type?.ToString());
    }

    // Паттерн alloc.c:
    //   char *p = malloc(32);
    //   if (p) { p[0] = 'A'; p[1] = 0; printf("%s\n", p); free(p); }
    // Ожидаемый фрагмент main.c:
    //   char* var9;
    //   var9 = malloc(32);
    //   if (var9 != 0) { *var9 = 65; var9[1] = 0; printf("%s\n", var9); free(var9); }
    [Fact]
    public void FormatCFunction_AllocLikeMain_EmitsCharPtrAndPointerStores()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));
        var catalog = HeaderCatalog.Load(includeDir);

        var ptrVar = new Variable(9);
        var pspBase = new Variable(Name: "_psp", IsInternal: true);
        var storage = new ProcedureStorage();
        storage.Add(new DisassembledProcedure
        {
            Offset = 0x100,
            Instructions = [],
            Name = "malloc",
            IsLibrary = true,
            Signature = catalog.All["malloc"],
        });

        var operations = new List<Operation>
        {
            new SetOperation(ptrVar, new CallExpr("malloc", [new ConstExpr(32)])),
            new IfOperation(
                new CmpExpr(CmpOperation.Ne, ptrVar, ConstExpr.Zero),
                [
                    new StoreOperation(ptrVar, pspBase, new ConstExpr(65)),
                    new StoreOperation(
                        new Math2Expr(Math2Operation.Add, ptrVar, new ConstExpr(1)),
                        pspBase,
                        ConstExpr.Zero),
                    new CallOperation("printf", [new StringExpr("%s\n"), ptrVar]),
                    new CallOperation("free", [ptrVar]),
                ]),
            new ReturnOperation(ConstExpr.Zero),
        };

        VariableTypeInferrer.Infer(operations, storage, catalog);
        var procedure = new DisassembledProcedure
        {
            Offset = 0x10,
            Instructions = [],
            Expressions = new ExpressionBuilder(),
            Name = "main",
            IsLibrary = false,
            Signature = new ProcedureSignature(CType.Int, []),
        };

        var source = CCodeGenerator.FormatCFunction(procedure, operations);

        Assert.Contains("char* var9;", source);
        Assert.DoesNotContain("void* var9;", source);
        Assert.DoesNotContain("int var9;", source);
        Assert.DoesNotContain("int _psp;", source);
        Assert.Contains("*var9 = 65;", source);
        Assert.Contains("var9[1] = 0;", source);
        Assert.DoesNotContain("_psp:[var9]", source);
    }

    // dst = strcpy(dst, "hello") — возвращаемое значение strcpy даёт тип char* назначению
    [Fact]
    public void FormatCFunction_StrcpyReturn_InfersCharPtr()
    {
        var includeDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "QuickC", "INCLUDE"));
        var catalog = HeaderCatalog.Load(includeDir);

        if (!catalog.TryGetSignature("strcpy", out var strcpySig) || strcpySig is null)
        {
            return;
        }

        var dstVar = new Variable(1);
        var operations = new List<Operation>
        {
            new SetOperation(
                dstVar,
                new CallExpr("strcpy", [dstVar, new StringExpr("hello")])),
        };

        VariableTypeInferrer.Infer(operations, new ProcedureStorage(), catalog);

        Assert.Equal("char*", dstVar.Type?.ToString());
    }
}
