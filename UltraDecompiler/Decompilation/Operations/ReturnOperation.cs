namespace UltraDecompiler.Decompilation.Operations;

/// <summary>
/// Операция возврата из процедуры (возврат значения через AX по соглашению cdecl/QuickC).
/// Value содержит символическое выражение, которое находилось в AX на момент выполнения RET.
/// При генерации C-кода значение используется или игнорируется в зависимости от ReturnType сигнатуры процедуры (void — bare return;).
/// </summary>
public sealed record ReturnOperation(Expr? Value) : Operation;
