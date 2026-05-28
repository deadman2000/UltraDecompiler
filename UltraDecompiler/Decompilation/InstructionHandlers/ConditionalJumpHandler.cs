namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Базовый класс для обработчиков условных переходов (Jcc и JCXZ).
/// Содержит общую логику установки block.Condition.
/// </summary>
public abstract class ConditionalJumpHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var condition = BuildCondition(block, instr);
        block.Condition = condition;
    }

    /// <summary>
    /// Строит символическое условие для данного перехода на основе состояния регистров.
    /// </summary>
    protected abstract Expr BuildCondition(ExprBlock block, Instruction instr);
}
