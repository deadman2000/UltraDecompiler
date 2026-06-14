namespace UltraDecompiler.Disassembly.Common;

public static class Extensions
{
    extension(int value)
    {
        public string ToHex()
        {
            if (value > -10 && value < 10)
                return value.ToString();

            if (value < 0x100)
                return $"{value:X2}h";

            return $"{value:X4}h";
        }
    }

    extension(ushort value)
    {
        public string ToHex()
        {
            if (value < 10)
                return value.ToString();

            if (value < 0x100)
                return $"{value:X2}h";

            return $"{value:X4}h";
        }
    }
}
