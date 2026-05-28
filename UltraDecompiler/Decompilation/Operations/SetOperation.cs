namespace UltraDecompiler.Decompilation.Operations;

/// <summary>
/// Установка значения
/// </summary>
/// <param name="Dst">Назначение</param>
/// <param name="Src">Источник</param>
public record SetOperation(Variable Dst, Expr Src) : Operation;
