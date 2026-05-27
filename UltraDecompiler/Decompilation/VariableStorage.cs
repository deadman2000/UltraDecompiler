namespace UltraDecompiler.Decompilation;

/// <summary>
/// Хранилище переменных
/// </summary>
public class VariableStorage
{
    private readonly List<Variable> _variables = [];
    private Variable? _pspBase;
    private readonly Dictionary<int, Variable> _pspFields = new();

    public void Clear()
    {
        _variables.Clear();
        _pspBase = null;
        _pspFields.Clear();
    }

    /// <summary>
    /// Создает новую переменную
    /// </summary>
    public Variable CreateVariable(string? name = null)
    {
        var v = new Variable
        {
            Number = _variables.Count,
            Name = name
        };
        _variables.Add(v);
        return v;
    }

    /// <summary>
    /// Возвращает (или создаёт) каноническую переменную — базу PSP.
    /// </summary>
    public Variable PspBase => _pspBase ??= CreateVariable("_psp");

    /// <summary>
    /// Пытается распознать доступ к известной структуре в памяти (например, поля PSP).
    /// Если адрес + сегмент соответствуют известному расположению, возвращает
    /// заранее созданную именованную Variable вместо сырой MemExpr.
    /// </summary>
    public Variable? TryGetKnownMemoryVariable(Expr address, Expr? segment)
    {
        // Проверка на PSP
        var psp = PspBase;
        if (IsSameVariable(segment, psp) || AddressContainsBase(address, psp))
        {
            int? offset = TryExtractConstantOffset(address, psp);
            if (offset != null)
            {
                if (PspKnownFields.IsKnown(offset.Value))
                    return GetOrCreatePspField(offset.Value);
            }
        }

        return null;
    }

    /// <summary>
    /// Возвращает (или создаёт) именованную переменную для известного поля PSP по смещению.
    /// Для неизвестных смещений создаёт generic-имя "Psp.Field_XX".
    /// </summary>
    public Variable GetOrCreatePspField(int offset)
    {
        if (_pspFields.TryGetValue(offset, out var v))
            return v;

        string name = PspKnownFields.GetName(offset);
        v = CreateVariable(name);
        _pspFields[offset] = v;
        return v;
    }

    private static bool IsSameVariable(Expr? expr, Variable target)
    {
        return expr is Variable v && ReferenceEquals(v, target);
    }

    private static bool AddressContainsBase(Expr address, Variable baseVar)
    {
        if (ReferenceEquals(address, baseVar))
            return true;

        if (address is Math2Expr m2 && m2.Operation == Math2Operation.Add)
        {
            return IsSameVariable(m2.First, baseVar) || IsSameVariable(m2.Second, baseVar);
        }
        return false;
    }

    private static int? TryExtractConstantOffset(Expr address, Variable baseVar)
    {
        if (address is ConstExpr c)
            return c.Value;

        if (address is Math2Expr add && add.Operation == Math2Operation.Add)
        {
            if (IsSameVariable(add.First, baseVar) && add.Second is ConstExpr c2)
                return c2.Value;
            if (IsSameVariable(add.Second, baseVar) && add.First is ConstExpr c3)
                return c3.Value;
        }

        return null;
    }

    private static class PspKnownFields
    {
        private static readonly Dictionary<int, string> Map = new()
        {
            [0x02] = "Psp.LastParagraph",
            [0x2C] = "Psp.EnvironmentSegment",
            [0x80] = "Psp.CommandTailLength",
            [0x81] = "Psp.CommandTail",
        };

        public static string GetName(int offset)
        {
            return Map.TryGetValue(offset, out var name)
                ? name
                : $"Psp.Field_{offset:X2}";
        }
        public static bool IsKnown(int offset) => Map.ContainsKey(offset);
    }
}