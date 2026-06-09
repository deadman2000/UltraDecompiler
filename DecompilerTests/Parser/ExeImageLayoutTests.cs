using DecompilerTests.Decompilation;
using UltraDecompiler.Parser;

namespace DecompilerTests.Parser;

public class ExeImageLayoutTests
{
    [Theory]
    [InlineData("HELLO_S.EXE", 5728, "Hello world\n")]
    [InlineData("HELLO_GS.EXE", 5728, "Hello world\n")]
    [InlineData("ADD_S.EXE", 5776, "%d")]
    [InlineData("ADD_GS.EXE", 5776, "%d")]
    public void From_SmallModel_ResolvesDataSegmentAndString(string exeName, int expectedDataOffset, string expectedString)
    {
        var parser = new DosExeParser(QuickCTestAssets.ProgramsPathOf(exeName));
        var layout = ExeImageLayout.From(parser);

        Assert.Equal(expectedDataOffset, layout.DataSegmentOffset);

        const int formatStringNearOffset = 618;
        int phys = layout.ToImageOffset(formatStringNearOffset);
        var bytes = new List<byte>();
        for (int i = phys; i < parser.Image.Length && parser.Image[i] != 0; i++)
        {
            bytes.Add(parser.Image[i]);
        }

        Assert.Equal(expectedString, System.Text.Encoding.ASCII.GetString(bytes.ToArray()));
    }
}
