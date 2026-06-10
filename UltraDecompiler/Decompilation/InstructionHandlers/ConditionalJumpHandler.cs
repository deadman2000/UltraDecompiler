using UltraDecompiler.PostProcessing;

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
        ApplyComparisonSignedness(block, instr.Mnemonic);
    }

    /// <summary>
    /// Если переход следует сразу за CMP, помечает сравниваемые переменные знаковыми или беззнаковыми.
    /// </summary>
    private static void ApplyComparisonSignedness(ExprBlock block, Mnemonic mnemonic)
    {
        if (block.PreviousMnemonic != Mnemonic.CMP || block.LastComparisonOperands is not { } cmp)
        {
            return;
        }

        if (VariableSignedness.IsUnsignedConditionalJump(mnemonic))
        {
            VariableSignedness.MarkUnsigned(cmp.Left);
            VariableSignedness.MarkUnsigned(cmp.Right);
        }
        else if (VariableSignedness.IsSignedConditionalJump(mnemonic))
        {
            VariableSignedness.MarkSigned(cmp.Left);
            VariableSignedness.MarkSigned(cmp.Right);
        }
    }

    /// <summary>
    /// Строит символическое условие для данного перехода на основе состояния регистров.
    /// </summary>
    protected abstract Expr BuildCondition(ExprBlock block, Instruction instr);
}
