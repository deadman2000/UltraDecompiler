using LibParser.Omf;
using UltraDecompiler.Disassembler;

namespace LibParserTests;

/// <summary>Релокации PUSH в crt0 <c>__astart</c> (LLIBCE.LIB).</summary>
public sealed class AstartRelocationTests
{
    [Fact]
    public void Disassemble_Astart_PushArgvEnvironHaveFixupNames()
    {
        if (!QuickCLibAssets.Exists("LLIBCE.LIB"))
        {
            return;
        }

        var lib = OmfLibraryParser.ParseFile(QuickCLibAssets.PathOf("LLIBCE.LIB"));
        var module = lib.FindModuleBySymbol("__astart");
        Assert.NotNull(module);

        var code = module.CodeSegments.First();
        var table = OmfRelocationTableBuilder.Build(code, module.Fixups);
        var disassembler = new X86Disassembler(code.Data, table);
        disassembler.Disassemble(0);

        var pushes = disassembler.Instructions
            .Where(static i => i.Mnemonic == Mnemonic.PUSH && i.Operand1.Type == OperandType.Memory)
            .ToList();

        Assert.True(pushes.Count >= 5);
        Assert.All(pushes, static p => Assert.NotNull(p.Operand1.Relocation));
        Assert.Contains(pushes, static p => p.Operand1.Relocation == "_environ+0x2");
        Assert.Contains(pushes, static p => p.Operand1.Relocation == "_environ");
        Assert.Contains(pushes, static p => p.Operand1.Relocation == "___argv+0x2");
        Assert.Contains(pushes, static p => p.Operand1.Relocation == "___argv");
        Assert.Contains(pushes, static p => p.Operand1.Relocation == "___argc");
    }
}
