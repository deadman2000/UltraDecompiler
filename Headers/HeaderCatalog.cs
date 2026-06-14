using System.Collections.Concurrent;
using System.Text.RegularExpressions;
namespace UltraDecompiler.Headers;

/// <summary>
/// Каталог сигнатур функций из заголовков (<c>INCLUDE/*.H</c>).
/// </summary>
public sealed class HeaderCatalog
{
    private static readonly ConcurrentDictionary<string, (long Signature, HeaderCatalog Catalog)> LoadCache = new();

    private readonly Dictionary<string, HeaderFunction> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _headerFileByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StructDefinition> _structsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<int, List<StructDefinition>> _structsBySize = new();

    public IReadOnlyDictionary<string, HeaderFunction> All => _byName;

    public IReadOnlyDictionary<string, StructDefinition> Structs => _structsByName;

    /// <summary>Пустой каталог для диагностического пайплайна без заголовков.</summary>
    public static HeaderCatalog Empty { get; } = new();

    private HeaderCatalog()
    {
    }

    /// <summary>Загружает все <c>*.H</c> из каталога.</summary>
    public static HeaderCatalog Load(string includeDirectory)
    {
        var fullPath = Path.GetFullPath(includeDirectory);
        var signature = ComputeDirectorySignature(fullPath);

        if (LoadCache.TryGetValue(fullPath, out var cached) && cached.Signature == signature)
        {
            return cached.Catalog;
        }

        var catalog = LoadUncached(fullPath);
        LoadCache[fullPath] = (signature, catalog);
        return catalog;
    }

    private static HeaderCatalog LoadUncached(string includeDirectory)
    {
        var catalog = new HeaderCatalog();
        if (!Directory.Exists(includeDirectory))
        {
            return catalog;
        }

        foreach (var path in Directory.EnumerateFiles(includeDirectory, "*.H", SearchOption.AllDirectories))
        {
            catalog.ParseFile(path);
        }

        return catalog;
    }

    /// <summary>Сигнатура каталога по времени изменения всех <c>*.H</c> (для инвалидации кэша).</summary>
    private static long ComputeDirectorySignature(string includeDirectory)
    {
        if (!Directory.Exists(includeDirectory))
        {
            return 0;
        }

        long signature = 0;
        foreach (var path in Directory.EnumerateFiles(includeDirectory, "*.H", SearchOption.AllDirectories))
        {
            signature ^= File.GetLastWriteTimeUtc(path).Ticks;
        }

        return signature;
    }

    public bool TryGetFunction(string cName, out HeaderFunction? function) =>
        _byName.TryGetValue(cName, out function);

    /// <summary>Возвращает имя файла заголовка QuickC INCLUDE, где объявлена функция.</summary>
    public bool TryGetHeaderFile(string cName, out string? headerFile) =>
        _headerFileByName.TryGetValue(cName, out headerFile);

    public bool TryGetStruct(string structName, out StructDefinition? definition) =>
        _structsByName.TryGetValue(structName, out definition);

    public bool TryGetStructHeader(string structName, out string? headerFile)
    {
        if (_structsByName.TryGetValue(structName, out var definition))
        {
            headerFile = definition.HeaderFile;
            return true;
        }

        headerFile = null;
        return false;
    }

    /// <summary>Возвращает структуры с заданным размером (из INCLUDE).</summary>
    public IReadOnlyList<StructDefinition> GetStructsBySize(int size) =>
        _structsBySize.TryGetValue(size, out var list) ? list : [];

    private void ParseFile(string path)
    {
        var headerFileName = Path.GetFileName(path);
        var lines = File.ReadAllLines(path);

        ParseStructs(lines, headerFileName);
        ParseUnions(lines, headerFileName);

        foreach (var line in lines)
        {
            if (!TryParseDeclaration(line, out var name, out var returnType, out var parameters, out var isVariadic))
            {
                continue;
            }

            _byName.TryAdd(name, new HeaderFunction(returnType, parameters, isVariadic));
            _headerFileByName.TryAdd(name, headerFileName);
        }
    }

    private void ParseStructs(string[] lines, string headerFileName)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = StripComments(lines[i]).Trim();
            if (!trimmed.StartsWith("struct ", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryParseStructBlock(lines, ref i, headerFileName, out var definition) || definition is null)
            {
                continue;
            }

            RegisterTypeDefinition(definition);
        }
    }

    private void ParseUnions(string[] lines, string headerFileName)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = StripComments(lines[i]).Trim();
            if (!trimmed.StartsWith("union ", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryParseUnionBlock(lines, ref i, headerFileName, out var definition) || definition is null)
            {
                continue;
            }

            RegisterTypeDefinition(definition);
        }
    }

    private void RegisterTypeDefinition(StructDefinition definition)
    {
        if (_structsByName.ContainsKey(definition.Name))
        {
            return;
        }

        _structsByName[definition.Name] = definition;
        if (!_structsBySize.TryGetValue(definition.Size, out var bySize))
        {
            bySize = [];
            _structsBySize[definition.Size] = bySize;
        }

        bySize.Add(definition);
    }

    private bool TryParseUnionBlock(
        string[] lines,
        ref int index,
        string headerFileName,
        out StructDefinition? definition)
    {
        definition = null;
        var head = StripComments(lines[index]).Trim();
        var braceIndex = head.IndexOf('{');
        if (braceIndex < 0)
        {
            return false;
        }

        var namePart = head[..braceIndex].Trim();
        var nameTokens = Tokenize(namePart).Where(static t => t != "union").ToList();
        if (nameTokens.Count != 1 || !Regex.IsMatch(nameTokens[0], @"^[A-Za-z_]\w*$"))
        {
            return false;
        }

        var unionName = nameTokens[0];
        var fields = new List<StructField>();
        var maxMemberSize = 0;
        var body = head[(braceIndex + 1)..].Trim();

        if (!TryParseUnionMembersFromLine(body, fields, ref maxMemberSize))
        {
            while (++index < lines.Length)
            {
                var line = StripComments(lines[index]).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (TryParseUnionMembersFromLine(line, fields, ref maxMemberSize))
                {
                    break;
                }
            }
        }

        if (fields.Count == 0 || maxMemberSize <= 0)
        {
            return false;
        }

        definition = new StructDefinition(unionName, headerFileName, fields, isUnion: true, sizeOverride: maxMemberSize);
        return true;
    }

    private bool TryParseUnionMembersFromLine(string line, List<StructField> fields, ref int maxMemberSize)
    {
        var trimmed = line.Trim();
        if (trimmed is "};" or "}")
        {
            return true;
        }

        if (trimmed.EndsWith("};", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^2].TrimEnd();
        }

        if (trimmed.EndsWith(';'))
        {
            trimmed = trimmed[..^1].Trim();
        }

        if (trimmed.Length == 0)
        {
            return trimmed.EndsWith('}');
        }

        var tokens = Tokenize(trimmed).Where(static t => !IsIgnoredToken(t)).ToList();
        if (tokens.Count < 3)
        {
            return false;
        }

        var memberName = tokens[^1];
        if (!Regex.IsMatch(memberName, @"^[A-Za-z_]\w*$"))
        {
            return false;
        }

        var typeKeywordIndex = tokens.IndexOf("struct");
        if (typeKeywordIndex < 0 || typeKeywordIndex >= tokens.Count - 2)
        {
            return false;
        }

        var nestedTypeName = tokens[typeKeywordIndex + 1];
        if (!_structsByName.TryGetValue(nestedTypeName, out var nestedDefinition) || nestedDefinition is null)
        {
            return false;
        }

        foreach (var nestedField in nestedDefinition.Fields)
        {
            if (fields.Any(existing => existing.Offset == nestedField.Offset))
            {
                continue;
            }

            fields.Add(new StructField(
                $"{memberName}.{nestedField.Name}",
                nestedField.Type,
                nestedField.Offset,
                nestedField.Size));
        }

        maxMemberSize = Math.Max(maxMemberSize, nestedDefinition.Size);
        return false;
    }

    private static bool TryParseStructBlock(
        string[] lines,
        ref int index,
        string headerFileName,
        out StructDefinition? definition)
    {
        definition = null;
        var head = StripComments(lines[index]).Trim();
        var braceIndex = head.IndexOf('{');
        if (braceIndex < 0)
        {
            return false;
        }

        var namePart = head[..braceIndex].Trim();
        var nameTokens = Tokenize(namePart).Where(static t => t != "struct").ToList();
        if (nameTokens.Count != 1 || !Regex.IsMatch(nameTokens[0], @"^[A-Za-z_]\w*$"))
        {
            return false;
        }

        var structName = nameTokens[0];
        var fields = new List<StructField>();
        var currentOffset = 0;
        var body = head[(braceIndex + 1)..].Trim();

        if (!TryParseStructFieldsFromLine(body, ref currentOffset, fields))
        {
            while (++index < lines.Length)
            {
                var line = StripComments(lines[index]).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (TryParseStructFieldsFromLine(line, ref currentOffset, fields))
                {
                    break;
                }
            }
        }

        if (fields.Count == 0)
        {
            return false;
        }

        definition = new StructDefinition(structName, headerFileName, fields);
        return true;
    }

    private static bool TryParseStructFieldsFromLine(string line, ref int currentOffset, List<StructField> fields)
    {
        var trimmed = line.Trim();
        if (trimmed is "};" or "}")
        {
            return true;
        }

        if (trimmed.EndsWith("};", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^2].TrimEnd();
        }

        if (trimmed.EndsWith(';'))
        {
            trimmed = trimmed[..^1].Trim();
        }

        if (trimmed.Length == 0)
        {
            return trimmed.EndsWith('}');
        }

        var tokens = Tokenize(trimmed).Where(static t => !IsIgnoredToken(t)).ToList();
        if (tokens.Count < 2)
        {
            return false;
        }

        var fieldName = tokens[^1];
        if (!Regex.IsMatch(fieldName, @"^[A-Za-z_]\w*$"))
        {
            return false;
        }

        var typeTokens = tokens.Take(tokens.Count - 1).ToList();
        var fieldType = ParseTypeTokens(typeTokens);
        var field = StructDefinition.CreateField(fieldName, fieldType, currentOffset);
        fields.Add(field);
        currentOffset = field.Offset + field.Size;
        return false;
    }

    private static bool TryParseDeclaration(
        string line,
        out string name,
        out CType returnType,
        out IReadOnlyList<HeaderFunctionParameter> parameters,
        out bool isVariadic)
    {
        name = string.Empty;
        returnType = CType.Int;
        parameters = [];
        isVariadic = false;

        var trimmed = StripComments(line).Trim();
        if (trimmed.Length == 0 ||
            trimmed.StartsWith('#') ||
            trimmed.StartsWith("typedef", StringComparison.Ordinal) ||
            trimmed.StartsWith("struct", StringComparison.Ordinal) ||
            trimmed.StartsWith("union", StringComparison.Ordinal) ||
            trimmed.StartsWith("enum", StringComparison.Ordinal) ||
            trimmed.StartsWith("extern", StringComparison.Ordinal) ||
            !trimmed.EndsWith(';'))
        {
            return false;
        }

        var parenIndex = trimmed.IndexOf('(');
        if (parenIndex <= 0)
        {
            return false;
        }

        var head = trimmed[..parenIndex].Trim();
        // Закрывающая «)» перед «;» — не часть списка параметров (иначе «(void)» превращается в «void)»).
        var closeParen = trimmed.LastIndexOf(')');
        if (closeParen <= parenIndex)
        {
            return false;
        }

        var paramList = trimmed[(parenIndex + 1)..closeParen].Trim();

        if (!TrySplitReturnAndName(head, out returnType, out name))
        {
            return false;
        }

        (parameters, isVariadic) = ParseParameterList(paramList);
        return true;
    }

    private static string StripComments(string line)
    {
        var block = line.IndexOf("/*", StringComparison.Ordinal);
        if (block >= 0)
        {
            line = line[..block];
        }

        var slash = line.IndexOf("//", StringComparison.Ordinal);
        return slash >= 0 ? line[..slash] : line;
    }

    private static bool TrySplitReturnAndName(string head, out CType returnType, out string name)
    {
        returnType = CType.Int;
        name = string.Empty;

        var tokens = Tokenize(head);
        if (tokens.Count == 0)
        {
            return false;
        }

        var cleaned = tokens
            .Where(static t => !IsIgnoredToken(t))
            .ToList();

        if (cleaned.Count < 2)
        {
            return false;
        }

        name = cleaned[^1];
        if (!Regex.IsMatch(name, @"^[A-Za-z_]\w*$"))
        {
            return false;
        }

        var returnTokens = cleaned.Take(cleaned.Count - 1).ToList();
        returnType = ParseTypeTokens(returnTokens);
        return true;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        foreach (Match match in Regex.Matches(text, @"\*+|[A-Za-z_]\w*"))
        {
            tokens.Add(match.Value);
        }

        return tokens;
    }

    private static bool IsIgnoredToken(string token) =>
        token.Equals("_CDECL", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("_NEAR", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("_FAR", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("cdecl", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("near", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("far", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("pascal", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("const", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("volatile", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("signed", StringComparison.OrdinalIgnoreCase);

    private static CType ParseTypeTokens(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return CType.Int;
        }

        var pointerDepth = tokens.Count(static t => t == "*");
        var typeTokens = tokens.Where(static t => t != "*").ToList();

        CType baseType;
        var structIndex = typeTokens.IndexOf("struct");
        var unionIndex = typeTokens.IndexOf("union");
        if (structIndex >= 0 && structIndex < typeTokens.Count - 1)
        {
            baseType = CType.StructType(typeTokens[structIndex + 1]);
        }
        else if (unionIndex >= 0 && unionIndex < typeTokens.Count - 1)
        {
            baseType = CType.UnionType(typeTokens[unionIndex + 1]);
        }
        else
        {
            baseType = CTypeKind.Unknown switch
            {
                _ when typeTokens.Contains("void") => CType.Void,
                _ when typeTokens.Contains("char") => CType.Char,
                _ when typeTokens.Contains("int") => CType.Int,
                _ when typeTokens.Contains("long") => new CType(CTypeKind.Long),
                _ when typeTokens.Contains("float") => new CType(CTypeKind.Float),
                _ when typeTokens.Contains("double") => new CType(CTypeKind.Double),
                _ when typeTokens.Contains("size_t") => new CType(CTypeKind.SizeT),
                _ when typeTokens.Contains("unsigned") => new CType(CTypeKind.Unsigned),
                _ when typeTokens.Contains("FILE") => new CType(CTypeKind.Pointer, CType.Int),
                _ => CType.Int,
            };
        }

        if (baseType.Kind == CTypeKind.Char && pointerDepth == 1)
        {
            baseType = CType.CharPtr;
            pointerDepth = 0;
        }

        for (var i = 0; i < pointerDepth; i++)
        {
            baseType = new CType(CTypeKind.Pointer, baseType);
        }

        return baseType;
    }

    private static (IReadOnlyList<HeaderFunctionParameter> Parameters, bool IsVariadic) ParseParameterList(string paramList)
    {
        if (paramList.Equals("void", StringComparison.OrdinalIgnoreCase) || paramList.Length == 0)
        {
            return ([], false);
        }

        var isVariadic = paramList.Contains("...", StringComparison.Ordinal);
        var parts = SplitParameters(paramList);
        var result = new List<HeaderFunctionParameter>();

        foreach (var part in parts)
        {
            if (part == "...")
            {
                isVariadic = true;
                break;
            }

            var tokens = Tokenize(part).Where(static t => !IsIgnoredToken(t)).ToList();
            if (tokens.Count == 0)
            {
                continue;
            }

            var type = ParseTypeTokens(tokens);
            result.Add(new HeaderFunctionParameter(type));
        }

        return (result, isVariadic);
    }

    private static List<string> SplitParameters(string paramList)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < paramList.Length; i++)
        {
            switch (paramList[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parts.Add(paramList[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        parts.Add(paramList[start..].Trim());
        return parts;
    }
}
