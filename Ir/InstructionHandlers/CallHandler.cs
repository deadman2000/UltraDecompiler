using UltraDecompiler.Ir.Calls;
using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает CALL и CALL_FAR.
/// </summary>
public class CallHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var op = instr.Operand1.IsSet ? instr.Operand1 : instr.Operand2;
        string name;
        IReadOnlyList<Expr> args;
        CallState? callState = null;

        if (op.Type == OperandType.Relative16)
        {
            var target = op.Value;
            name = block.CalleeNameResolver?.Invoke(target) ?? $"sub_{target:X4}";

            // Запоминаем адрес перехода + полное состояние на момент вызова в отдельном CallState.
            // Аргументы НЕ вычисляем здесь. Их определим после того,
            // как будет проанализирована целевая функция (её сигнатура:
            // какие параметры, через стек или регистры).
            var pushArgs = CallSiteArgumentResolver.ResolveFromPushSequence(
                block, block.BasicBlock.Instructions, instr);

            callState = new CallState
            {
                TargetOffset = target,
                CallSiteStack = block.EndStack.ToArray(),
                CallSitePushArgs = pushArgs
            };

            args = [];
        }
        else if (instr.Mnemonic == Mnemonic.CALL_FAR)
        {
            name = "far_sub";
            args = BuildIndirectTargetArgs(block, op, instr);
        }
        else if (op.Type == OperandType.Memory || op.Type == OperandType.Register16)
        {
            name = "indirect_call";
            args = BuildIndirectTargetArgs(block, op, instr);
        }
        else
        {
            name = "unknown_call";
            args = [];
        }

        var callExpr = new CallExpr(name, args)
        {
            CallState = callState
        };

        // Все вызовы (в т.ч. к библиотечным) моделируем как возвращающие значение в AX.
        // Это консервативно для dataflow. Если callee на самом деле void (по проанализированной сигнатуре),
        // то в CCodeGenerator или дополнительном этапе можно убрать присваивание (мёртвое).
        block.Set(block.Variables.AX, callExpr);
    }

    private static List<Expr> BuildIndirectTargetArgs(ExprBlock block, Operand op, Instruction instr)
    {
        var args = new List<Expr>();
        if (op.Type == OperandType.Memory || op.Type == OperandType.Register16)
        {
            args.Add(op.GetExpression(block, instr.Segment));
        }

        return args;
    }
}
