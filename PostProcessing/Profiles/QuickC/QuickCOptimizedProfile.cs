using UltraDecompiler.Compilation;
using UltraDecompiler.PostProcessing.Abstractions;
using UltraDecompiler.PostProcessing.Literals;
using UltraDecompiler.PostProcessing.Normalization;
using UltraDecompiler.PostProcessing.Stack;
using UltraDecompiler.PostProcessing.Types;

namespace UltraDecompiler.PostProcessing.Profiles.QuickC;

/// <summary>
/// Профиль QuickC для оптимизированной компиляции (<c>/Ot</c>, <c>/Ox</c>).
/// </summary>
public sealed class QuickCOptimizedProfile : IDecompilationProfile
{
    public static QuickCOptimizedProfile Instance { get; } = new();

    public OptimizationLevel OptimizationLevel => OptimizationLevel.Enabled;

    public void ApplyIrConstructionPasses(IrConstructionContext context)
    {
        // Оптимизированный код не использует Od-эпилоги и tail-return паттерны QuickC.
    }

    public IReadOnlyList<IPostProcessPass> GetProcedurePasses() => ProcedurePasses;

    public IReadOnlyList<IPostProcessPass> GetGlobalPasses() => [];

    public IReadOnlyList<IPostProcessPass> GetDiagnosticPasses() => DiagnosticPasses;

    private static readonly IReadOnlyList<IPostProcessPass> DiagnosticPasses =
    [
        new DelegatePostProcessPass(
            "StackCheckDetector.RemoveChkstkCalls",
            static (_, ops) => StackCheckDetector.RemoveChkstkCalls(ops)),
        new DelegatePostProcessPass(
            nameof(OperationOptimizer),
            static (_, ops) => OperationOptimizer.Optimize(ops)),
    ];

    private static readonly IReadOnlyList<IPostProcessPass> ProcedurePasses =
    [
        new DelegatePostProcessPass(
            "StackCheckDetector.RemoveChkstkCalls",
            static (_, ops) => StackCheckDetector.RemoveChkstkCalls(ops)),
        new DelegatePostProcessPass(
            nameof(OperationOptimizer),
            static (_, ops) => OperationOptimizer.Optimize(ops)),
        new DelegatePostProcessPass(
            nameof(VariableTypeInferrer),
            static (ctx, ops) =>
            {
                VariableTypeInferrer.Infer(ops, ctx.Storage, ctx.HeaderCatalog);
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
            nameof(VoidCallNormalizer),
            static (ctx, ops) => VoidCallNormalizer.Normalize(ops, ctx.Storage, ctx.HeaderCatalog)),
        new DelegatePostProcessPass(
            nameof(CharLiteralMaterializer),
            static (ctx, ops) => CharLiteralMaterializer.Materialize(ctx.Storage, ops)),
        new DelegatePostProcessPass(
            nameof(CharPtrLiteralMaterializer),
            static (ctx, ops) => CharPtrLiteralMaterializer.MaterializeCalls(
                ops, ctx.Storage, ctx.Image, ctx.Layout!)),
        new DelegatePostProcessPass(
            nameof(FlagCallLiteralMaterializer),
            static (_, ops) => FlagCallLiteralMaterializer.Materialize(ops)),
    ];
}
