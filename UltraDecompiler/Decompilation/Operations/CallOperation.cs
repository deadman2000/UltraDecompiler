namespace UltraDecompiler.Decompilation.Operations;

/// <summary>
/// Вызов метода
/// </summary>
public record CallOperation(Procedure Procedure, IReadOnlyList<Expr> Args) : Operation
{
    public override string ToString()
    {
        var args = string.Join(", ", Args);
        return $"{Procedure.Name}({args})";
    }
}
