namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает инструкции возврата.
/// </summary>
public class RetHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        block.Operations.Add(new ReturnOperation(block.Variables.AX.ToGet()));
    }
}
