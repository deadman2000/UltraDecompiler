namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает CALL и CALL_FAR.
///
/// Прямые near-вызовы: имя и сигнатура из <see cref="ProcedureStorage"/>.
/// Аргументы подставляются из символического стека (cdecl) по сигнатуре callee.
/// </summary>
public class CallHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var op = instr.Operand1.IsSet ? instr.Operand1 : instr.Operand2;
        string name;
        ProcedureSignature signature = ProcedureSignature.Unknown;
        IReadOnlyList<Expr> args;

        if (op.Type == OperandType.Relative16)
        {
            var target = op.Value;
            if (block.Procedures?.TryGet(target, out var procedure) == true && procedure is not null)
            {
                name = procedure.Name;
                signature = procedure.Signature;
            }
            else
            {
                name = block.Procedures?.GetName(target) ?? $"sub_{target:X4}";
            }

            args = ResolveArguments(block, instr, signature);
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

        var callExpr = new CallExpr(name, args);

        if (signature.ReturnType.IsVoid)
        {
            block.Operations.Add(new CallOperation(callExpr.Name, callExpr.Args));
            return;
        }

        var resultVar = block.Variables.CreateVariable();
        block.Operations.Add(new SetOperation(resultVar, callExpr));
        block.EndRegisters = block.EndRegisters.Set16(GpRegister16.AX, resultVar);
    }

    private static IReadOnlyList<Expr> ResolveArguments(
        ExprBlock block,
        Instruction callInstruction,
        ProcedureSignature signature)
    {
        if (signature.IsVariadic)
        {
            var fromStack = CallSiteArgumentResolver.ResolveAllFromStack(block.EndStack);
            if (fromStack.Count > 0)
            {
                return fromStack;
            }

            var fromPush = CallSiteArgumentResolver.ResolveFromPushSequence(
                block,
                block.BasicBlock.Instructions,
                callInstruction);

            if (fromPush.Count > 0)
            {
                return fromPush;
            }
        }

        if (signature.Parameters.Count == 0)
        {
            var fromPush = CallSiteArgumentResolver.ResolveFromPushSequence(
                block,
                block.BasicBlock.Instructions,
                callInstruction);

            if (fromPush.Count > 0)
            {
                return fromPush;
            }
        }

        if (signature.Parameters.Count > 0)
        {
            return CallSiteArgumentResolver.Resolve(block, signature);
        }

        return [];
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
