using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

public class ProcedureStorageTests
{
    [Fact]
    public void GetName_UnknownOffset_ReturnsSyntheticName()
    {
        var storage = new ProcedureStorage();

        Assert.Equal("sub_0042", storage.GetName(0x42));
    }

    [Fact]
    public void Add_AndTryGet_ReturnsStoredProcedure()
    {
        var storage = new ProcedureStorage();
        var procedure = new DisassembledProcedure
        {
            Offset = 0x10,
            Instructions = [],
            Expressions = null!,
            Name = "main",
            IsLibrary = false,
        };

        storage.Add(procedure);

        Assert.True(storage.TryGet(0x10, out var found));
        Assert.Same(procedure, found);
        Assert.Equal("main", storage.GetName(0x10));
    }
}
