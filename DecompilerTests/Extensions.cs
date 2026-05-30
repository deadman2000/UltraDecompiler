using DecompilerTests.Tools;

namespace DecompilerTests;

internal static class Extensions
{
    extension(string str)
    {
        public byte[] FromHex()
        {
            return HexConverter.FromHexString(str.AsSpan());
        }
    }
}
