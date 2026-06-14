namespace UltraDecompiler.Ir.Procedures;

/// <summary>
/// Параметр декомпилируемой функции, переданный через стек (соглашение QuickC / Microsoft C, near).
/// </summary>
/// <param name="StackOffset">Смещение от BP после пролога (первый параметр — 4, следующий — 6 и т.д.).</param>
/// <param name="Variable">Символическая переменная параметра.</param>
public record FunctionParameter(int StackOffset, Variable Variable);
