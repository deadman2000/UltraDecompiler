using UltraDecompiler.PostProcessing.Loops;

namespace DecompilerTests.Decompilation;

public sealed class ArgsForLoopRecognizerTests
{
    [Fact]
    public void Convert_ArgcWhileWithTrailingIncrement_BecomesFor()
    {
        var index = new Variable(Name: "var1");
        var argc = new Variable(Name: "argc");
        var body = new List<Operation> { new IncOperation(index) };
        var loop = new WhileOperation(new CmpExpr(CmpOperation.Ult, index, argc), body);
        var operations = new List<Operation>
        {
            new SetOperation(index, new ConstExpr(1)),
            loop,
        };

        var converted = ArgvEnvpForLoopRecognizer.Convert(operations).ToList();

        Assert.IsType<ForOperation>(converted[^1]);
    }
}
