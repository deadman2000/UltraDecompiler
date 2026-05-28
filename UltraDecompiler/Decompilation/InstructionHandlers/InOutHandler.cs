using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает инструкции IN и OUT (ввод/вывод через порты).
/// 
/// Моделируются как вызовы из msdos.h / conio.h стиля:
/// - IN  → inb(port) или inw(port)  (результат записывается в AL/AX)
/// - OUT → outb(port, value) или outw(port, value)
/// 
/// Это соответствует стилю, используемому для _disable/_enable и прерываний.
/// </summary>
public class InOutHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        bool isIn = instr.Mnemonic == Mnemonic.IN;

        // Определяем размер (8 или 16 бит)
        bool isWord;

        if (isIn)
        {
            // IN: результат идёт в AL или AX
            var dest = instr.Operand1;
            isWord = dest.Type == OperandType.Register16 && dest.Value == 0; // AX
        }
        else
        {
            // OUT: источник — AL или AX
            var src = instr.Operand2;
            isWord = src.Type == OperandType.Register16 && src.Value == 0; // AX
        }

        string funcName = isWord ? (isIn ? "inw" : "outw") : (isIn ? "inb" : "outb");

        // Порт: либо Immediate8, либо значение DX
        Expr portExpr;
        if (instr.Operand1.Type == OperandType.Immediate8 || instr.Operand1.Type == OperandType.Immediate16)
        {
            portExpr = new ConstExpr(instr.Operand1.Value);
        }
        else if (instr.Operand2.Type == OperandType.Immediate8 || instr.Operand2.Type == OperandType.Immediate16)
        {
            portExpr = new ConstExpr(instr.Operand2.Value);
        }
        else
        {
            // Порт в DX
            portExpr = block.EndRegisters.Get16(2); // DX = регистр 2
        }

        if (isIn)
        {
            // IN — читаем значение из порта
            var callExpr = new CallExpr(new Procedure { Name = funcName }, new[] { portExpr });

            // Создаём SetOperation и обновляем AL/AX
            var resultVar = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, callExpr));

            if (isWord)
                block.EndRegisters = block.EndRegisters.Set16(0, resultVar); // AX
            else
                block.EndRegisters = block.EndRegisters.Set8(0, resultVar);  // AL
        }
        else
        {
            // OUT — пишем значение в порт
            Expr valueExpr = instr.Operand2.GetExpression(block, instr.Segment);

            var callExpr = new CallExpr(new Procedure { Name = funcName }, new[] { portExpr, valueExpr });
            block.Operations.Add(new CallOperation(callExpr.Procedure, callExpr.Args));
        }
    }
}
