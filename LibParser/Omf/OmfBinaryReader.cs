namespace LibParser.Omf;

/// <summary>Чтение полей OMF (little-endian, индексы).</summary>
internal ref struct OmfBinaryReader
{
    private ReadOnlySpan<byte> _data;
    private int _pos;

    public OmfBinaryReader(ReadOnlySpan<byte> data, int start = 0)
    {
        _data = data;
        _pos = start;
    }

    public int Position => _pos;

    public bool End => _pos >= _data.Length;

    public byte ReadByte()
    {
        if (_pos >= _data.Length)
        {
            throw new InvalidDataException("Неожиданный конец данных OMF.");
        }

        return _data[_pos++];
    }

    public ushort ReadUInt16()
    {
        if (_pos + 2 > _data.Length)
        {
            throw new InvalidDataException("Неожиданный конец данных OMF.");
        }

        var value = ReadUInt16At(_data, _pos);
        _pos += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        if (_pos + 4 > _data.Length)
        {
            throw new InvalidDataException("Неожиданный конец данных OMF.");
        }

        var value = ReadUInt32At(_data, _pos);
        _pos += 4;
        return value;
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        if (_pos + count > _data.Length)
        {
            throw new InvalidDataException("Неожиданный конец данных OMF.");
        }

        var slice = _data.Slice(_pos, count);
        _pos += count;
        return slice;
    }

    public string ReadCountedAscii()
    {
        var length = ReadByte();
        if (length == 0)
        {
            return string.Empty;
        }

        var bytes = ReadBytes(length);
        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    /// <summary>Индекс OMF (1 или 2 байта).</summary>
    public int ReadIndex()
    {
        if (!TryReadIndex(out var index))
        {
            throw new InvalidDataException("Неожиданный конец данных OMF.");
        }

        return index;
    }

    /// <summary>Читает индекс, если в буфере достаточно байт.</summary>
    public bool TryReadIndex(out int index)
    {
        if (_pos >= _data.Length)
        {
            index = 0;
            return false;
        }

        var first = _data[_pos];
        if ((first & 0x80) == 0)
        {
            _pos++;
            index = first;
            return true;
        }

        if (_pos + 1 >= _data.Length)
        {
            index = 0;
            return false;
        }

        var second = _data[_pos + 1];
        _pos += 2;
        index = ((first & 0x7F) << 8) | second;
        return true;
    }

    public bool TryReadUInt16(out ushort value)
    {
        if (_pos + 2 > _data.Length)
        {
            value = 0;
            return false;
        }

        value = ReadUInt16At(_data, _pos);
        _pos += 2;
        return true;
    }

    public bool TryReadUInt32(out uint value)
    {
        if (_pos + 4 > _data.Length)
        {
            value = 0;
            return false;
        }

        value = ReadUInt32At(_data, _pos);
        _pos += 4;
        return true;
    }

    public void Skip(int count) => _pos += count;

    public static ushort ReadUInt16At(ReadOnlySpan<byte> data, int offset) =>
        (ushort)(data[offset] | (data[offset + 1] << 8));

    public static uint ReadUInt32At(ReadOnlySpan<byte> data, int offset) =>
        (uint)(data[offset]
            | (data[offset + 1] << 8)
            | (data[offset + 2] << 16)
            | (data[offset + 3] << 24));
}
