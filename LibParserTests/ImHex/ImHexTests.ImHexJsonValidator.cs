using System.Text.Json;
using LibParser.Models;
using LibParser.Omf;

namespace LibParserTests;

public sealed partial class ImHexTests
{
    /// <summary>Сверяет JSON ImHex с разбором <see cref="OmfLibraryParser"/>.</summary>
    private static class ImHexJsonValidator
    {
        private sealed record JsonDictionarySymbol(string Name, ushort ModulePage);

        private sealed record JsonModule(string HeaderRecordType, string HeaderName);

        public static void Validate(JsonElement root, OmfLibrary expected, string displayName)
        {
            Assert.Equal(expected.Header.PageSize - 3, GetInt32(root, "omfHeaderRecordLength", displayName));
            Assert.Equal(expected.Header.DictionaryOffset, GetInt32(root, "omfDictionaryOffset", displayName));
            Assert.Equal(expected.Header.DictionaryBlockCount, GetUInt16(root, "omfDictionaryBlockCount", displayName));
            Assert.Equal(expected.Header.Flags, GetByte(root, "omfLibraryFlags", displayName));

            Assert.True(root.TryGetProperty("library", out var library), $"JSON {displayName}: нет поля library.");
            ValidateHeader(library.GetProperty("header"), expected.Header, displayName);
            ValidateModules(library, expected, displayName);
            ValidateSymbols(library, expected, displayName);
        }

        private static void ValidateHeader(JsonElement header, OmfLibraryHeader expected, string displayName)
        {
            var type = GetString(header, "type", displayName);
            Assert.Contains("LibraryHeader", type, StringComparison.Ordinal);

            Assert.Equal(expected.PageSize - 3, GetInt32(header, "recordLength", displayName));
            Assert.Equal(expected.DictionaryOffset, GetInt32(header, "dictionaryOffset", displayName));
            Assert.Equal(expected.DictionaryBlockCount, GetUInt16(header, "dictionaryBlockCount", displayName));
            Assert.Equal(expected.Flags, GetByte(header, "flags", displayName));
        }

        private static void ValidateModules(JsonElement library, OmfLibrary expected, string displayName)
        {
            Assert.True(
                library.TryGetProperty("objectModules", out var modules),
                $"JSON {displayName}: нет поля objectModules.");

            var jsonModules = CollectModules(modules, displayName)
                .Where(static module => IsModuleHeaderRecord(module.HeaderRecordType))
                .ToArray();

            var expectedModules = expected.Modules
                .OrderBy(static module => module.FileOffset)
                .ToArray();

            Assert.Equal(expectedModules.Length, jsonModules.Length);

            for (var index = 0; index < expectedModules.Length; index++)
            {
                var expectedModule = expectedModules[index];
                var jsonModule = jsonModules[index];

                Assert.Equal(expectedModule.HeaderName, jsonModule.HeaderName);
            }
        }

        private static void ValidateSymbols(JsonElement library, OmfLibrary expected, string displayName)
        {
            Assert.True(
                library.TryGetProperty("dictionaryBlocks", out var blocks),
                $"JSON {displayName}: нет поля dictionaryBlocks.");

            Assert.Equal(expected.Header.DictionaryBlockCount, blocks.GetArrayLength());

            var jsonSymbols = CollectDictionarySymbols(blocks);
            Assert.Equal(expected.Symbols.Count, jsonSymbols.Count);

            foreach (var expectedSymbol in expected.Symbols.Values.OrderBy(static symbol => symbol.Name, StringComparer.Ordinal))
            {
                Assert.True(
                    jsonSymbols.TryGetValue(expectedSymbol.Name, out var jsonSymbol),
                    $"JSON {displayName}: нет символа словаря '{expectedSymbol.Name}'.");

                Assert.Equal(expectedSymbol.Name, jsonSymbol.Name);
                Assert.Equal(expectedSymbol.ModulePage, jsonSymbol.ModulePage);
            }

            foreach (var jsonSymbol in jsonSymbols.Values.OrderBy(static symbol => symbol.Name, StringComparer.Ordinal))
            {
                Assert.True(
                    expected.Symbols.ContainsKey(jsonSymbol.Name),
                    $"JSON {displayName}: лишний символ словаря '{jsonSymbol.Name}'.");
            }

            ValidateModulePages(jsonSymbols.Values, expected, displayName);

            foreach (var block in blocks.EnumerateArray())
            {
                Assert.True(block.TryGetProperty("htab", out var htab), $"JSON {displayName}: блок словаря без htab.");
                Assert.Equal(37, htab.GetArrayLength());
                Assert.True(block.TryGetProperty("fflag", out _), $"JSON {displayName}: блок словаря без fflag.");
            }
        }

        private static void ValidateModulePages(
            IEnumerable<JsonDictionarySymbol> jsonSymbols,
            OmfLibrary expected,
            string displayName)
        {
            var parserModulePages = expected.Modules
                .Select(static module => module.PageNumber)
                .ToHashSet();

            var jsonModulePages = jsonSymbols
                .Select(static symbol => symbol.ModulePage)
                .ToHashSet();

            foreach (var page in jsonModulePages)
            {
                Assert.Contains(page, parserModulePages);
            }

            var modulesWithoutDictionarySymbols = parserModulePages.Except(jsonModulePages).ToArray();
            Assert.Equal(
                expected.Modules.Count,
                jsonModulePages.Count + modulesWithoutDictionarySymbols.Length);
        }

        private static List<JsonModule> CollectModules(JsonElement objectModules, string displayName)
        {
            var modules = new List<JsonModule>();
            foreach (var module in objectModules.EnumerateArray())
            {
                if (!TryReadModule(module, out var jsonModule))
                {
                    Assert.Fail($"JSON {displayName}: некорректный элемент objectModules.");
                }

                modules.Add(jsonModule);
            }

            return modules;
        }

        private static bool TryReadModule(JsonElement module, out JsonModule jsonModule)
        {
            jsonModule = default!;

            if (!module.TryGetProperty("headerRecordType", out var typeElement)
                || !module.TryGetProperty("headerName", out var headerNameElement))
            {
                return false;
            }

            var headerRecordType = DecodeImHexJsonString(typeElement.GetString() ?? string.Empty);
            var parsedName = headerNameElement.GetProperty("name").GetString() ?? string.Empty;
            jsonModule = new JsonModule(headerRecordType, DecodeImHexJsonString(parsedName));
            return true;
        }

        private static bool IsModuleHeaderRecord(string headerRecordType) =>
            headerRecordType.Contains("Theadr", StringComparison.Ordinal)
            || headerRecordType.Contains("Lheadr", StringComparison.Ordinal);

        private static Dictionary<string, JsonDictionarySymbol> CollectDictionarySymbols(JsonElement dictionaryBlocks)
        {
            var symbols = new Dictionary<string, JsonDictionarySymbol>(StringComparer.Ordinal);
            foreach (var block in dictionaryBlocks.EnumerateArray())
            {
                CollectDictionarySymbols(block, symbols);
            }

            return symbols;
        }

        private static void CollectDictionarySymbols(JsonElement element, Dictionary<string, JsonDictionarySymbol> symbols)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (TryReadDictionarySymbol(element, out var symbol))
                    {
                        symbols[symbol.Name] = symbol;
                    }

                    foreach (var property in element.EnumerateObject())
                    {
                        CollectDictionarySymbols(property.Value, symbols);
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        CollectDictionarySymbols(item, symbols);
                    }

                    break;
            }
        }

        private static bool TryReadDictionarySymbol(JsonElement element, out JsonDictionarySymbol symbol)
        {
            symbol = default!;

            if (!element.TryGetProperty("name", out var nameElement)
                || !element.TryGetProperty("modulePage", out var pageElement)
                || !element.TryGetProperty("nameLen", out _))
            {
                return false;
            }

            var parsedName = nameElement.GetString();
            if (string.IsNullOrEmpty(parsedName))
            {
                return false;
            }

            var name = DecodeImHexJsonString(parsedName);
            symbol = new JsonDictionarySymbol(name, (ushort)pageElement.GetInt32());
            return true;
        }

        private static string DecodeImHexJsonString(string value) =>
            Uri.UnescapeDataString(value);

        private static int GetInt32(JsonElement element, string propertyName, string displayName)
        {
            Assert.True(
                element.TryGetProperty(propertyName, out var value),
                $"JSON {displayName}: нет поля {propertyName}.");
            return value.GetInt32();
        }

        private static ushort GetUInt16(JsonElement element, string propertyName, string displayName)
        {
            Assert.True(
                element.TryGetProperty(propertyName, out var value),
                $"JSON {displayName}: нет поля {propertyName}.");
            return (ushort)value.GetInt32();
        }

        private static byte GetByte(JsonElement element, string propertyName, string displayName)
        {
            Assert.True(
                element.TryGetProperty(propertyName, out var value),
                $"JSON {displayName}: нет поля {propertyName}.");
            return (byte)value.GetInt32();
        }

        private static string GetString(JsonElement element, string propertyName, string displayName)
        {
            Assert.True(
                element.TryGetProperty(propertyName, out var value),
                $"JSON {displayName}: нет поля {propertyName}.");
            return value.GetString()
                ?? throw new InvalidOperationException($"JSON {displayName}: поле {propertyName} не строка.");
        }
    }
}
