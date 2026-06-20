using System.Text.Json;
using LibParser.Omf;

namespace LibParserTests.ImHex;

/// <summary>
/// Прогон эталонных QuickC .LIB через ImHex CLI (<c>imhex --pl format</c>) и шаблон <c>omf_lib.hexpat</c>.
/// </summary>
public sealed partial class ImHexTests
{
    public static IEnumerable<object[]> QuickCLibraryMemberData()
    {
        foreach (var fileName in QuickCLibAssets.EnumerateLibFileNames())
        {
            yield return [fileName];
        }
    }

    [Theory(Skip = "ImHex")]
    [MemberData(nameof(QuickCLibraryMemberData))]
    public void OmfLibHexpat_ValidatesQuickCLibrary(string libFileName)
    {
        if (!ImHexTestAssets.IsImHexAvailable || !ImHexTestAssets.IsPatternAvailable || !QuickCLibAssets.Exists(libFileName))
            return;

        var libPath = QuickCLibAssets.PathOf(libFileName);
        var result = ImHexPatternRunner.Run(
            ImHexTestAssets.ImHexExecutable,
            libPath,
            ImHexTestAssets.OmfLibPatternPath);

        Assert.True(
            result.Success,
            $"ImHex: ошибка паттерна для {libFileName}.{Environment.NewLine}{result.Details}");

        Assert.False(string.IsNullOrWhiteSpace(result.Json));
        using var document = JsonDocument.Parse(result.Json!);
        var expected = OmfLibraryParser.ParseFile(libPath);
        ImHexJsonValidator.Validate(document.RootElement, expected, libFileName);
    }
}
