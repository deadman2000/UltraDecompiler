using UltraDecompiler.Ir.Builder.Patterns;

namespace UltraDecompiler.Ir.Builder;

/// <summary>
/// Выполняет построение IR для программы, скомпилированной в QuickC с отключенной оптимизацией (/Od).
/// Применяет специфичные паттерны декомпиляции для кода без оптимизации:
/// - Распознавание switch-конструкций QuickC
/// - Паттерны push-аргументов через локальные переменные
/// - Специфичные эвристики для циклов
/// </summary>
public class ExpressionBuilderQuickCUnopt : ExpressionBuilder
{
    /// <summary>
    /// Применяет паттерны QuickC /Od к блоку:
    /// - <see cref="StackLocalPushArgPattern"/> — подстановка локалей вместо push reg
    /// </summary>
    protected override void ApplyBlockPatterns(ExprBlock block)
    {
        StackLocalPushArgPattern.Apply(block);
    }

    /// <inheritdoc />
    protected override bool ShouldEmitReturnFromRet(BasicBlock block) =>
        !IsSharedEpilogueReachedByTailJmp(block);

    /// <inheritdoc />
    protected override bool ShouldInsertTailReturnsBeforeEpilogue() => true;
}
