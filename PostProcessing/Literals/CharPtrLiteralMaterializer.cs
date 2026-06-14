using UltraDecompiler.Disassembly.Parser;
using UltraDecompiler.Ir.Helpers;
using UltraDecompiler.PostProcessing.Infrastructure;

namespace UltraDecompiler.PostProcessing.Literals;

/// <summary>
/// Материализует строковые литералы для аргументов <c>char*</c> после вывода типов указателей.
/// </summary>
public static class CharPtrLiteralMaterializer
{
    /// <summary>
    /// Заменяет near-адреса на <see cref="StringExpr"/> для параметров <c>char*</c> в вызовах.
    /// </summary>
    public static IReadOnlyList<Operation> MaterializeCalls(
        IReadOnlyList<Operation> operations,
        ProcedureStorage storage,
        byte[] image,
        ExeImageLayout layout)
    {
        return MaterializeList(operations.ToList(), storage, image, layout);
    }

    private static List<Operation> MaterializeList(
        List<Operation> operations,
        ProcedureStorage storage,
        byte[] image,
        ExeImageLayout layout)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = MaterializeNested(operations[i], storage, image, layout);
        }

        return operations;
    }

    private static Operation MaterializeNested(
        Operation operation,
        ProcedureStorage storage,
        byte[] image,
        ExeImageLayout layout) =>
        operation switch
        {
            SetOperation set when set.Src is CallExpr call =>
                new SetOperation(set.Dst, MaterializeCall(call, storage, image, layout)),
            CallOperation call => new CallOperation(call.Name, MaterializeCallArgs(call, storage, image, layout)),
            IfOperation branch => new IfOperation(
                branch.Condition,
                MaterializeList(branch.ThenBody.ToList(), storage, image, layout),
                branch.ElseBody is not null
                    ? MaterializeList(branch.ElseBody.ToList(), storage, image, layout)
                    : null),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                MaterializeList(loop.Body.ToList(), storage, image, layout)),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? MaterializeNested(loop.Init, storage, image, layout) : null,
                loop.Condition,
                loop.Iteration is not null ? MaterializeNested(loop.Iteration, storage, image, layout) : null,
                MaterializeList(loop.Body.ToList(), storage, image, layout)),
            SwitchOperation sw => OperationTreeMapper.MapSwitchBodies(
                sw,
                bodies => MaterializeList(bodies.ToList(), storage, image, layout)),
            _ => operation,
        };

    private static IReadOnlyList<Expr> MaterializeCallArgs(
        CallOperation call,
        ProcedureStorage storage,
        byte[] image,
        ExeImageLayout layout) =>
        MaterializeCall(new CallExpr(call.Name, call.Args), storage, image, layout).Args;

    private static CallExpr MaterializeCall(
        CallExpr call,
        ProcedureStorage storage,
        byte[] image,
        ExeImageLayout layout)
    {
        if (!storage.TryGetByName(call.Name, out var target) || target is null)
        {
            return call;
        }

        var sig = target.Signature;
        if (sig.Parameters.Count == 0)
        {
            return call;
        }

        var newArgs = new List<Expr>(call.Args);
        var changed = false;

        for (var i = 0; i < sig.Parameters.Count && i < newArgs.Count; i++)
        {
            if (!sig.Parameters[i].Type.IsCharPtr || newArgs[i] is StringExpr)
            {
                continue;
            }

            var mat = StringLiteralMaterializer.TryMaterialize(image, newArgs[i], layout);
            if (mat is null)
            {
                continue;
            }

            newArgs[i] = mat;
            changed = true;
        }

        return changed ? new CallExpr(call.Name, newArgs) : call;
    }
}
