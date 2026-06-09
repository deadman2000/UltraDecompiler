using UltraDecompiler.Parser;

namespace UltraDecompiler.Decompilation;

/// <summary>
/// Подстановка аргументов вызовов
/// </summary>
public static class CallSiteResolver
{
    /// <summary>
    /// Разрешает все вызовы во всех процедурах хранилища (обновляет Operations in-place).
    /// Должен вызываться после ProcedureSignatureResolver.ResolveAll.
    /// </summary>
    public static void ResolveAll(ProcedureStorage storage, byte[]? image = null, ExeImageLayout? layout = null)
    {
        foreach (var procedure in storage.All)
        {
            ResolveBlocks(procedure.Expressions.Blocks, storage, image, layout);
        }
    }

    /// <summary>
    /// Разрешает вызовы в списке ExprBlock'ов (используется из ExpressionBuilder при Build с procedures,
    /// и из ResolveAll).
    /// </summary>
    public static void ResolveBlocks(IReadOnlyList<ExprBlock> blocks, ProcedureStorage storage, byte[]? image = null, ExeImageLayout? layout = null)
    {
        foreach (var block in blocks)
        {
            ResolveInBlock(block, storage, image, layout);
        }
    }

    private static void ResolveInBlock(ExprBlock block, ProcedureStorage storage, byte[]? image, ExeImageLayout? layout)
    {
        for (var i = 0; i < block.Operations.Count; i++)
        {
            var op = block.Operations[i];

            if (op is SetOperation setOp && setOp.Src is CallExpr callExpr)
            {
                var resolvedExpr = ResolveCallExpr(callExpr, storage, image, layout);
                if (!ReferenceEquals(resolvedExpr, callExpr))
                {
                    block.Operations[i] = new SetOperation(setOp.Dst, resolvedExpr);
                }
            }
            // CallOperation сейчас не участвует в отложенном разрешении по Target/CallState
            // (см. комментарий в ResolveCallOperation).
        }
    }

    private static CallExpr ResolveCallExpr(CallExpr callExpr, ProcedureStorage storage, byte[]? image, ExeImageLayout? layout)
    {
        var state = callExpr.CallState;
        if (state == null)
        {
            // После первого разрешения CallState обычно уже нет. Если у нас есть образ,
            // мы всё равно можем "дотипизировать" аргументы по текущей сигнатуре callee
            // (преобразовать ConstExpr адреса в StringExpr для char* параметров).
            if (image != null && layout != null && storage.TryGetByName(callExpr.Name, out var p) && p != null)
            {
                var calleeSig = p.Signature;
                var materialized = MaterializeCharPtrArgs(callExpr.Args, calleeSig, image, layout);
                if (!ReferenceEquals(materialized, callExpr.Args))
                {
                    return new CallExpr(callExpr.Name, materialized);
                }
            }
            return callExpr;
        }

        if (!storage.TryGet(state.TargetOffset, out var targetProc) || targetProc is null)
            return callExpr;

        var finalName = targetProc.Name;
        var sig = targetProc.Signature;

        IReadOnlyList<Expr> finalArgs;

        // Если есть запомненное состояние, строим аргументы строго по сигнатуре callee
        if (state.CallSiteStack != null || state.CallSitePushArgs != null || state.CallSiteRegisters != null)
        {
            finalArgs = BuildArgsFromCalleeSignature(callExpr, sig, image, layout, storage);
        }
        else
        {
            // CallExpr без снимков состояния — fallback.
            finalArgs = ComputeFinalArgs(callExpr.Args, sig, image);
        }

        if (string.Equals(finalName, callExpr.Name, StringComparison.Ordinal)
            && finalArgs.Count == callExpr.Args.Count)
        {
            return callExpr;
        }

        // Создаём финальный CallExpr с подставленными именем и аргументами.
        // CallState больше не нужен после разрешения.
        return new CallExpr(finalName, finalArgs);
    }

    /// <summary>
    /// Строит список аргументов для CallExpr строго по сигнатуре callee.
    /// Использует запомненное состояние call site (стек + регистры + push-аргументы),
    /// упакованное в CallState.
    /// </summary>
    private static IReadOnlyList<Expr> BuildArgsFromCalleeSignature(CallExpr callSite, ProcedureSignature sig, byte[]? image, ExeImageLayout? layout, ProcedureStorage storage)
    {
        var state = callSite.CallState;
        if (sig.Parameters.Count == 0 && !sig.IsVariadic)
            return [];

        var stackSnap = state?.CallSiteStack ?? [];
        var pushList = state?.CallSitePushArgs;
        var regs = state?.CallSiteRegisters;

        // Выбираем источник stack-слов в зависимости от сигнатуры callee.
        // Для фиксированного числа stack-параметров надёжнее брать top-N из снимка стека
        // на момент CALL (символический стек точно отражает, что было положено для этого вызова,
        // даже если между отдельными push были mov/вычисления).
        // Push-лист (из ResolveFromPushSequence) хорош для variadic (показывает сколько caller реально передал).
        IReadOnlyList<Expr> stackSource;
        if (sig.IsVariadic)
        {
            stackSource = (pushList != null && pushList.Count > 0) ? pushList : stackSnap;
        }
        else
        {
            // Фиксированные stack-параметры — берём из снимка стека на момент вызова.
            stackSource = stackSnap;
        }

        int stackIdx = 0; // потребление с вершины (0 = верхушка на момент CALL)

        var result = new List<Expr>(sig.Parameters.Count);

        foreach (var param in sig.Parameters)
        {
            Expr arg;
            switch (param.Location)
            {
                case StackParameter:
                    arg = stackIdx < stackSource.Count ? stackSource[stackIdx++] : ConstExpr.Zero;
                    break;

                case RegisterParameter(var reg):
                    arg = regs?.Get16(reg) ?? ConstExpr.Zero;
                    break;

                default:
                    arg = ConstExpr.Zero;
                    break;
            }
            result.Add(arg);
        }

        if (sig.IsVariadic && stackSource.Count > stackIdx)
        {
            // Для variadic добавляем то, что caller передал на стек сверх fixed параметров.
            for (int i = stackIdx; i < stackSource.Count; i++)
            {
                result.Add(stackSource[i]);
            }
        }

        if (image != null && layout != null)
            return MaterializeCharPtrArgs(result, sig, image, layout);

        return result;
    }

    private static IReadOnlyList<Expr> ComputeFinalArgs(IReadOnlyList<Expr> capturedArgs, ProcedureSignature sig, byte[]? image)
    {
        // Fallback для CallExpr, у которых не было снимков состояния.
        // Просто берём/подрезаем то, что было захвачено раньше.
        if (sig.IsVariadic)
            return capturedArgs;

        var declared = sig.StackParameterCount;
        if (declared > 0 && capturedArgs.Count >= declared)
            return capturedArgs.Take(declared).ToList();

        if (declared > 0)
            return capturedArgs;

        return capturedArgs;
    }

    /// <summary>
    /// Для уже разрешённых аргументов (без CallState) материализует StringExpr для тех,
    /// чей тип по текущей сигнатуре callee — char*.
    /// </summary>
    private static IReadOnlyList<Expr> MaterializeCharPtrArgs(IReadOnlyList<Expr> args, ProcedureSignature sig, byte[] image, ExeImageLayout layout)
    {
        if (args.Count == 0 || sig.Parameters.Count == 0)
            return args;

        var result = new List<Expr>(args);

        for (int i = 0; i < sig.Parameters.Count && i < result.Count; i++)
        {
            if (sig.Parameters[i].Type.IsCharPtr)
            {
                var mat = StringLiteralMaterializer.TryMaterialize(image, result[i], layout);
                if (mat != null)
                    result[i] = mat;
            }
        }
        return result;
    }
}
