namespace UltraDecompiler.Decompilation;

/// <summary>Параметр процедуры с типом и местом передачи.</summary>
public sealed record ProcedureParameter(CType Type, ParameterLocation Location);

/// <summary>Сигнатура процедуры: возвращаемый тип и параметры.</summary>
public sealed class ProcedureSignature
{
    public static ProcedureSignature Unknown { get; } = new(CType.Int, []);

    public CType ReturnType { get; }

    public IReadOnlyList<ProcedureParameter> Parameters { get; }

    /// <summary>Функция объявлена с <c>...</c> (аргументы восстанавливаются по PUSH перед CALL).</summary>
    public bool IsVariadic { get; init; }

    /// <summary>
    /// Регистры, которые процедура может модифицировать (clobber). Полезно для точного моделирования состояния после CALL.
    /// Для базовой поддержки return используется, чтобы после вызова "портить" caller-saved регистры (кроме AX при возврате значения).
    /// </summary>
    public IReadOnlySet<GpRegister16> Clobbers { get; init; } = new HashSet<GpRegister16>();

    public ProcedureSignature(
        CType returnType,
        IReadOnlyList<ProcedureParameter> parameters,
        bool isVariadic = false,
        IReadOnlySet<GpRegister16>? clobbers = null)
    {
        ReturnType = returnType;
        Parameters = parameters;
        IsVariadic = isVariadic;
        Clobbers = clobbers ?? new HashSet<GpRegister16>();
    }

    public int StackParameterCount =>
        Parameters.Count(static p => p.Location is StackParameter);
}
