namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JNS (Jump if Not Sign) — переход при SF=0 (неотрицательный результат).
/// </summary>
public class JnsHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return !block.Variables.SF;
    }
}
