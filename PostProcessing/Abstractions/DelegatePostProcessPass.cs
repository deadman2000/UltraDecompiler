namespace UltraDecompiler.PostProcessing.Abstractions;

/// <summary>Обёртка над функцией post-process pass.</summary>
internal sealed class DelegatePostProcessPass : IPostProcessPass
{
    private readonly Func<PostProcessContext, IReadOnlyList<Operation>, IReadOnlyList<Operation>> _apply;

    public DelegatePostProcessPass(
        string name,
        Func<PostProcessContext, IReadOnlyList<Operation>, IReadOnlyList<Operation>> apply)
    {
        Name = name;
        _apply = apply;
    }

    public string Name { get; }

    public IReadOnlyList<Operation> Apply(PostProcessContext context, IReadOnlyList<Operation> operations) =>
        _apply(context, operations);
}
