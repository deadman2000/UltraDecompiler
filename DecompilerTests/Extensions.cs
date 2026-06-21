using DecompilerTests.Tools;

namespace DecompilerTests;

internal static class ExprTestHelpers
{
    public static void AssertReferencesVariable(Expr expr, Variable variable) =>
        Assert.True(AssignmentTarget.ReferencesVariable(expr, variable));

    public static void AssertSameVariable(Variable expected, Expr actual)
    {
        Assert.True(AssignmentTarget.TryGetVariable(actual, out var variable));
        Assert.Same(expected, variable);
    }
}

internal static class Extensions
{
    extension(string str)
    {
        public byte[] FromHex()
        {
            return HexConverter.FromHexString(str.AsSpan());
        }
    }
}
