namespace Tests.Tools;

public static class HexConverter
{
    public static byte[] FromHexString(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
            return Array.Empty<byte>();

        // Первый проход: считаем количество валидных hex-символов
        int hexCount = CountHexCharacters(input);

        if (hexCount == 0)
            return Array.Empty<byte>();

        if (hexCount % 2 != 0)
            throw new FormatException("Нечётное количество шестнадцатеричных символов.");

        byte[] result = new byte[hexCount / 2];

        // Второй проход: заполняем массив байт
        ParseHexToBytes(input, result);

        return result;
    }

    private static int CountHexCharacters(ReadOnlySpan<char> input)
    {
        int count = 0;

        foreach (ReadOnlySpan<char> line in input.EnumerateLines())
        {
            // Обрезаем комментарий после ';'
            ReadOnlySpan<char> cleanLine = line;
            int commentIndex = line.IndexOf(';');
            if (commentIndex >= 0)
                cleanLine = line.Slice(0, commentIndex);

            // Подсчёт hex-символов
            foreach (char c in cleanLine)
            {
                if (IsHexChar(c))
                    count++;
            }
        }

        return count;
    }

    private static void ParseHexToBytes(ReadOnlySpan<char> input, Span<byte> destination)
    {
        int byteIndex = 0;
        char? highNibble = null;

        foreach (ReadOnlySpan<char> line in input.EnumerateLines())
        {
            ReadOnlySpan<char> cleanLine = line;
            int commentIndex = line.IndexOf(';');
            if (commentIndex >= 0)
                cleanLine = line.Slice(0, commentIndex);

            foreach (char c in cleanLine)
            {
                if (!IsHexChar(c))
                    continue;

                if (highNibble == null)
                {
                    highNibble = c;
                }
                else
                {
                    byte high = HexCharToNibble(highNibble.Value);
                    byte low = HexCharToNibble(c);
                    destination[byteIndex++] = (byte)((high << 4) | low);
                    highNibble = null;
                }
            }
        }
    }

    private static bool IsHexChar(char c)
    {
        return (c >= '0' && c <= '9') ||
               (c >= 'A' && c <= 'F') ||
               (c >= 'a' && c <= 'f');
    }

    private static byte HexCharToNibble(char c)
    {
        if (c >= '0' && c <= '9')
            return (byte)(c - '0');
        if (c >= 'A' && c <= 'F')
            return (byte)(c - 'A' + 10);
        if (c >= 'a' && c <= 'f')
            return (byte)(c - 'a' + 10);

        throw new ArgumentException("Неверный hex-символ");
    }
}