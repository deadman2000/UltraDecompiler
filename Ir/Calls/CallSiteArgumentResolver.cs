using UltraDecompiler.Decompilation;
using UltraDecompiler.Ir.Expressions;
using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.Calls;

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
    /// Восстанавливает аргументы, подготовленные на стеке перед CALL (cdecl).
    /// Собирает выражения из PUSH-инструкций, идущих перед вызовом (даже если между ними есть mov/вычисления).
    /// Останавливается при предыдущем CALL или корректировке SP (add/sub sp — cleanup от предыдущего вызова).
    /// Это позволяет корректно собирать аргументы для variadic (printf и т.п.) и обычных вызовов
    /// в реальном коде QuickC, где подготовка аргументов не всегда "чистая" последовательность PUSH без промежуточных инструкций.
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

        var pushed = new List<(Instruction Instr, Expr Expr)>();
        for (var i = callIndex - 1; i >= 0; i--)
        {
            var instr = blockInstructions[i];

            if (instr.Mnemonic == Mnemonic.CALL || instr.Mnemonic == Mnemonic.CALL_FAR)
            {
                break; // предыдущий вызов — начало подготовки аргументов для текущего
            }

            if (instr.Mnemonic == Mnemonic.ADD || instr.Mnemonic == Mnemonic.SUB)
            {
                var dst = instr.Operand1;
                if (dst.Type == OperandType.Register16 &&
                    dst.AsGpRegister16() == GpRegister16.SP)
                {
                    break; // корректировка стека (cleanup после предыдущего вызова или alloc)
                }
            }

            if (instr.Mnemonic == Mnemonic.PUSH)
            {
                var expr = block.PushExprsByOffset.TryGetValue(instr.Offset, out var recorded)
                    ? recorded
                    : instr.Operand1.GetExpression(block, instr.Segment);
                pushed.Add((instr, expr));
            }
            // продолжаем дальше по блоку, чтобы поймать push после mov reg, val и т.п.
        }

        // QuickC после _chkstk часто делает push DI; push SI до подготовки аргументов следующего вызова.
        // Без этой обрезки variadic (printf) получает лишние нулевые аргументы из сохранённых регистров.
        TrimCalleeSavePushesFromEnd(pushed);

        // Первый элемент — самый близкий к CALL (вершина на момент подготовки последнего push).
        return pushed.ConvertAll(static p => p.Expr);
    }

    /// <summary>
    /// Убирает с конца списка push'и сохранения callee-saved регистров (SI/DI/BX),
    /// не являющиеся аргументами вызова.
    /// </summary>
    private static void TrimCalleeSavePushesFromEnd(List<(Instruction Instr, Expr Expr)> pushed)
    {
        while (pushed.Count > 0 && IsCalleeSaveRegisterPush(pushed[^1].Instr))
        {
            pushed.RemoveAt(pushed.Count - 1);
        }
    }

    private static bool IsCalleeSaveRegisterPush(Instruction instr)
    {
        if (instr.Mnemonic != Mnemonic.PUSH || instr.Operand1.Type != OperandType.Register16)
        {
            return false;
        }

        return instr.Operand1.AsGpRegister16() is GpRegister16.SI or GpRegister16.DI or GpRegister16.BX;
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

    /// <summary>
    /// Возвращает содержимое стека, пригодное для использования в качестве аргументов *исходящего* вызова.
    /// Исключает синтетический "retAddr", который добавляется в BuildProc как моделирование адреса возврата
    /// *текущей* декомпилируемой процедуры. Этот retAddr лежит внизу стековой модели и не является
    /// аргументом для вызываемых функций (в т.ч. _chkstk и других runtime).
    /// </summary>
    public static IReadOnlyList<Expr> ResolveCallArgsFromStack(Stack<Expr> stack)
    {
        var all = ResolveAllFromStack(stack);
        return all.Where(static e => !IsSyntheticReturnAddress(e)).ToList();
    }

    private static bool IsSyntheticReturnAddress(Expr e)
    {
        return e is Variable v && string.Equals(v.Name, "retAddr", StringComparison.Ordinal);
    }
}
