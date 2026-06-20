using TestSupport;
using UltraDecompiler.PostProcessing.Stack;

namespace DecompilerTests.Decompilation;

/// <summary>Детекция /Gs (проверка стека) и удаление вызовов _chkstk из сгенерированного C.</summary>
public class StackCheckDetectorTests
{
    // hello.exe со stack check: флаг включён, _chkstk() не попадает в main.c
    [Fact(Skip = "NotImplemented")]
    public void Decompile_HelloSmall_DetectsStackCheckAndRemovesChkstkFromC()
    {
        var result = DecompileTestHelper.DecompileExample("hello.c", stackCheck: true);

        Assert.True(result.Success);
        Assert.True(result.CompilerOptions.StackCheckingEnabled);

        var mainSource = File.ReadAllText(
            result.OutputFiles.First(path => path.EndsWith("main.c", StringComparison.Ordinal)));
        Assert.DoesNotContain("_chkstk(", mainSource, StringComparison.Ordinal);
    }

    // add.exe: _chkstk убирается из main.c и всех sub_*.c пользователя
    [Fact(Skip = "NotImplemented")]
    public void Decompile_AddSmall_DetectsStackCheckAndRemovesChkstkFromAllUserFunctions()
    {
        var result = DecompileTestHelper.DecompileExample("add.c", stackCheck: true);

        Assert.True(result.Success);
        Assert.True(result.CompilerOptions.StackCheckingEnabled);

        foreach (var filePath in result.OutputFiles)
        {
            var source = File.ReadAllText(filePath);
            Assert.DoesNotContain("_chkstk(", source, StringComparison.Ordinal);
        }
    }

    // Юнит-тест фильтра: _chkstk внутри if тоже удаляется из списка операций
    [Fact(Skip = "NotImplemented")]
    public void RemoveChkstkCalls_StripsNestedCalls()
    {
        var operations = new List<Operation>
        {
            new SetOperation(new Variable(1), new CallExpr(StackCheckDetector.ChkstkCName, [])),
            new IfOperation(
                new ConstExpr(1),
                [new SetOperation(new Variable(2), new CallExpr(StackCheckDetector.ChkstkCName, []))],
                null),
        };

        var filtered = StackCheckDetector.RemoveChkstkCalls(operations);

        Assert.Empty(filtered);
    }
}
