namespace UltraDecompiler.Decompilation.Operations;

/// <summary>
/// Операция возврата из процедуры (возврат значения через AX по соглашению cdecl/QuickC).
/// Value содержит символическое выражение, которое находилось в AX на момент выполнения RET.
/// При генерации C-кода значение используется или игнорируется в зависимости от ReturnType сигнатуры процедуры (void — bare return;).
/// </summary>
/// <param name="Value">Символическое значение AX на момент выхода.</param>
/// <param name="IsExplicit">
/// Для void: <see langword="true"/>, если выход оформлен как явный <c>return;</c> в исходнике QuickC
/// (переход JMP на общий эпилог); <see langword="false"/> при линейном fall-through к RET.
/// Для не-void всегда игнорируется при кодогенерации.
/// </param>
public sealed record ReturnOperation(Expr? Value, bool IsExplicit = false) : Operation;
