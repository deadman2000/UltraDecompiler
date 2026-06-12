using UltraDecompiler.Decompilation;
using UltraDecompiler.Headers;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Выводит типы near-указателей (<c>char*</c>) по использованию в IR и синхронизирует сигнатуры процедур.
/// </summary>
public static class PointerTypeInferrer
{
    /// <summary>
    /// Помечает переменные-указатели и обновляет типы параметров в сигнатуре процедуры.
    /// </summary>
    public static void Infer(
        DisassembledProcedure procedure,
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
                if (InferFromOperation(op))
                {
                    changed = true;
                }
            }
        }

        ApplySignature(procedure);
        PropagateCallArgumentTypes(operations, storage, headers);
        ApplySignature(procedure);
    }

    private static bool InferFromOperation(Operation op)
    {
        return op switch
        {
            StoreOperation store when PointerStoreFormatter.TryGetIndexedPointer(store, out var ptr, out _)
                => TryUpgradeToCharPtr(ptr),
            StoreOperation store when store.Address is Variable ptr && !IsSegmentBase(ptr)
                => TryUpgradeToCharPtr(ptr),
            SetOperation { Src: MemExpr mem } when PointerDerefFormatter.TryGetNearPointerBase(mem, out var ptr)
                => TryUpgradeToCharPtr(ptr),
            SetOperation { Src: Math2Expr { Operation: Math2Operation.Add, First: Variable src }, Dst: var dst }
                when src.Type?.IsCharPtr == true && AssignmentTarget.TryGetVariable(dst, out var dstVar)
                => TryUpgradeToCharPtr(dstVar),
            SetOperation { Src: Math2Expr { Operation: Math2Operation.Add, Second: Variable src2 }, Dst: var dst }
                when src2.Type?.IsCharPtr == true && AssignmentTarget.TryGetVariable(dst, out var dstVar)
                => TryUpgradeToCharPtr(dstVar),
            IfOperation branch => InferFromExpr(branch.Condition)
                || branch.ThenBody.Any(InferFromOperation)
                || (branch.ElseBody?.Any(InferFromOperation) ?? false),
            WhileOperation loop => InferFromExpr(loop.Condition)
                || loop.Body.Any(InferFromOperation),
            ForOperation loop => InferFromExpr(loop.Condition)
                || (loop.Init is not null && InferFromOperation(loop.Init))
                || (loop.Iteration is not null && InferFromOperation(loop.Iteration))
                || loop.Body.Any(InferFromOperation),
            _ => InferFromExpr(GetOperationExpr(op)),
        };
    }

    private static bool InferFromExpr(Expr? expr)
    {
        if (expr is null)
        {
            return false;
        }

        var changed = false;

        foreach (var mem in ExprSubstitution.CollectMemExprs(expr))
        {
            if (PointerDerefFormatter.TryGetNearPointerBase(mem, out var ptr)
                && TryUpgradeToCharPtr(ptr))
            {
                changed = true;
            }
        }

        return changed;
    }

    private static Expr? GetOperationExpr(Operation op) =>
        op switch
        {
            SetOperation set => set.Src,
            StoreOperation store => store.Value,
            CallOperation call => call.Args.FirstOrDefault(),
            ReturnOperation ret => ret.Value,
            _ => null,
        };

    private static bool TryUpgradeToCharPtr(Variable variable)
    {
        if (variable.ArraySize is not null)
        {
            return false;
        }

        if (variable.Type is { IsCharPtrPtr: true })
        {
            return false;
        }

        if (variable.Name is "argc")
        {
            return false;
        }

        if (variable.Type is { IsStruct: true })
        {
            return false;
        }

        if (variable.Type is { IsCharPtr: true })
        {
            return false;
        }

        if (variable.Type is { Kind: CTypeKind.Pointer } && !variable.Type.IsCharPtr)
        {
            return false;
        }

        variable.Type = CType.CharPtr;
        return true;
    }

    private static void ApplySignature(DisassembledProcedure procedure)
    {
        if (procedure.Expressions is null || procedure.Expressions.Parameters.Count == 0)
        {
            return;
        }

        var newParameters = new List<ProcedureParameter>(procedure.Expressions.Parameters.Count);

        foreach (var fp in procedure.Expressions.Parameters)
        {
            var type = fp.Variable.Type ?? CType.Int;
            newParameters.Add(new ProcedureParameter(type, new StackParameter(fp.StackOffset)));
        }

        var returnType = procedure.Signature.ReturnType;
        procedure.Signature = new ProcedureSignature(returnType, newParameters);
    }

    /// <summary>
    /// Если переменная-указатель передаётся в параметр <c>char*</c>, помечает её как <c>char*</c>.
    /// </summary>
    private static void PropagateCallArgumentTypes(
        IEnumerable<Operation> operations,
        ProcedureStorage storage,
        HeaderCatalog headers)
    {
        foreach (var op in ExpressionBuilder.EnumerateNested(operations))
        {
            CallExpr? call = op switch
            {
                SetOperation { Src: CallExpr setCall } => setCall,
                CallOperation callOp => new CallExpr(callOp.Name, callOp.Args),
                _ => null,
            };

            if (call is null)
            {
                continue;
            }

            if (!TryResolveSignature(call.Name, storage, headers, out var signature) || signature is null)
            {
                continue;
            }

            for (var i = 0; i < signature.Parameters.Count && i < call.Args.Count; i++)
            {
                if (signature.Parameters[i].Type.IsStructPtr && call.Args[i] is Variable structVar)
                {
                    if (signature.Parameters[i].Type.Pointee?.StructName is { } structName)
                    {
                        structVar.Type = CType.StructType(structName);
                        structVar.ArraySize = null;
                    }

                    continue;
                }

                if (!signature.Parameters[i].Type.IsCharPtr || call.Args[i] is not Variable variable)
                {
                    continue;
                }

                TryUpgradeToCharPtr(variable);
            }
        }
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

    private static bool IsSegmentBase(Variable variable) =>
        variable.Name is "_psp";
}
