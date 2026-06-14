using UltraDecompiler.Decompilation;

namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Запись значения в память (store).
/// </summary>
/// TODO: Объединить Address и Segment
public record StoreOperation(Expr Address, Expr? Segment, Expr Value) : Operation;
