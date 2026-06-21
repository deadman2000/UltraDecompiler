using UltraDecompiler.Common;
using UltraDecompiler.PostProcessing.Abstractions;
using UltraDecompiler.PostProcessing.Epilogue;
using UltraDecompiler.PostProcessing.Literals;
using UltraDecompiler.PostProcessing.Normalization;
using UltraDecompiler.PostProcessing.Stack;
using UltraDecompiler.PostProcessing.Structs;
using UltraDecompiler.PostProcessing.Types;

namespace UltraDecompiler.PostProcessing.Profiles.QuickC;

/// <summary>
/// Профиль QuickC без оптимизации (<c>/Od</c>)
/// </summary>
public sealed class QuickCUnoptimizedProfile : IDecompilationProfile
{
    public static QuickCUnoptimizedProfile Instance { get; } = new();

    public OptimizationLevel OptimizationLevel => OptimizationLevel.Disabled;

    public IReadOnlyList<IPostProcessPass> GetProcedurePasses() => ProcedurePasses;

    private static readonly IReadOnlyList<IPostProcessPass> ProcedurePasses =
    [
        new DelegatePostProcessPass(
            "StackCheckDetector.RemoveChkstkCalls",
            static (_, ops) => StackCheckDetector.RemoveChkstkCalls(ops)),
        new DelegatePostProcessPass(
            nameof(OperationOptimizer),
            static (_, ops) => OperationOptimizer.Optimize(ops)),
        new DelegatePostProcessPass(
            nameof(MainParameterNormalizer),
            static (ctx, ops) => MainParameterNormalizer.Normalize(ctx.Procedure, ops)),
        new DelegatePostProcessPass(
            nameof(VariableTypeInferrer),
            static (ctx, ops) =>
            {
                VariableTypeInferrer.Infer(ops, ctx.Storage, ctx.HeaderCatalog);
                return ops;
            }),
        new DelegatePostProcessPass(
            nameof(LongTypeInferrer),
            static (ctx, ops) => LongTypeInferrer.Infer(ctx.Procedure, ops, ctx.Storage)),
        new DelegatePostProcessPass(
            nameof(StructLocalInferrer),
            static (ctx, ops) =>
            {
                StructLocalInferrer.Infer(ctx.Procedure, ops, ctx.Storage, ctx.HeaderCatalog);
                return ops;
            }),
        new DelegatePostProcessPass(
            nameof(StackLocalArrayInferrer),
            static (ctx, ops) =>
            {
                StackLocalArrayInferrer.Infer(ctx.Procedure, ops);
                return ops;
            }),
        new DelegatePostProcessPass(
            nameof(PointerTypeInferrer),
            static (ctx, ops) =>
            {
                PointerTypeInferrer.Infer(ctx.Procedure, ops, ctx.Storage, ctx.HeaderCatalog);
                return ops;
            }),
        new DelegatePostProcessPass(
            nameof(SignednessInferrer),
            static (ctx, ops) =>
            {
                SignednessInferrer.Infer(ctx.Procedure, ops);
                return ops;
            }),
        new DelegatePostProcessPass(
            "VariableTypeInferrer (2nd)",
            static (ctx, ops) =>
            {
                VariableTypeInferrer.Infer(ops, ctx.Storage, ctx.HeaderCatalog);
                return ops;
            }),
        new DelegatePostProcessPass(
            nameof(FarPointerStackPairInferrer),
            static (ctx, ops) =>
            {
                FarPointerStackPairInferrer.Infer(ctx.Procedure, ops);
                return ops;
            }),
        new DelegatePostProcessPass(
            nameof(FarPointerLocalInferrer),
            static (ctx, ops) => FarPointerLocalInferrer.Infer(ctx.Procedure, ops)),
        new DelegatePostProcessPass(
            nameof(VoidCallNormalizer),
            static (ctx, ops) => VoidCallNormalizer.Normalize(ops, ctx.Storage, ctx.HeaderCatalog)),
        new DelegatePostProcessPass(
            nameof(StructFieldRewriter),
            static (ctx, ops) => StructFieldRewriter.Rewrite(ctx.Procedure, ops, ctx.Storage, ctx.HeaderCatalog)),
        new DelegatePostProcessPass(
            nameof(StructFieldLoadSimplifier),
            static (_, ops) => StructFieldLoadSimplifier.Simplify(ops)),
        new DelegatePostProcessPass(
            "OperationOptimizer (2nd)",
            static (_, ops) => OperationOptimizer.Optimize(ops)),
        new DelegatePostProcessPass(
            nameof(TempVariableEliminator),
            static (_, ops) => TempVariableEliminator.Eliminate(ops)),
        new DelegatePostProcessPass(
            nameof(ShiftCountSimplifier),
            static (_, ops) => ShiftCountSimplifier.Simplify(ops)),
        new DelegatePostProcessPass(
            nameof(VoidReturnNormalizer),
            static (ctx, ops) => VoidReturnNormalizer.Normalize(ctx.Procedure, ops)),
        new DelegatePostProcessPass(
            nameof(CharLiteralMaterializer),
            static (ctx, ops) => CharLiteralMaterializer.Materialize(ctx.Storage, ops)),
        new DelegatePostProcessPass(
            nameof(CharPtrLiteralMaterializer),
            static (ctx, ops) => CharPtrLiteralMaterializer.MaterializeCalls(
                ops, ctx.Storage, ctx.Image, ctx.Layout!)),
        new DelegatePostProcessPass(
            "OperationOptimizer (3rd)",
            static (_, ops) => OperationOptimizer.Optimize(ops)),
        new DelegatePostProcessPass(
            nameof(LongTypeInferrer.RewriteCallArguments),
            static (ctx, ops) => LongTypeInferrer.RewriteCallArguments(ops, ctx.Storage)),
    ];
}