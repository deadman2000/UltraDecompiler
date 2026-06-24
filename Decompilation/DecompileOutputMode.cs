namespace UltraDecompiler.Decompilation;

/// <summary>Способ сохранения сгенерированных артефактов декомпиляции.</summary>
public enum DecompileOutputMode
{
    /// <summary>Запись .c/.h/Makefile в <see cref="Decompiler"/> outputDirectory.</summary>
    FileSystem,

    /// <summary>Генерация в память: содержимое в <see cref="DecompileResult.GeneratedFiles"/>.</summary>
    InMemory,
}
