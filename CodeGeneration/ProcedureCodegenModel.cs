namespace UltraDecompiler.CodeGeneration;

/// <summary>Данные процедуры, необходимые для генерации C-кода (без зависимости от Decompilation).</summary>
public sealed record ProcedureCodegenModel(
    string Name,
    int Offset,
    ProcedureSignature Signature,
    IReadOnlyList<FunctionParameter> Parameters,
    IReadOnlyList<Variable> StackLocals);
