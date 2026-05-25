namespace UltraDecompiler.Decompilation;

/// <summary>
/// Хранилище переменных
/// </summary>
public class VariableStorage
{
    private readonly List<Variable> _variables = [];

    public void Clear()
    {
        _variables.Clear();
    }

    /// <summary>
    /// Создает новую переменную
    /// </summary>
    public Variable CreateVariable()
    {
        var v = new Variable
        {
            Number = _variables.Count
        };
        _variables.Add(v);
        return v;
    }
}