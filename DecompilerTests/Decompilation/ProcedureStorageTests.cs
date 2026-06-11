using UltraDecompiler.Decompilation;

namespace DecompilerTests.Decompilation;

/// <summary>Реестр процедур: синтетические имена sub_* и поиск по смещению.</summary>
public class ProcedureStorageTests
{
    // Неизвестное смещение → имя sub_0042 (hex offset в имени)
    [Fact]
    public void GetName_UnknownOffset_ReturnsSyntheticName()
    {
        var storage = new ProcedureStorage();

        Assert.Equal("sub_0042", storage.GetName(0x42));
    }

    // Add + TryGet возвращают ту же процедуру; GetName отдаёт заданное имя
    [Fact]
    public void Add_AndTryGet_ReturnsStoredProcedure()
    {
        var storage = new ProcedureStorage();
        var procedure = new DisassembledProcedure
        {
            Offset = 0x10,
            Instructions = [],
            Name = "main",
            IsLibrary = false,
        };

        storage.Add(procedure);

        Assert.True(storage.TryGet(0x10, out var found));
        Assert.Same(procedure, found);
        Assert.Equal("main", storage.GetName(0x10));
    }
}
