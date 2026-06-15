using UltraDecompiler.PostProcessing.Normalization;

namespace DecompilerTests.PostProcessing;

/// <summary>Упрощение <c>&amp; 255</c> в счётчиках сдвигов для round-trip QuickC.</summary>
public sealed class ShiftCountSimplifierTests
{
    // QuickC пишет (x << n), а не (x << (n & 255)) — маска из CL не должна попадать в C.
    [Fact]
    public void Simplify_RemovesLowByteMaskFromShiftCount()
    {
        var arg0 = new Variable(0, Name: "arg0") { Type = CType.UnsignedInt };
        var arg1 = new Variable(1, Name: "arg1") { Type = CType.Int };
        var expr = new Math2Expr(
            Math2Operation.Or,
            new Math2Expr(
                Math2Operation.Shl,
                arg0,
                new Math2Expr(Math2Operation.And, arg1, new ConstExpr(255))),
            new Math2Expr(
                Math2Operation.Shr,
                arg0,
                new Math2Expr(
                    Math2Operation.And,
                    new Math2Expr(Math2Operation.Sub, new ConstExpr(16), arg1),
                    new ConstExpr(255))));

        var operations = new List<Operation>
        {
            new ReturnOperation(expr, IsExplicit: true),
        };

        var simplified = ShiftCountSimplifier.Simplify(operations);
        var ret = Assert.IsType<ReturnOperation>(Assert.Single(simplified));

        Assert.Equal(
            "(arg0 << arg1) | (arg0 >> 16 - arg1)",
            ret.Value!.ToString());
    }
}
