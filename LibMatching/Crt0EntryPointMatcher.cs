using Common;
using LibParser.Models;
using LibParser.Omf;
using UltraDecompiler.Disassembler;

namespace LibMatching;

/// <summary>Результат сопоставления точки входа EXE с одной OMF-библиотекой.</summary>
public sealed record EntryPointLibraryMatch
{
    public required string LibraryPath { get; init; }

    public required string LibraryFileName { get; init; }

    public required OmfLibrary Library { get; init; }

    public required IReadOnlyList<LibraryMatchResult> Matches { get; init; }

    /// <summary>Совпадение символа <c>__astart</c> модуля crt0 на точке входа.</summary>
    public LibraryMatchResult? AstartMatch =>
        Matches.FirstOrDefault(static m =>
            m.SymbolName == "__astart"
            && m.ModuleName.Equals("crt0", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Сопоставление точки входа программы с crt0 во всех .LIB каталога.</summary>
public static class Crt0EntryPointMatcher
{
    /// <summary>
    /// Для каждого <c>*.LIB</c> в <paramref name="libraryDirectory"/> сопоставляет код
    /// по <paramref name="entryPointOffset"/> и возвращает библиотеки с совпадением crt0.
    /// </summary>
    public static IReadOnlyList<EntryPointLibraryMatch> MatchDirectory(
        byte[] image,
        RelocationTable imageRelocations,
        int entryPointOffset,
        string libraryDirectory) =>
        MatchDirectory(image, imageRelocations, entryPointOffset, libraryDirectory, RegisterState.InitExe);

    /// <summary>
    /// Для каждого <c>*.LIB</c> в <paramref name="libraryDirectory"/> сопоставляет код
    /// по <paramref name="entryPointOffset"/> и возвращает библиотеки с совпадением crt0.
    /// </summary>
    public static IReadOnlyList<EntryPointLibraryMatch> MatchDirectory(
        byte[] image,
        RelocationTable imageRelocations,
        int entryPointOffset,
        string libraryDirectory,
        RegisterState initRegisters)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);

        if (!Directory.Exists(libraryDirectory))
        {
            throw new DirectoryNotFoundException($"Каталог библиотек не найден: {libraryDirectory}");
        }

        var results = new List<EntryPointLibraryMatch>();

        foreach (var libraryPath in Directory.EnumerateFiles(libraryDirectory, "*.LIB").OrderBy(static p => p))
        {
            var library = OmfLibraryParser.ParseFile(libraryPath);
            var matches = LibraryFunctionMatcher.Match(
                image,
                imageRelocations,
                entryPointOffset,
                library,
                initRegisters);

            if (matches.Count == 0)
            {
                continue;
            }

            results.Add(new EntryPointLibraryMatch
            {
                LibraryPath = libraryPath,
                LibraryFileName = Path.GetFileName(libraryPath),
                Library = library,
                Matches = matches,
            });
        }

        return results;
    }
}
