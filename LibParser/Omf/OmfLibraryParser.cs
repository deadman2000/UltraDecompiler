namespace LibParser.Omf;

using System.Collections.Concurrent;
using LibParser.Models;

/// <summary>Разбор файлов .LIB формата OMF (Microsoft QuickC / LINK).</summary>
public static class OmfLibraryParser
{
    private static readonly ConcurrentDictionary<string, (long WriteTimeUtcTicks, OmfLibrary Library)> ParseFileCache = new();

    /// <summary>Загрузить и разобрать библиотеку из файла.</summary>
    public static OmfLibrary ParseFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var writeTimeUtcTicks = File.GetLastWriteTimeUtc(fullPath).Ticks;

        if (ParseFileCache.TryGetValue(fullPath, out var cached) && cached.WriteTimeUtcTicks == writeTimeUtcTicks)
        {
            return cached.Library;
        }

        var data = File.ReadAllBytes(fullPath);
        var library = Parse(data, Path.GetFileName(fullPath));
        ParseFileCache[fullPath] = (writeTimeUtcTicks, library);
        return library;
    }

    /// <summary>Разобрать библиотеку из памяти.</summary>
    /// <param name="fileName">Имя файла для идентификации (если не задано — пустая строка).</param>
    public static OmfLibrary Parse(ReadOnlySpan<byte> data, string? fileName = null)
    {
        if (data.Length < 16 || data[0] != OmfRecordTypes.LibraryHeader)
        {
            throw new InvalidDataException("Файл не является OMF-библиотекой (ожидается запись F0h).");
        }

        var header = ParseHeader(data);
        var modules = ParseModules(data, header);
        var symbols = OmfDictionaryParser.Parse(
            data,
            header.DictionaryOffset,
            header.DictionaryBlockCount,
            header.CaseSensitive);

        return new OmfLibrary
        {
            FileName = fileName ?? string.Empty,
            Header = header,
            Modules = modules,
            Symbols = symbols,
            RawData = data.ToArray(),
        };
    }

    private static OmfLibraryHeader ParseHeader(ReadOnlySpan<byte> data)
    {
        var recordLength = OmfBinaryReader.ReadUInt16At(data, 1);
        var pageSize = recordLength + 3;
        var dictionaryOffset = (int)OmfBinaryReader.ReadUInt32At(data, 3);
        var dictionaryBlocks = OmfBinaryReader.ReadUInt16At(data, 7);
        var flags = data[9];

        return new OmfLibraryHeader
        {
            PageSize = pageSize,
            DictionaryOffset = dictionaryOffset,
            DictionaryBlockCount = dictionaryBlocks,
            Flags = flags,
        };
    }

    private static List<OmfModule> ParseModules(ReadOnlySpan<byte> data, OmfLibraryHeader header)
    {
        var modules = new List<OmfModule>();
        var pageSize = header.PageSize;
        var offset = pageSize;

        while (offset < header.DictionaryOffset)
        {
            if (offset >= data.Length)
            {
                break;
            }

            var recordType = data[offset];
            if (recordType is not (OmfRecordTypes.Theadr or OmfRecordTypes.Lheadr))
            {
                break;
            }

            var moduleEnd = FindModuleEnd(data, offset, header.DictionaryOffset);
            var paddedEnd = AlignUp(moduleEnd, pageSize);
            if (paddedEnd > header.DictionaryOffset)
            {
                paddedEnd = header.DictionaryOffset;
            }

            // Номер страницы в словаре = смещение модуля / размер страницы (страница 0 — заголовок F0).
            var pageNumber = (ushort)(offset / pageSize);
            var moduleBytes = data.Slice(offset, paddedEnd - offset);
            modules.Add(OmfModuleParser.Parse(moduleBytes, pageNumber, offset));

            offset = paddedEnd;
        }

        return modules;
    }

    private static int FindModuleEnd(ReadOnlySpan<byte> data, int start, int limit)
    {
        var pos = start;
        while (pos < limit)
        {
            if (pos + 3 > data.Length)
            {
                return pos;
            }

            var recordType = data[pos];
            var recordLength = OmfBinaryReader.ReadUInt16At(data, pos + 1);
            var next = pos + 3 + recordLength;
            if (next > data.Length)
            {
                return data.Length;
            }

            if (recordType is OmfRecordTypes.Modend or OmfRecordTypes.Modend32)
            {
                return next;
            }

            pos = next;
        }

        return pos;
    }

    private static int AlignUp(int value, int alignment) =>
        (value + alignment - 1) / alignment * alignment;
}
