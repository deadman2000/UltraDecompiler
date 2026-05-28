using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает инструкции LOOP, LOOPE/LOOPZ, LOOPNE/LOOPNZ.
/// 
/// Всегда выполняет CX = CX - 1, затем решает, брать ли ConditionalBlock:
/// - LOOP   : переход, если CX != 0
/// - LOOPE  : переход, если CX != 0 && ZF == 1
/// - LOOPNE : переход, если CX != 0 && ZF == 0
/// 
/// Декремент CX всегда эмитится как SetOperation (как и другие арифметические обновления CX).
/// </summary>
public class LoopHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        if (block.BasicBlock.ConditionalBlock == null)
        {
            throw new InvalidOperationException(
                $"LOOP instruction without ConditionalBlock at {block.BasicBlock.StartOffset:X6}");
        }

        // Получаем текущее значение CX
        Expr cxCurrent = block.EndRegisters.Get16(GpRegister16.CX);

        // Вычисляем CX - 1
        Expr cxNew = cxCurrent.Calculate(Math2Operation.Sub, ConstExpr.One);

        // Создаём именованную переменную для нового значения CX (как для обычной арифметики)
        if (cxNew is not ConstExpr)
        {
            var cxVar = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(cxVar, cxNew));
            cxNew = cxVar;
        }
        // Для константного CX просто обновляем регистр (без лишней операции)

        // Обновляем символическое состояние регистров
        block.EndRegisters = block.EndRegisters.Set16(GpRegister16.CX, cxNew);

        // Строим условие перехода
        Expr cxNotZero = new CmpExpr(CmpOperation.Ne, cxNew, ConstExpr.Zero);

        Expr condition = instr.Mnemonic switch
        {
            Mnemonic.LOOP => cxNotZero,

            Mnemonic.LOOPE => cxNotZero & block.EndRegisters.ZF,

            Mnemonic.LOOPNE => cxNotZero & !block.EndRegisters.ZF,

            _ => cxNotZero
        };

        block.Condition = condition;
    }
}
