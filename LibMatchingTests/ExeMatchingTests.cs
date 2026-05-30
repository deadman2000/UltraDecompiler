using LibParser.Omf;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Parser;

namespace LibMatchingTests;

public class ExeMatchingTests
{
    [Fact]
    public void Match_HelloPrintf_With_SLIBCE()
    {
        const int printfOffset = 0x5C4;

        var parser = new DosExeParser(@"..\..\..\..\QuickC\PROGRAMS\HELLO.EXE");

        var disassembler = new X86Disassembler(parser.Image, parser.RelocationTable);
        disassembler.Disassemble(printfOffset, RegisterState.InitExe);

        var lib = OmfLibraryParser.ParseFile(@"..\..\..\..\QuickC\SLIBCE.LIB");

        // TODO disassembler должен сматчиться с lib и сказать, что это функция _printf из библиотеки SLIBCE.LIB
    }
}
