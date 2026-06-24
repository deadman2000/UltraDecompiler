namespace UltraDecompiler.Decompilation;

/// <summary>Сгенерированный файл декомпиляции (имя без пути и текстовое содержимое).</summary>
public sealed record GeneratedOutputFile
{
    public required string FileName { get; init; }

    public required string Content { get; init; }
}
