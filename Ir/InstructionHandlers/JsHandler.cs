namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обработчик JS (Jump if Sign) — переход при SF=1 (отрицательный результат).
/// </summary>
public class JsHandler : ConditionalJumpHandler
{
    protected override Expr BuildCondition(ExprBlock block, Instruction instr)
    {
        return block.Variables.SF.ToGet();
    }
}
