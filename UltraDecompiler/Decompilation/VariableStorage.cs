namespace UltraDecompiler.Decompilation;

/// <summary>
/// Хранилище переменных
/// </summary>
public class VariableStorage
{
    private readonly List<Variable> _variables = [];
    private Variable? _pspBase;
    private readonly Dictionary<int, Variable> _pspFields = new();
    private readonly Dictionary<int, Variable> _stackParameters = new();
    private readonly Dictionary<int, Variable> _stackLocals = new();

    /// <summary>true, если для функции активирован стандартный стековый кадр (BP-based).</summary>
    public bool StackFrameActive { get; private set; }

    public void Clear()
    {
        _variables.Clear();
        _pspBase = null;
        _pspFields.Clear();
        _stackParameters.Clear();
        _stackLocals.Clear();
        StackFrameActive = false;
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
    /// Активирует стековый кадр и создаёт переменные параметров для указанных смещений [BP+offset].
    /// </summary>
    public IReadOnlyList<FunctionParameter> ActivateStackFrame(IEnumerable<int> stackOffsets)
    {
        StackFrameActive = true;
        _stackParameters.Clear();

        var result = new List<FunctionParameter>();
        int index = 0;

        foreach (var offset in stackOffsets.OrderBy(o => o))
        {
            if (_stackParameters.ContainsKey(offset))
                continue;

            var variable = CreateVariable($"arg{index}");
            _stackParameters[offset] = variable;
            result.Add(new FunctionParameter(offset, variable));
            index++;
        }

        return result;
    }

    /// <summary>
    /// Возвращает переменную параметра для обращения [BP+disp], если кадр активен.
    /// </summary>
    public Variable? TryGetStackParameter(int bpDisplacement)
    {
        if (!StackFrameActive || bpDisplacement < 4)
            return null;

        return _stackParameters.GetValueOrDefault(bpDisplacement);
    }

    /// <summary>
    /// Активирует стековый кадр и создаёт переменные для локальных переменных
    /// по отрицательным смещениям [BP+disp] (disp &lt; 0, обычно -2, -4, ...).
    /// Локальные получают безымянные Variable (будут varN в выводе).
    /// </summary>
    public IReadOnlyList<Variable> ActivateStackLocals(IEnumerable<int> localOffsets)
    {
        StackFrameActive = true;
        _stackLocals.Clear();

        var result = new List<Variable>();
        foreach (var offset in localOffsets.OrderBy(o => o))
        {
            if (_stackLocals.ContainsKey(offset))
                continue;

            var variable = CreateVariable();
            _stackLocals[offset] = variable;
            result.Add(variable);
        }

        return result;
    }

    /// <summary>
    /// Возвращает переменную локала для обращения [BP+disp] (disp &lt; 0), если кадр активен.
    /// </summary>
    public Variable? TryGetStackLocal(int bpDisplacement)
    {
        if (!StackFrameActive || bpDisplacement >= 0)
            return null;

        return _stackLocals.GetValueOrDefault(bpDisplacement);
    }

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