namespace UltraDecompiler.Ir.Operations;

/// <summary>
/// Операция цикла do-while (постусловие).
///
/// Используется для точного представления циклов QuickC, где тело выполняется минимум один раз,
/// а проверка условия находится в конце (bottom-tested).
///
/// Отличается от WhileOperation отсутствием начального прыжка на тест в сгенерированном
/// ассемблере QuickC (/Od и /Ox): тело собирается линейно, условие проверяется после обновления.
///
/// Структура соответствует исходному коду:
/// <code>
/// do {
///     Body
/// } while (Condition);
/// </code>
/// </summary>
/// <param name="Condition">Условие продолжения (выполнять тело пока истинно)</param>
/// <param name="Body">Тело цикла (включая возможные обновления счётчика)</param>
public record DoWhileOperation(Expr Condition, IReadOnlyList<Operation> Body) : Operation;
