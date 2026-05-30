using Common;
using LibMatching;
using UltraDecompiler.Disassembler;

namespace LibMatchingTests;

/// <summary>Unit-тесты извлечения и сравнения тел функций.</summary>
public class FunctionBodyTests
{
    [Fact]
    public void Extract_SimplePrologueEpilogue_StopsAtRet()
    {
        // push bp; mov bp, sp; ret
        byte[] code = [0x55, 0x8B, 0xEC, 0xC3];

        var body = FunctionBodyExtractor.Extract(code, RelocationTable.Empty, 0, RegisterState.Unknown);

        Assert.Equal(3, body.Count);
        Assert.Equal(Mnemonic.PUSH, body[0].Mnemonic);
        Assert.Equal(Mnemonic.MOV, body[1].Mnemonic);
        Assert.Equal(Mnemonic.RET, body[2].Mnemonic);
    }

    [Fact]
    public void Extract_OffsetBeyondImage_ReturnsEmpty()
    {
        byte[] code = [0x55, 0x8B, 0xEC, 0xC3];

        var body = FunctionBodyExtractor.Extract(code, RelocationTable.Empty, code.Length, RegisterState.Unknown);

        Assert.Empty(body);
    }

    [Fact]
    public void Extract_IncludesCallWithoutFollowingCallee()
    {
        // push bp; call +2; ret; nop; ret (callee)
        byte[] code = [0x55, 0xE8, 0x02, 0x00, 0xC3, 0x90, 0xC3];

        var body = FunctionBodyExtractor.Extract(code, RelocationTable.Empty, 0, RegisterState.Unknown);

        Assert.Equal(3, body.Count);
        Assert.Equal(Mnemonic.CALL, body[1].Mnemonic);
        Assert.Equal(Mnemonic.RET, body[2].Mnemonic);
    }

    [Fact]
    public void Comparer_IdenticalBodies_AreEquivalent()
    {
        byte[] code = [0x55, 0x8B, 0xEC, 0xC3];
        var left = FunctionBodyExtractor.Extract(code, RelocationTable.Empty, 0, RegisterState.Unknown);
        var right = FunctionBodyExtractor.Extract(code, RelocationTable.Empty, 0, RegisterState.Unknown);

        Assert.True(FunctionBodyComparer.AreEquivalent(left, right, RelocationTable.Empty));
    }

    [Fact]
    public void Comparer_DifferentCallTarget_StillEquivalentWhenLibHasFixup()
    {
        byte[] libraryCode = [0xE8, 0x00, 0x00, 0xC3];
        byte[] imageCode = [0xE8, 0x05, 0x00, 0xC3];
        RelocationTable libraryRelocations = new("", [
            new RelocationEntry { Segment = 0, Offset = 1, OffsetName = "__helper" },
        ]);

        var libraryBody = FunctionBodyExtractor.Extract(libraryCode, libraryRelocations, 0, RegisterState.Unknown);
        var imageBody = FunctionBodyExtractor.Extract(imageCode, RelocationTable.Empty, 0, RegisterState.Unknown);

        Assert.True(FunctionBodyComparer.AreEquivalent(imageBody, libraryBody, libraryRelocations));
    }

    [Fact]
    public void Comparer_DifferentNonRelocImmediate_NotEquivalent()
    {
        // mov ax, 1; ret  vs  mov ax, 2; ret
        byte[] leftCode = [0xB8, 0x01, 0x00, 0xC3];
        byte[] rightCode = [0xB8, 0x02, 0x00, 0xC3];

        var left = FunctionBodyExtractor.Extract(leftCode, RelocationTable.Empty, 0, RegisterState.Unknown);
        var right = FunctionBodyExtractor.Extract(rightCode, RelocationTable.Empty, 0, RegisterState.Unknown);

        Assert.False(FunctionBodyComparer.AreEquivalent(left, right, RelocationTable.Empty));
    }

    [Fact]
    public void Comparer_ShortJumpSameDisplacement_DifferentTargetAddress_StillEquivalent()
    {
        byte[] libraryCode = [0x3C, 0x02, 0x73, 0x02, 0xC3];
        byte[] imageCode = new byte[0x20];
        Array.Copy(libraryCode, 0, imageCode, 0x10, libraryCode.Length);

        var libraryBody = FunctionBodyExtractor.Extract(libraryCode, RelocationTable.Empty, 0, RegisterState.Unknown);
        var imageBody = FunctionBodyExtractor.Extract(imageCode, RelocationTable.Empty, 0x10, RegisterState.Unknown);

        Assert.True(FunctionBodyComparer.AreEquivalent(imageBody, libraryBody, RelocationTable.Empty));
    }
}
