namespace UltraDecompiler.Decompilation;

public class VariableStorage
{
    private List<Variable> _variables = [];

    public void Clear()
    {
        _variables.Clear();
    }

    public Variable CreateVariable()
    {
        var ind = _variables.Count;
        var v = new Variable
        {
            Number = ind
        };
        _variables.Add(v);
        return v;
    }
}