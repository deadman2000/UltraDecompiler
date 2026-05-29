namespace LibParserTests;

using LibParser.Models;
using LibParser.Omf;

public sealed class OmfFixuppParserTests
{
    [Fact]
    public void Parse_PrintfModule_HasFixupsWithExternalTargets()
    {
        if (!QuickCLibAssets.Exists("CLIBC.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("CLIBC.LIB"));
        var module = lib.FindModuleBySymbol("_printf");
        Assert.NotNull(module);

        Assert.NotEmpty(module.ExternalSymbols);
        Assert.Contains(module.ExternalSymbols, static s => s.Name == "__output");
        Assert.NotEmpty(module.Fixups);

        var extFixups = module.Fixups
            .Where(static f => f.Target.Kind == OmfFixupDatumKind.Extdef)
            .ToList();
        Assert.NotEmpty(extFixups);

        var outputFixup = extFixups.FirstOrDefault(static f => f.Target.Name == "__output");
        Assert.NotNull(outputFixup);
        Assert.Equal(OmfFixupLocationType.Offset16, outputFixup.LocationType);
        Assert.True(outputFixup.SegmentOffset >= 0);
    }

    [Fact]
    public void Parse_PrintfModule_MultipleExternalFixups()
    {
        if (!QuickCLibAssets.Exists("CLIBC.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("CLIBC.LIB"));
        var module = lib.FindModuleBySymbol("_printf");
        Assert.NotNull(module);

        var extFixups = module.Fixups
            .Where(static f => f.Target.Kind == OmfFixupDatumKind.Extdef && f.Target.Name is not null)
            .ToList();
        Assert.True(extFixups.Count >= 4);
        Assert.Contains(extFixups, static f => f.Target.Name == "__output");
        Assert.All(extFixups, static f =>
            Assert.Equal(OmfFixupDatumKind.TargetFrame, f.Frame.Kind));
    }

    [Fact]
    public void Parse_87Lib_FpmathModule_HasFixups()
    {
        if (!QuickCLibAssets.Exists("87.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("87.LIB"));
        var module = lib.FindModuleBySymbol("__fpmath");
        Assert.NotNull(module);
        Assert.NotEmpty(module.Fixups);
    }

    [Fact]
    public void Parse_AllQuickCLibraries_ModulesHaveFixupsOrNoCode()
    {
        if (!QuickCLibAssets.Exists("CLIBC.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("CLIBC.LIB"));
        var withCode = lib.Modules.Where(static m => m.CodeSegments.Any(static s => s.Data.Length > 0)).ToList();
        Assert.NotEmpty(withCode);
        Assert.True(withCode.Count(static m => m.Fixups.Count > 0) > 100);
    }
}
