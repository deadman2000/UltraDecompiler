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

    public ProcedureSignature(
        CType returnType,
        IReadOnlyList<ProcedureParameter> parameters,
        bool isVariadic = false)
    {
        ReturnType = returnType;
        Parameters = parameters;
        IsVariadic = isVariadic;
    }

    public int StackParameterCount =>
        Parameters.Count(static p => p.Location is StackParameter);
}
