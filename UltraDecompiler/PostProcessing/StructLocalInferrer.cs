using UltraDecompiler.Decompilation;
using UltraDecompiler.Headers;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Восстанавливает локальные переменные типа <c>struct</c> из заголовков QuickC
/// по размеру кадра и аргументам вызовов (<c>struct T *</c>).
/// </summary>
public static class StructLocalInferrer
{
    /// <summary>
    /// Помечает стековые локали как <c>struct</c> и объединяет слова одной структуры.
    /// </summary>
    public static void Infer(
        DisassembledProcedure procedure,
        IEnumerable<Operation> operations,
        ProcedureStorage storage,
        HeaderCatalog headers)
    {
        if (procedure.Expressions is null)
        {
            return;
        }

        var variables = procedure.Expressions.Variables;
        var stackLocals = variables.StackLocals;
        if (stackLocals.Count == 0)
        {
            return;
        }

        var structByName = new Dictionary<string, StructDefinition>(StringComparer.Ordinal);

        foreach (var op in ExpressionBuilder.EnumerateNested(operations))
        {
            if (!TryGetCall(op, out var call))
            {
                continue;
            }

            if (!TryResolveSignature(call.Name, storage, headers, out var signature) || signature is null)
            {
                continue;
            }

            for (var i = 0; i < signature.Parameters.Count && i < call.Args.Count; i++)
            {
                var paramType = signature.Parameters[i].Type;
                if (!paramType.IsStructPtr || paramType.Pointee?.StructName is not { } structName)
                {
                    continue;
                }

                if (!headers.TryGetStruct(structName, out var definition) || definition is null)
                {
                    continue;
                }

                structByName[structName] = definition;

                if (call.Args[i] is not Variable variable || !variable.IsStack || variable.ArraySize is not null)
                {
                    continue;
                }

                variable.Type = definition.CType;
                variable.ArraySize = null;

                var match = stackLocals.FirstOrDefault(e => ReferenceEquals(e.Variable, variable));
                if (match.Variable is not null)
                {
                    variables.ConsolidateStructLocal(match.Offset, definition);
                }
            }
        }

    }

    private static bool TryGetCall(Operation op, out CallExpr call)
    {
        switch (op)
        {
            case SetOperation { Src: CallExpr setCall }:
                call = setCall;
                return true;
            case CallOperation callOp:
                call = new CallExpr(callOp.Name, callOp.Args);
                return true;
            default:
                call = null!;
                return false;
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
}
