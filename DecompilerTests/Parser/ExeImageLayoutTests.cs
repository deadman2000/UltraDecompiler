using TestSupport;
using UltraDecompiler.Compilation;
using UltraDecompiler.Parser;

namespace DecompilerTests.Parser;

public class ExeImageLayoutTests
{
    [Theory]
    [InlineData("hello.c", true, 5728, "Hello world\n")]
    [InlineData("hello.c", false, 5728, "Hello world\n")]
    [InlineData("add.c", true, 5776, "%d")]
    [InlineData("add.c", false, 5776, "%d")]
    public void From_SmallModel_ResolvesDataSegmentAndString(
        string sourceFileName,
        bool stackCheck,
        int expectedDataOffset,
        string expectedString)
    {
        var parser = new DosExeParser(ExeProvider.Get(sourceFileName, MemoryModel.Small, stackCheck));
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
