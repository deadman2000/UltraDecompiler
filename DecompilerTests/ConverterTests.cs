using Tests.Tools;

namespace Tests;

public class ConverterTests
{
    [Fact]
    public void HexStringWithComment()
    {
        var bytes = """
            AA BB 1234 CDEF ; Привет
            001122
            """.FromHex();

        byte[] expected =
        [
            0xAA, 0xBB, 0x12, 0x34, 0xCD, 0xEF,
            0x00, 0x11, 0x22
        ];

        Assert.Equal(bytes, expected);
    }

    [Fact]
    public void HexStringWithComment2()
    {
        string input = """
        4D 5A 90 00 03 00 00 00 ; MZ header
        04 00 00 00 FF FF 00 00
        B8 00 00 00 00 00 00 00
        """;

        byte[] expected =
        [
            0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00,
            0xB8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];

        byte[] actual = HexConverter.FromHexString(input);

        Assert.Equal(expected, actual);
    }
}
