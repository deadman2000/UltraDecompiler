using LibParser.Omf;
using UltraDecompiler.Disassembler;

namespace LibParserTests;

public sealed class OmfRelocationTableBuilderTests
{
    [Fact]
    public void Build_PrintfModule_IncludesPcRelativeCallFixups()
    {
        if (!QuickCLibAssets.Exists("CLIBC.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("CLIBC.LIB"));
        var module = lib.FindModuleBySymbol("_printf");
        Assert.NotNull(module);

        var code = module.CodeSegments.First();
        var table = OmfRelocationTableBuilder.Build(code, module.Fixups);

        Assert.Contains(table.Entries, e => e.Offset == 0x08 && e.OffsetName == "__iob");
        Assert.Contains(table.Entries, e => e.Offset == 0x1E && e.OffsetName == "__stbuf");
        Assert.Contains(table.Entries, e => e.Offset == 0x38 && e.OffsetName == "__output");
        Assert.Contains(table.Entries, e => e.Offset == 0x48 && e.OffsetName == "__ftbuf");

        var disassembler = new X86Disassembler(code.Data, table);
        disassembler.Disassemble(0);

        var calls = disassembler.Instructions.Where(static i => i.Mnemonic == Mnemonic.CALL).ToList();
        Assert.Equal(3, calls.Count);
        Assert.All(calls, static c => Assert.NotNull(c.Operand1.Relocation));
        Assert.Contains(calls, static c => c.Operand1.Relocation == "__stbuf");
        Assert.Contains(calls, static c => c.Operand1.Relocation == "__output");
        Assert.Contains(calls, static c => c.Operand1.Relocation == "__ftbuf");
    }
}
