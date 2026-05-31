namespace UltraDecompiler.Decompilation;

/// <summary>
/// Подставляет аргументы вызова из символического стека и регистров по сигнатуре callee.
/// </summary>
public static class CallSiteArgumentResolver
{
    /// <summary>
    /// Строит список выражений-аргументов и снимает переданные слова со стека.
    /// </summary>
    public static IReadOnlyList<Expr> Resolve(ExprBlock block, ProcedureSignature signature)
    {
        var stackCount = signature.StackParameterCount;
        var stackArgs = TakeStackArguments(block.EndStack, stackCount);

        var args = new List<Expr>(signature.Parameters.Count);
        var stackIndex = 0;

        foreach (var parameter in signature.Parameters)
        {
            switch (parameter.Location)
            {
                case StackParameter:
                    if (stackIndex < stackArgs.Count)
                    {
                        args.Add(stackArgs[stackIndex++]);
                    }
                    else
                    {
                        args.Add(new ConstExpr(0));
                    }

                    break;

                case RegisterParameter(var reg):
                    args.Add(block.EndRegisters.Get16(reg));
                    break;
            }
        }

        return args;
    }

    /// <summary>Снимает все слова со стека как список аргументов (cdecl, для variadic).</summary>
    public static IReadOnlyList<Expr> ResolveAllFromStack(Stack<Expr> stack)
    {
        var count = stack.Count;
        if (count == 0)
        {
            return [];
        }

        return TakeStackArguments(stack, count);
    }

    /// <summary>
    /// Восстанавливает аргументы по последовательности PUSH перед CALL (cdecl).
    /// </summary>
    public static IReadOnlyList<Expr> ResolveFromPushSequence(
        ExprBlock block,
        IReadOnlyList<Instruction> blockInstructions,
        Instruction callInstruction)
    {
        var callIndex = -1;
        for (var i = 0; i < blockInstructions.Count; i++)
        {
            if (blockInstructions[i].Offset == callInstruction.Offset)
            {
                callIndex = i;
                break;
            }
        }

        if (callIndex < 0)
        {
            return [];
        }

        var pushed = new List<Expr>();
        for (var i = callIndex - 1; i >= 0; i--)
        {
            var instr = blockInstructions[i];
            if (instr.Mnemonic != Mnemonic.PUSH)
            {
                break;
            }

            pushed.Add(instr.Operand1.GetExpression(block, instr.Segment));
        }

        pushed.Reverse();
        return pushed;
    }

    private static List<Expr> TakeStackArguments(Stack<Expr> stack, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        // Stack.ToArray(): index 0 — вершина (последний PUSH = первый параметр C, cdecl).
        // Стек не сжимаем: очистку аргументов выполняет вызывающий код (ADD SP / POP).
        var items = stack.ToArray();
        var takeCount = Math.Min(count, items.Length);
        return items.Take(takeCount).ToList();
    }
}
