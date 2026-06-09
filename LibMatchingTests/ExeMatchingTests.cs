using LibParser.Omf;
using TestSupport;
using UltraDecompiler.Disassembler;
using UltraDecompiler.LibMatching;
using UltraDecompiler.Parser;

namespace LibMatchingTests;

/// <summary>Сопоставление hello.c (разные модели памяти) с библиотеками QuickC.</summary>
public class ExeMatchingTests
{
    [Theory]
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void Match_Printf_ForMemoryModel(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(modelCase.ExePath);
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
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void Match_EntryPoint_DoesNotFindPrintf(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(modelCase.ExePath);
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
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void Match_PrintfOffsetPlusOne_ReturnsEmpty(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(modelCase.ExePath);
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
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void Match_Printf_FindsOnlyPrintfSymbol(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(modelCase.ExePath);
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
    [MemberData(nameof(ExeMemoryModelCases.MemberData), MemberType = typeof(ExeMemoryModelCases))]
    public void Match_Printf_WrongLibrary_ReturnsEmpty(ExeMemoryModelCase modelCase)
    {
        var parser = new DosExeParser(modelCase.ExePath);
        var printfOffset = PrintfOffsetFinder.Find(
            parser,
            OmfLibraryParser.ParseFile(QuickCTestAssets.LibPathOf(modelCase.LibraryFileName)));

        var wrongLibraryName = modelCase.LibraryFileName switch
        {
            "SLIBCE.LIB" => "CLIBC.LIB",
            "CLIBC.LIB" => "SLIBCE.LIB",
            "MLIBC.LIB" => "SLIBCE.LIB",
            "LLIBCE.LIB" => "SLIBCE.LIB",
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
