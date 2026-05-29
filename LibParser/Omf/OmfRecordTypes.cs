namespace LibParser.Omf;

/// <summary>Типы записей OMF, используемые при разборе библиотек QuickC.</summary>
internal static class OmfRecordTypes
{
    public const byte Theadr = 0x80;
    public const byte Lheadr = 0x82;
    public const byte Coment = 0x88;
    public const byte Modend = 0x8A;
    public const byte Modend32 = 0x8B;
    public const byte Extdef = 0x8C;
    public const byte Pubdef = 0x90;
    public const byte Pubdef32 = 0x91;
    public const byte Lnames = 0x96;
    public const byte Segdef = 0x98;
    public const byte Segdef32 = 0x99;
    public const byte Grpdef = 0x9A;
    public const byte Fixup = 0x9C;
    public const byte Fixup32 = 0x9D;
    public const byte Ledata = 0xA0;
    public const byte Ledata32 = 0xA1;
    public const byte Lidata = 0xA2;
    public const byte Lidata32 = 0xA3;
    public const byte LibraryHeader = 0xF0;
    public const byte LibraryEnd = 0xF1;
    public const byte ExtendedDictionary = 0xF2;

    public const byte ComentClassLibMod = 0xA3;
}
