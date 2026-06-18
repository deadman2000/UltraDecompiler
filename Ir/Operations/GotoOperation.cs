namespace UltraDecompiler.Ir.Operations;

/// <summary>Безусловный переход (<c>goto label;</c>).</summary>
/// <param name="Label">Имя метки назначения.</param>
public sealed record GotoOperation(string Label) : Operation;