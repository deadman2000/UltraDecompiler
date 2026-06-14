namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает NOP (No Operation).
/// Ничего не делает — просто пропускается.
/// </summary>
public class NopHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        // Ничего не делаем
    }
}
