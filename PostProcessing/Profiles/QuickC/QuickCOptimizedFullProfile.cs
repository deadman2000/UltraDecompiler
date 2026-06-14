using UltraDecompiler.Compilation;
using UltraDecompiler.PostProcessing.Abstractions;
using UltraDecompiler.PostProcessing.Epilogue;
using UltraDecompiler.PostProcessing.Literals;
using UltraDecompiler.PostProcessing.Loops;
using UltraDecompiler.PostProcessing.Normalization;
using UltraDecompiler.PostProcessing.Stack;
using UltraDecompiler.PostProcessing.Structs;
using UltraDecompiler.PostProcessing.Types;

namespace UltraDecompiler.PostProcessing.Profiles.QuickC;

/// <summary>
/// Профиль QuickC для оптимизированной компиляции (<c>/Ox</c>).
/// В отличие от unopt-профиля не применяет TailReturnInserter и Od-специфичные эпилог/структ/фар-пассы.
/// </summary>
public sealed class QuickCOptimizedFullProfile : IDecompilationProfile
{
    public static QuickCOptimizedFullProfile Instance { get; } = new();

    public OptimizationLevel OptimizationLevel => OptimizationLevel.EnabledFull;

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
            nameof(StackLocalArrayInferrer),
            static (ctx, ops) =>
            {
                StackLocalArrayInferrer.Infer(ctx.Procedure, ops);
                return ops;
            }),
        new DelegatePostProcessPass(
            nameof(StructLocalInferrer),
            static (ctx, ops) =>
            {
                StructLocalInferrer.Infer(ctx.Procedure, ops, ctx.Storage, ctx.HeaderCatalog);
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
            static (ctx, ops) => StructFieldLoadSimplifier.Simplify(ctx.Procedure, ops)),
        new DelegatePostProcessPass(
            "OperationOptimizer (2nd)",
            static (_, ops) => OperationOptimizer.Optimize(ops)),
        new DelegatePostProcessPass(
            nameof(CommutativeOperationNormalizer),
            static (_, ops) => CommutativeOperationNormalizer.Normalize(ops)),
        new DelegatePostProcessPass(
            nameof(IncDecSequenceNormalizer),
            static (_, ops) => IncDecSequenceNormalizer.Normalize(ops)),
        new DelegatePostProcessPass(
            nameof(ShiftCountSimplifier),
            static (_, ops) => ShiftCountSimplifier.Simplify(ops)),
        new DelegatePostProcessPass(
            nameof(IfElseReturnFlattener),
            static (_, ops) => IfElseReturnFlattener.Flatten(ops)),
        new DelegatePostProcessPass(
            nameof(VoidReturnNormalizer),
            static (ctx, ops) => VoidReturnNormalizer.Normalize(ctx.Procedure, ops)),
        new DelegatePostProcessPass(
            nameof(IfEarlyReturnInverter),
            static (_, ops) => IfEarlyReturnInverter.Invert(ops)),
        new DelegatePostProcessPass(
            nameof(ReturnBranchSwapper),
            static (_, ops) => ReturnBranchSwapper.Swap(ops)),
        new DelegatePostProcessPass(
            nameof(BooleanPredicateReturnNormalizer),
            static (ctx, ops) => BooleanPredicateReturnNormalizer.Normalize(ctx.Procedure, ops)),
        new DelegatePostProcessPass(
            nameof(PointerCompareSimplifier),
            static (_, ops) => PointerCompareSimplifier.Simplify(ops)),
        new DelegatePostProcessPass(
            nameof(CharLiteralMaterializer),
            static (ctx, ops) => CharLiteralMaterializer.Materialize(ctx.Storage, ops)),
        new DelegatePostProcessPass(
            nameof(FlagCallLiteralMaterializer),
            static (_, ops) => FlagCallLiteralMaterializer.Materialize(ops)),
        new DelegatePostProcessPass(
            nameof(CharPtrLiteralMaterializer),
            static (ctx, ops) => CharPtrLiteralMaterializer.MaterializeCalls(
                ops, ctx.Storage, ctx.Image, ctx.Layout!)),
        new DelegatePostProcessPass(
            nameof(WhileLoopRecognizer),
            static (_, ops) => WhileLoopRecognizer.Convert(ops)),
        new DelegatePostProcessPass(
            nameof(CounterLoopRecognizer),
            static (_, ops) => CounterLoopRecognizer.Convert(ops)),
        new DelegatePostProcessPass(
            nameof(OxRegisterCounterLoopRecognizer),
            static (ctx, ops) => OxRegisterCounterLoopRecognizer.Convert(ctx.Procedure, ops)),
        new DelegatePostProcessPass(
            nameof(ArgvEnvpLoopSimplifier),
            static (_, ops) => ArgvEnvpLoopSimplifier.Simplify(ops)),
        new DelegatePostProcessPass(
            "WhileLoopRecognizer (2nd)",
            static (_, ops) => WhileLoopRecognizer.Convert(ops)),
        new DelegatePostProcessPass(
            "CounterLoopRecognizer (2nd)",
            static (_, ops) => CounterLoopRecognizer.Convert(ops)),
        new DelegatePostProcessPass(
            nameof(ArgvIterationNormalizer),
            static (_, ops) => ArgvIterationNormalizer.Normalize(ops)),
        new DelegatePostProcessPass(
            nameof(ArgvVerboseContinueNormalizer),
            static (_, ops) => ArgvVerboseContinueNormalizer.Normalize(ops)),
        new DelegatePostProcessPass(
            nameof(ArgvLoopIncrementHoister),
            static (_, ops) => ArgvLoopIncrementHoister.Hoist(ops)),
        new DelegatePostProcessPass(
            "ArgvIterationNormalizer (2nd)",
            static (_, ops) => ArgvIterationNormalizer.Normalize(ops)),
        new DelegatePostProcessPass(
            nameof(ArgvEnvpForLoopRecognizer),
            static (_, ops) => ArgvEnvpForLoopRecognizer.Convert(ops)),
        new DelegatePostProcessPass(
            nameof(IfElseReturnFlattener.FlattenSingleSidedReturns),
            static (_, ops) => IfElseReturnFlattener.FlattenSingleSidedReturns(ops)),
        new DelegatePostProcessPass(
            nameof(NullGuardSequenceNormalizer),
            static (_, ops) => NullGuardSequenceNormalizer.Normalize(ops)),
        new DelegatePostProcessPass(
            nameof(PointerLoopBodySimplifier),
            static (_, ops) => PointerLoopBodySimplifier.Simplify(ops)),
        new DelegatePostProcessPass(
            "OperationOptimizer (3rd)",
            static (_, ops) => OperationOptimizer.Optimize(ops)),
        new DelegatePostProcessPass(
            nameof(UnreachableOperationTrimmer),
            static (_, ops) => UnreachableOperationTrimmer.Trim(ops)),
        new DelegatePostProcessPass(
            nameof(LongTypeInferrer.RewriteCallArguments),
            static (ctx, ops) => LongTypeInferrer.RewriteCallArguments(ops, ctx.Storage)),
    ];
}
