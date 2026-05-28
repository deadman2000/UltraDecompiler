using UltraDecompiler.Disassembler;

namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает CALL и CALL_FAR.
///
/// Прямые near-вызовы (E8) получают имя вида "sub_XXXX" по целевому адресу в образе.
/// Косвенные вызовы (FF/2, FF/3) представляются как indirect_call/far_sub с выражением адреса в аргументах.
///
/// Все вызовы по умолчанию моделируются как возвращающие значение (в AX):
/// создаём SetOperation(resultVar, CallExpr) и обновляем AX = resultVar.
/// Это позволяет продолжать symbolic execution кода, который использует результат вызова.
///
/// Аргументы функций пока не анализируются (требуется восстановление соглашений о вызовах и анализ стека).
/// </summary>
public class CallHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        string name;
        var args = new List<Expr>();

        var op = instr.Operand1.IsSet ? instr.Operand1 : instr.Operand2;

        if (op.Type == OperandType.Relative16)
        {
            // Прямой near call. Target — уже вычисленный абсолютный адрес в образе.
            name = $"sub_{op.Value:X4}";
        }
        else if (instr.Mnemonic == Mnemonic.CALL_FAR)
        {
            name = "far_sub";
            if (op.Type == OperandType.Memory)
            {
                var targetExpr = op.GetExpression(block, instr.Segment);
                args.Add(targetExpr);
            }
        }
        else if (op.Type == OperandType.Memory || op.Type == OperandType.Register16)
        {
            // Косвенный near call (обычно FF /2)
            name = "indirect_call";
            var targetExpr = op.GetExpression(block, instr.Segment);
            args.Add(targetExpr);
        }
        else
        {
            name = "unknown_call";
        }

        var proc = new Procedure { Name = name };
        var callExpr = new CallExpr(proc, args);

        // Моделируем возврат значения через AX (стандартная практика для DOS/QuickC).
        var resultVar = block.Variables.CreateVariable();
        block.Operations.Add(new SetOperation(resultVar, callExpr));
        block.EndRegisters = block.EndRegisters.Set16(0, resultVar);
    }
}
