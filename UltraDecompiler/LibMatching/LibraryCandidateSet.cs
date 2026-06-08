using LibParser.Models;

namespace UltraDecompiler.LibMatching;

/// <summary>
/// Набор OMF-библиотек-кандидатов: сужается по мере сопоставления символов с образом EXE.
/// </summary>
public sealed class LibraryCandidateSet
{
    private readonly List<OmfLibrary> _candidates;
    private readonly List<OmfLibrary> _linked = [];

    public LibraryCandidateSet(IEnumerable<OmfLibrary> initialLibraries)
    {
        ArgumentNullException.ThrowIfNull(initialLibraries);
        _candidates = initialLibraries.ToList();
    }

    /// <summary>Библиотеки, ещё допустимые для сопоставления.</summary>
    public IReadOnlyList<OmfLibrary> Candidates => _candidates;

    /// <summary>Библиотеки, из которых уже сопоставлен хотя бы один символ.</summary>
    public IReadOnlyList<OmfLibrary> Linked => _linked;

    /// <summary>Имена подключаемых .LIB (порядок первого подтверждения).</summary>
    public IReadOnlyList<string> LinkedFileNames =>
        _linked.Select(static l => l.FileName).ToList();

    /// <summary>Отмечает библиотеку как использованную, не сужая кандидатов.</summary>
    public void ConfirmLibrary(OmfLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);

        if (!_linked.Contains(library))
        {
            _linked.Add(library);
        }
    }

    /// <summary>
    /// Подтверждает библиотеку как провайдера crt0/__astart.
    /// В отличие от NarrowBySymbol — НЕ исключает другие библиотеки, тоже содержащие __astart.
    /// Такие библиотеки считаются взаимозаменяемыми базами; варианты будут сформированы на верхнем уровне.
    /// </summary>
    public void ConfirmAstartProvider(OmfLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        ConfirmLibrary(library);
        // Специально не вызываем удаление других с __astart — сохраняем альтернативы.
    }

    /// <summary>
    /// Исключает из кандидатов все .LIB, кроме <paramref name="matchedLibrary"/>,
    /// в словаре которых есть <paramref name="symbolName"/>.
    /// </summary>
    public void NarrowBySymbol(OmfLibrary matchedLibrary, string symbolName)
    {
        ArgumentNullException.ThrowIfNull(matchedLibrary);
        ArgumentException.ThrowIfNullOrEmpty(symbolName);

        ConfirmLibrary(matchedLibrary);

        for (var i = _candidates.Count - 1; i >= 0; i--)
        {
            var library = _candidates[i];
            if (ReferenceEquals(library, matchedLibrary))
            {
                continue;
            }

            if (library.Symbols.ContainsKey(symbolName))
            {
                _candidates.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Сужает кандидатов до библиотек, содержащих отличный от совпавшего модуль.
    /// </summary>
    public void NarrowByEntryPointMatches(string moduleName, IReadOnlyList<EntryPointLibraryMatchInfo> entryMatches)
    {
        ArgumentNullException.ThrowIfNull(entryMatches);

        if (entryMatches.Count == 0)
        {
            _candidates.Clear();
            return;
        }

        var allowed = entryMatches.Select(static m => m.Library).ToHashSet();
        for (var i = _candidates.Count - 1; i >= 0; i--)
        {
            if (_candidates[i].Modules.Any(m => m.LibraryModuleName == moduleName) && !allowed.Contains(_candidates[i]))
            {
                _candidates.RemoveAt(i);
            }
        }
    }
}
