namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Операция цикла while.
/// 
/// Используется для представления циклов с условием продолжения (в первую очередь
/// REPZ/REPNZ-версий строковых инструкций CMPS и SCAS).
/// 
/// Тело цикла хранится как список вложенных <see cref="Operation"/>, что позволяет
/// рекурсивно описывать сложные структуры (в т.ч. вложенные циклы).
/// </summary>
/// <param name="Condition">Условие продолжения цикла (выражение, которое должно быть истинно для продолжения)</param>
/// <param name="Body">Тело цикла — последовательность операций, выполняемых на каждой итерации</param>
public record WhileOperation(Expr Condition, IReadOnlyList<Operation> Body) : Operation;
