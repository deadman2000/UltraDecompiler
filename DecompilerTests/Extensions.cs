using Tests.Tools;

namespace Tests;

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
