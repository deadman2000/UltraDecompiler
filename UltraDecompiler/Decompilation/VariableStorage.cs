using UltraDecompiler.Headers;

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
    private readonly List<(int BaseOffset, Variable Variable, StructDefinition Definition)> _structLocals = [];
    private readonly List<(int BaseOffset, Variable BaseVariable, Variable SegmentVariable)> _farPointerLocals = [];
    private readonly Dictionary<Variable, (Variable Base, string FieldName)> _mergedFieldLocals = new();
    private int _stackNumber;
    private int _tempNumber;

    /// <summary>true, если для функции активирован стандартный стековый кадр (BP-based).</summary>
    public bool StackFrameActive { get; private set; }

    public void Clear()
    {
        _variables.Clear();
        _pspBase = null;
        _pspFields.Clear();
        _stackParameters.Clear();
        _stackLocals.Clear();
        _structLocals.Clear();
        _farPointerLocals.Clear();
        _mergedFieldLocals.Clear();
        StackFrameActive = false;
        _stackNumber = 0;
        _tempNumber = 0;
    }

    /// <summary>
    /// Создаёт именованную переменную (параметры, поля PSP, переменные строковых циклов и т.п.).
    /// </summary>
    public Variable CreateVariable(string? name = null)
    {
        var v = new Variable(Name: name);
        _variables.Add(v);
        return v;
    }

    /// <summary>
    /// Создаёт служебную переменную символического выполнения (регистры, PSP, retAddr).
    /// </summary>
    public Variable CreateInternalVariable(string name)
    {
        var v = new Variable(Number: 0, Name: name, IsInternal: true);
        _variables.Add(v);
        return v;
    }

    /// <summary>
    /// Создаёт локальную переменную стекового кадра [BP±disp] (<c>varN</c>).
    /// </summary>
    public Variable CreateStackVariable()
    {
        var v = new Variable(Number: ++_stackNumber, IsStack: true);
        _variables.Add(v);
        return v;
    }

    /// <summary>
    /// Создаёт временную переменную IR-анализа (<c>tempN</c>).
    /// </summary>
    public Variable CreateTempVariable()
    {
        var v = new Variable(Number: ++_tempNumber, IsTemp: true);
        _variables.Add(v);
        return v;
    }

    /// <summary>
    /// Возвращает (или создаёт) каноническую переменную — базу PSP.
    /// </summary>
    public Variable PspBase => _pspBase ??= CreateInternalVariable("_psp");

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
    /// Гарантирует стандартные параметры <c>main</c> на смещениях 4/6/(8) с именами argc/argv/envp.
    /// </summary>
    /// <returns>Параметры и отображение старых переменных параметров на обновлённые экземпляры.</returns>
    public (IReadOnlyList<FunctionParameter> Parameters, IReadOnlyDictionary<Variable, Variable> Renames) EnsureMainParameters(
        bool includeArgc,
        bool includeArgv,
        bool includeEnvp)
    {
        StackFrameActive = true;

        var specs = new List<(int Offset, string Name, CType Type)>();

        if (includeArgc)
        {
            specs.Add((4, "argc", CType.Int));
        }

        if (includeArgv)
        {
            specs.Add((6, "argv", CType.CharPtrPtr));
        }

        if (includeEnvp)
        {
            specs.Add((8, "envp", CType.CharPtrPtr));
        }

        var result = new List<FunctionParameter>(specs.Count);
        var renames = new Dictionary<Variable, Variable>(ReferenceEqualityComparer.Instance);

        foreach (var (offset, name, type) in specs)
        {
            Variable variable;
            if (!_stackParameters.TryGetValue(offset, out var existing))
            {
                variable = CreateVariable(name);
                variable.Type = type;
                _stackParameters[offset] = variable;
            }
            else
            {
                variable = existing with { Name = name };
                variable.Type = type;
                if (!ReferenceEquals(existing, variable))
                {
                    renames[existing] = variable;
                }

                _stackParameters[offset] = variable;
            }

            result.Add(new FunctionParameter(offset, variable));
        }

        return (result, renames);
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

    /// <summary>Локальные переменные стекового кадра [BP+disp], disp &lt; 0.</summary>
    public IReadOnlyList<(int Offset, Variable Variable)> StackLocals =>
        _stackLocals.Select(static kv => (kv.Key, kv.Value)).OrderBy(static e => e.Key).ToList();

    /// <summary>
    /// Активирует стековый кадр и создаёт переменные для локальных переменных
    /// по отрицательным смещениям [BP+disp] (disp &lt; 0, обычно -2, -4, ...).
    /// Локальные получают безымянные Variable (будут varN в выводе).
    /// Порядок создания — от BP к низу стека (убывание offset), как в исходном C.
    /// </summary>
    public IReadOnlyList<Variable> ActivateStackLocals(IEnumerable<int> localOffsets)
    {
        StackFrameActive = true;
        _stackLocals.Clear();

        var result = new List<Variable>();
        foreach (var offset in localOffsets.OrderDescending())
        {
            if (_stackLocals.ContainsKey(offset))
                continue;

            var variable = CreateStackVariable();
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

    /// <summary>Зарегистрированные на стеке структуры из заголовков.</summary>
    public IReadOnlyList<(int BaseOffset, Variable Variable, StructDefinition Definition)> StructLocals =>
        _structLocals;

    /// <summary>Зарегистрированные на стеке far-указатели (<c>char far *</c>).</summary>
    public IReadOnlyList<(int BaseOffset, Variable BaseVariable, Variable SegmentVariable)> FarPointerLocals =>
        _farPointerLocals;

    /// <summary>
    /// Регистрирует пару стековых слов (offset + segment) как один far-указатель.
    /// </summary>
    public void RegisterFarPointerLocal(int baseOffset, Variable baseVariable, Variable segmentVariable)
    {
        if (_farPointerLocals.Any(e => ReferenceEquals(e.BaseVariable, baseVariable)))
        {
            return;
        }

        baseVariable.Type = CType.CharFarPtr;
        baseVariable.FarPointerSegmentVariable = segmentVariable;
        segmentVariable.IsMergedFarPointerSegment = true;

        _farPointerLocals.Add((baseOffset, baseVariable, segmentVariable));

        if (baseOffset + 2 != 0 && _stackLocals.TryGetValue(baseOffset + 2, out var merged) &&
            ReferenceEquals(merged, segmentVariable))
        {
            _stackLocals.Remove(baseOffset + 2);
        }
    }

    /// <summary>
    /// Объединяет диапазон стековых локалей в одну переменную типа <paramref name="definition"/>.
    /// </summary>
    public void ConsolidateStructLocal(int baseOffset, StructDefinition definition)
    {
        if (!_stackLocals.TryGetValue(baseOffset, out var baseVariable))
        {
            return;
        }

        baseVariable.Type = definition.CType;
        baseVariable.ArraySize = null;

        _structLocals.RemoveAll(e => e.BaseOffset == baseOffset);
        _structLocals.Add((baseOffset, baseVariable, definition));

        var endOffset = baseOffset + definition.Size;
        var removeKeys = _stackLocals.Keys
            .Where(k => k >= baseOffset && k < endOffset && k != baseOffset)
            .ToList();

        foreach (var offset in removeKeys)
        {
            if (!_stackLocals.TryGetValue(offset, out var mergedVar))
            {
                continue;
            }

            var fieldOffset = offset - baseOffset;
            if (definition.TryResolveField(fieldOffset, out var field) && field is not null)
            {
                _mergedFieldLocals[mergedVar] = (baseVariable, field.Name);
            }

            _stackLocals.Remove(offset);
        }
    }

    /// <summary>Пытается сопоставить [BP+disp] с полем зарегистрированной структуры.</summary>
    public bool TryResolveStructFieldAccess(int bpDisplacement, out MemberExpr? member)
    {
        member = null;
        if (!StackFrameActive || bpDisplacement >= 0)
        {
            return false;
        }

        foreach (var (baseOffset, baseVariable, definition) in _structLocals)
        {
            if (bpDisplacement < baseOffset || bpDisplacement >= baseOffset + definition.Size)
            {
                continue;
            }

            var fieldOffset = bpDisplacement - baseOffset;
            if (!definition.TryResolveField(fieldOffset, out var field) || field is null)
            {
                continue;
            }

            member = new MemberExpr(baseVariable, field.Name);
            return true;
        }

        return false;
    }

    /// <summary>Возвращает поле структуры для ранее объединённого слова локала.</summary>
    public bool TryGetMergedField(Variable variable, out MemberExpr? member)
    {
        if (_mergedFieldLocals.TryGetValue(variable, out var mapped))
        {
            member = new MemberExpr(mapped.Base, mapped.FieldName);
            return true;
        }

        member = null;
        return false;
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