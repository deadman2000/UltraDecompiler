using LibMatching;
using LibParser.Omf;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Parser;

namespace LibMatchingTests;

/// <summary>Сопоставление hello.c (разные модели памяти) с библиотеками QuickC.</summary>
public class ExeMatchingTests
{
    [Theory]
    [MemberData(nameof(HelloMemoryModelCases.MemberData), MemberType = typeof(HelloMemoryModelCases))]
    public void Match_Printf_ForMemoryModel(HelloMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(modelCase.ExeFileName));
        var lib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));
        var printfOffset = PrintfOffsetFinder.Find(parser, lib);

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            printfOffset,
            lib,
            RegisterState.InitExe);

        Assert.Contains(matches, static m => m.SymbolName == "_printf");

        var match = matches.First(static m => m.SymbolName == "_printf");
        Assert.Equal("printf", match.ModuleName);
        Assert.Equal(0, match.ModuleCodeOffset);
    }

    [Theory]
    [MemberData(nameof(HelloMemoryModelCases.MemberData), MemberType = typeof(HelloMemoryModelCases))]
    public void Match_EntryPoint_DoesNotFindPrintf(HelloMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(modelCase.ExeFileName));
        var lib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            (int)parser.EntryPointOffset,
            lib,
            RegisterState.InitExe);

        Assert.DoesNotContain(matches, static m => m.SymbolName == "_printf");
    }

    [Theory]
    [MemberData(nameof(HelloMemoryModelCases.MemberData), MemberType = typeof(HelloMemoryModelCases))]
    public void Match_PrintfOffsetPlusOne_ReturnsEmpty(HelloMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(modelCase.ExeFileName));
        var lib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));
        var printfOffset = PrintfOffsetFinder.Find(parser, lib);

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            printfOffset + 1,
            lib,
            RegisterState.InitExe);

        Assert.Empty(matches);
    }

    [Theory]
    [MemberData(nameof(HelloMemoryModelCases.MemberData), MemberType = typeof(HelloMemoryModelCases))]
    public void Match_Printf_With87Lib_ReturnsEmpty(HelloMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(modelCase.ExeFileName));
        var mathLib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf("87.LIB"));
        var printfOffset = PrintfOffsetFinder.Find(
            parser,
            OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName)));

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            printfOffset,
            mathLib,
            RegisterState.InitExe);

        Assert.Empty(matches);
    }

    [Theory]
    [MemberData(nameof(HelloMemoryModelCases.MemberData), MemberType = typeof(HelloMemoryModelCases))]
    public void Match_Printf_FindsOnlyPrintfSymbol(HelloMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(modelCase.ExeFileName));
        var lib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName));
        var printfOffset = PrintfOffsetFinder.Find(parser, lib);

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            printfOffset,
            lib,
            RegisterState.InitExe);

        Assert.All(matches, static m => Assert.Equal("_printf", m.SymbolName));
    }

    [Theory]
    [MemberData(nameof(HelloMemoryModelCases.MemberData), MemberType = typeof(HelloMemoryModelCases))]
    public void Match_Printf_WrongLibrary_ReturnsEmpty(HelloMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(modelCase.ExeFileName));
        var printfOffset = PrintfOffsetFinder.Find(
            parser,
            OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName)));

        var wrongLibraryName = modelCase.LibraryFileName switch
        {
            "SLIBCE.LIB" => "CLIBC.LIB",
            "CLIBC.LIB" => "SLIBCE.LIB",
            "MLIBC.LIB" => "SLIBCE.LIB",
            "LLIBC.LIB" => "SLIBCE.LIB",
            _ => throw new InvalidOperationException($"Неизвестная библиотека: {modelCase.LibraryFileName}"),
        };

        var wrongLib = OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(wrongLibraryName));

        var matches = LibraryFunctionMatcher.Match(
            parser.Image,
            parser.RelocationTable,
            printfOffset,
            wrongLib,
            RegisterState.InitExe);

        Assert.DoesNotContain(matches, static m => m.SymbolName == "_printf");
    }
}
