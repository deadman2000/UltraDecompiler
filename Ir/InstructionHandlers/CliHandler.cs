namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает инструкцию CLI → вызов _disable() (отключение аппаратных прерываний).
/// </summary>
public class CliHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        block.Operations.Add(new CallOperation("_disable", []));
    }
}
