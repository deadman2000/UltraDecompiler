using UltraDecompiler.Decompilation;

namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Одна ветка <c>switch</c>: метка <paramref name="Value"/> или <see langword="default"/>.
/// </summary>
/// <param name="Value">Константа case; <see langword="null"/> — ветка <c>default</c>.</param>
/// <param name="Body">Тело ветки до точки выхода (break в QuickC).</param>
public record SwitchCase(ConstExpr? Value, IReadOnlyList<Operation> Body);
