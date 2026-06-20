namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Базовый класс для обработчиков условных переходов (Jcc и JCXZ).
/// Содержит общую логику установки block.Condition.
/// </summary>
/// <summary>
/// Базовый класс для обработчиков условных переходов (Jcc).
/// Содержит общую логику установки block.Condition.
/// </summary>
public abstract class ConditionalJumpHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var condition = CmpJumpConditions.TryBuild(block, instr.Mnemonic, out var cmpCondition) && cmpCondition is not null
            ? cmpCondition
            : BuildCondition(block, instr);
        block.Condition = condition;
    }

    /// <summary>
    /// Строит символическое условие для данного перехода на основе состояния флагов.
    /// </summary>
    protected abstract Expr BuildCondition(ExprBlock block, Instruction instr);
}

