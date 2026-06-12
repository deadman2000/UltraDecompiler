using UltraDecompiler.Decompilation;
using UltraDecompiler.Headers;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Выводит типы локальных переменных по сигнатурам вызовов из заголовков QuickC
/// и записывает их в <see cref="Variable.Type"/>.
/// </summary>
public static class VariableTypeInferrer
{
    /// <summary>
    /// Заполняет <see cref="Variable.Type"/> у переменных, встречающихся в операциях.
    /// </summary>
    public static void Infer(
        IEnumerable<Operation> operations,
        ProcedureStorage storage,
        HeaderCatalog headers)
    {
        var changed = true;

        while (changed)
        {
            changed = false;
            foreach (var op in ExpressionBuilder.EnumerateNested(operations))
            {
                if (InferFromOperation(op, storage, headers))
                {
                    changed = true;
                }

                if (RefineFromUsage(op, storage, headers))
                {
                    changed = true;
                }
            }
        }
    }

    private static bool InferFromOperation(
        Operation op,
        ProcedureStorage storage,
        HeaderCatalog headers)
    {
        if (op is not SetOperation set)
        {
            return false;
        }

        if (!AssignmentTarget.TryGetVariable(set.Dst, out var dstVar))
        {
            return false;
        }

        if (set.Src is Variable srcVar && srcVar.Type is not null)
        {
            return VariableSignedness.TrySetType(dstVar, srcVar.Type);
        }

        CType? inferred = set.Src switch
        {
            CallExpr call => ResolveCallReturnType(call.Name, storage, headers),
            _ => null,
        };

        if (inferred is null || !ShouldApplyInferredType(dstVar.Type, inferred))
        {
            return false;
        }

        dstVar.Type = inferred;
        return true;
    }

    /// <summary>
    /// Не затирает уточнённый указатель (char* и т.д.) обратно в void* из сигнатуры malloc/calloc.
    /// </summary>
    private static bool ShouldApplyInferredType(CType? current, CType inferred)
    {
        if (current == inferred)
        {
            return false;
        }

        if (current is not null
            && inferred.IsVoidPtr
            && current.Kind == CTypeKind.Pointer
            && !current.IsVoidPtr)
        {
            return false;
        }

        return VariableSignedness.CanApplyType(current, inferred);
    }

    /// <summary>
    /// Уточняет void* по использованию: индексация и аргументы char* требуют конкретного типа указателя.
    /// </summary>
    private static bool RefineFromUsage(
        Operation op,
        ProcedureStorage storage,
        HeaderCatalog headers)
    {
        return op switch
        {
            StoreOperation store when PointerStoreFormatter.TryGetIndexedPointer(store, out var ptr, out _)
                => TryUpgradeVoidPtrToCharPtr(ptr),
            CallOperation call => RefineFromCallArgs(call.Name, call.Args, storage, headers),
            _ => false,
        };
    }

    private static bool RefineFromCallArgs(
        string name,
        IReadOnlyList<Expr> args,
        ProcedureStorage storage,
        HeaderCatalog headers)
    {
        if (!TryResolveSignature(name, storage, headers, out var signature) || signature is null)
        {
            return false;
        }

        var changed = false;

        for (var i = 0; i < signature.Parameters.Count && i < args.Count; i++)
        {
            if (!signature.Parameters[i].Type.IsCharPtr || args[i] is not Variable variable)
            {
                continue;
            }

            if (TryUpgradeVoidPtrToCharPtr(variable))
            {
                changed = true;
            }
        }

        return changed;
    }

    private static bool TryUpgradeVoidPtrToCharPtr(Variable variable)
    {
        if (variable.Type is not { IsVoidPtr: true })
        {
            return false;
        }

        variable.Type = CType.CharPtr;
        return true;
    }

    private static CType? ResolveCallReturnType(string name, ProcedureStorage storage, HeaderCatalog headers)
    {
        if (!TryResolveSignature(name, storage, headers, out var signature) || signature is null)
        {
            return null;
        }

        return signature.ReturnType;
    }

    private static bool TryResolveSignature(
        string name,
        ProcedureStorage storage,
        HeaderCatalog headers,
        out ProcedureSignature? signature)
    {
        if (storage.TryGetByName(name, out var procedure)
            && procedure is not null
            && procedure.Signature != ProcedureSignature.Unknown)
        {
            signature = procedure.Signature;
            return true;
        }

        if (headers.TryGetSignature(name, out signature) && signature is not null)
        {
            return true;
        }

        signature = null;
        return false;
    }
}
