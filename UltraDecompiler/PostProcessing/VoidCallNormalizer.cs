using UltraDecompiler.Decompilation;
using UltraDecompiler.Headers;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Преобразует присваивания вида <c>temp = void_fn(...)</c> в отдельный вызов <c>void_fn(...)</c>.
/// </summary>
public static class VoidCallNormalizer
{
    public static IReadOnlyList<Operation> Normalize(
        IReadOnlyList<Operation> operations,
        ProcedureStorage storage,
        HeaderCatalog headers) =>
        operations.Select(op => NormalizeOperation(op, storage, headers)).ToList();

    private static Operation NormalizeOperation(
        Operation op,
        ProcedureStorage storage,
        HeaderCatalog headers) =>
        op switch
        {
            SetOperation { Src: CallExpr call } set
                when AssignmentTarget.TryGetVariable(set.Dst, out var dst) && dst.IsTemp
                && TryGetReturnType(call.Name, storage, headers, out var returnType)
                && returnType!.IsVoid =>
                new CallOperation(call.Name, call.Args),
            IfOperation branch => new IfOperation(
                branch.Condition,
                NormalizeList(branch.ThenBody, storage, headers),
                branch.ElseBody is null ? null : NormalizeList(branch.ElseBody, storage, headers)),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                NormalizeList(loop.Body, storage, headers)),
            ForOperation loop => new ForOperation(
                loop.Init is null ? null : NormalizeOperation(loop.Init, storage, headers),
                loop.Condition,
                loop.Iteration is null ? null : NormalizeOperation(loop.Iteration, storage, headers),
                NormalizeList(loop.Body, storage, headers)),
            _ => op,
        };

    private static List<Operation> NormalizeList(
        IReadOnlyList<Operation> operations,
        ProcedureStorage storage,
        HeaderCatalog headers) =>
        operations.Select(op => NormalizeOperation(op, storage, headers)).ToList();

    private static bool TryGetReturnType(
        string name,
        ProcedureStorage storage,
        HeaderCatalog headers,
        out CType? returnType)
    {
        if (storage.TryGetByName(name, out var procedure)
            && procedure is not null
            && procedure.Signature != ProcedureSignature.Unknown)
        {
            returnType = procedure.Signature.ReturnType;
            return true;
        }

        if (headers.TryGetSignature(name, out var signature) && signature is not null)
        {
            returnType = signature.ReturnType;
            return true;
        }

        returnType = null;
        return false;
    }
}
