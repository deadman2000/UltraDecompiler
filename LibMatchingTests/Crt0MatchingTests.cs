using Common;
using LibParser.Omf;
using TestSupport;
using UltraDecompiler.Compilation;
using UltraDecompiler.LibMatching;

namespace LibMatchingTests;

/// <summary>Сопоставление кода CRT0 (C runtime startup) с hello.exe.</summary>
public class Crt0MatchingTests
{
    [Theory]
    [InlineData("SLIBCE.LIB")]
    [InlineData("CLIBC.LIB")]
    [InlineData("MLIBC.LIB")]
    [InlineData("LLIBC.LIB")]
    public void Match_Crt0ModuleAgainstItself_FindsRuntimeSymbols(string libFileName)
    {
        // Проверяет, что тело соответствует самому себе
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(libFileName));
        var crt0 = Crt0TestHelpers.GetCrt0Module(library);
        var code = crt0.CodeSegments.First(static s => s.IsCode);

        var matches = LibraryFunctionMatcher.Match(code.Data, RelocationTable.Empty, 0, library);

        Assert.Contains(matches, static m => m.SymbolName == "__astart");
        Assert.All(matches, m => Assert.Equal(crt0.PageNumber, m.ModulePage));
    }

    [Theory]
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void Match_HelloEntryPoint_WithCrt0(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(modelCase.ExePath);
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));
        var crt0Page = Crt0TestHelpers.GetCrt0ModulePage(library);

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            (int)parser.EntryPointOffset,
            library,
            RegisterState.InitExe);

        Assert.Contains(matches, static m => m.SymbolName == "__astart");

        var match = matches.First(static m => m.SymbolName == "__astart");
        Assert.Equal(crt0Page, match.ModulePage);
        Assert.Equal("crt0", match.ModuleName);
        Assert.Equal(0, match.ModuleCodeOffset);
    }

    [Fact]
    public void Match_HelloEntryPoint_Matches_Astart()
    {
        var parser = new DosExeParser(ExeProvider.Get("hello.c", MemoryModel.Large));
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("LLIBCE.LIB"));
        var crt0 = Crt0TestHelpers.GetCrt0Module(library);

        var disasm = new X86Disassembler(parser.Image, parser.RelocationTable);
        disasm.Disassemble((int)parser.EntryPointOffset, RegisterState.InitExe);

        var isMatch = LibraryFunctionMatcher.TryMatchModule(disasm.Instructions, crt0, crt0.CodeSegments.First(), 0);
        Assert.True(isMatch);
    }

    [Theory]
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void Match_HelloEntryPoint_DoesNotFindPrintf(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(modelCase.ExePath);
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            (int)parser.EntryPointOffset,
            library,
            RegisterState.InitExe);

        Assert.DoesNotContain(matches, static m => m.SymbolName == "_printf");
    }

    [Theory]
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void Match_HelloPrintf_DoesNotFindCrt0Symbols(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(modelCase.ExePath);
        var library = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));
        var printfOffset = PrintfOffsetFinder.Find(parser, library);

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            printfOffset,
            library,
            RegisterState.InitExe);

        Assert.DoesNotContain(matches, static m => m.SymbolName == "__astart");
    }

    [Theory]
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void Match_HelloEntryPoint_With87Lib_ReturnsEmpty(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(modelCase.ExePath);
        var mathLib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("87.LIB"));

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            (int)parser.EntryPointOffset,
            mathLib,
            RegisterState.InitExe);

        Assert.Empty(matches);
    }
}
