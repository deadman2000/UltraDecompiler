using System.Text.RegularExpressions;
using UltraDecompiler.Decompilation;

namespace UltraDecompiler.Headers;

/// <summary>
/// Каталог сигнатур функций из заголовков (<c>INCLUDE/*.H</c>).
/// </summary>
public sealed class HeaderCatalog
{
    private readonly Dictionary<string, ProcedureSignature> _byName = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ProcedureSignature> All => _byName;

    /// <summary>Загружает все <c>*.H</c> из каталога.</summary>
    public static HeaderCatalog Load(string includeDirectory)
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

    public bool TryGetSignature(string cName, out ProcedureSignature? signature) =>
        _byName.TryGetValue(cName, out signature);

    private void ParseFile(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (!TryParseDeclaration(line, out var name, out var returnType, out var parameters, out var isVariadic))
            {
                continue;
            }

            _byName.TryAdd(name, new ProcedureSignature(returnType, parameters, isVariadic));
        }
    }

    private static bool TryParseDeclaration(
        string line,
        out string name,
        out CType returnType,
        out IReadOnlyList<ProcedureParameter> parameters,
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
        var paramList = trimmed[(parenIndex + 1)..^1].Trim();

        if (!TrySplitReturnAndName(head, out returnType, out name))
        {
            return false;
        }

        (parameters, isVariadic) = ParseParameterList(paramList);
        return true;
    }

    private static string StripComments(string line)
    {
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

        CType baseType = CTypeKind.Unknown switch
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

    private static (IReadOnlyList<ProcedureParameter> Parameters, bool IsVariadic) ParseParameterList(string paramList)
    {
        if (paramList.Equals("void", StringComparison.OrdinalIgnoreCase) || paramList.Length == 0)
        {
            return ([], false);
        }

        var isVariadic = paramList.Contains("...", StringComparison.Ordinal);
        var parts = SplitParameters(paramList);
        var result = new List<ProcedureParameter>();
        var stackOffset = 4;

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
            result.Add(new ProcedureParameter(type, new StackParameter(stackOffset)));
            stackOffset += 2;
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
