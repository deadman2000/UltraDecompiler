namespace UltraDecompiler.Ir.Operations;

/// <summary>Метка для <c>goto</c> (<c>label:</c>).</summary>
/// <param name="Label">Имя метки.</param>
public sealed record LabelOperation(string Label) : Operation;