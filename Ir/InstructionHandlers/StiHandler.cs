namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает инструкцию STI → вызов _enable() (включение аппаратных прерываний).
/// </summary>
public class StiHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        block.Operations.Add(new CallOperation("_enable", []));
    }
}
