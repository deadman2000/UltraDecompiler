using System.Runtime.InteropServices;

namespace UltraDecompiler.Disassembly.Parser;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ImageDosHeader
{
    public ushort Magic;                  // 0x00 - Сигнатура "MZ" (0x5A4D)
    public ushort ExtraPageSize;          // 0x02 - Байты на последней странице
    public ushort NumberOfPages;          // 0x04 - Количество страниц в файле
    public ushort RelocationsCount;       // 0x06 - Количество элементов в таблице релокаций
    public ushort HeaderSizeInParagraphs; // 0x08 - Размер заголовка в параграфах
    public ushort MinAlloc;               // 0x0A - Минимальное количество дополнительных параграфов
    public ushort MaxAlloc;               // 0x0C - Максимальное количество дополнительных параграфов
    public ushort InitSS;                 // 0x0E - Начальное значение SS
    public ushort InitSP;                 // 0x10 - Начальное значение SP
    public ushort CheckSum;               // 0x12 - Контрольная сумма
    public ushort InitIP;                 // 0x14 - Начальное значение IP
    public ushort InitCS;                 // 0x16 - Начальное значение CS
    public ushort RelocationsTableOffset; // 0x18 - Адрес таблицы релокаций
    public ushort OverlayNumber;          // 0x1A - Номер оверлея
}
