using UltraDecompiler.Decompilation;

namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Присваивание: простая переменная, разыменование указателя или выражение с инкрементом в lvalue.
/// </summary>
/// <param name="Dst">Левая часть (переменная, <c>*ptr</c>, <c>*ptr++</c> и т.д.).</param>
/// <param name="Src">Правая часть.</param>
public record SetOperation(Expr Dst, Expr Src) : Operation;
