namespace UltraDecompiler.PostProcessing.Abstractions;

/// <summary>Один проход постобработки IR перед кодогенерацией.</summary>
public interface IPostProcessPass
{
    string Name { get; }

    IReadOnlyList<Operation> Apply(PostProcessContext context, IReadOnlyList<Operation> operations);
}
