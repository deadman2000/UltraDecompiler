namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает инструкции возврата RET / RET_IMM (основные для near cdecl).
/// 
/// На момент RET текущее символическое значение AX (в EndRegisters) считается возвращаемым.
/// Создаём ReturnOperation — это позволяет ExpressionBuilder и CCodeGenerator генерировать явный "return".
/// Дальнейшая обработка инструкций в блоке прекращается (логика early-exit остаётся в GenerateCode).
/// 
/// RET_IMM (ret N) — возвращаем значение, imm для очистки стека игнорируем (очистка аргументов выполняется caller'ом в cdecl).
/// </summary>
public class RetHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var retVal = block.EndRegisters.Get16(GpRegister16.AX);
        block.Operations.Add(new ReturnOperation(retVal));
    }
}
