using Common;

namespace UltraDecompiler.Parser;

/// <summary>
/// Раскладка сегментов в плоском образе MZ EXE/COM после загрузки (без PSP).
/// Нужна для перевода near-указателей DGROUP в смещение в <see cref="DosExeParser.Image"/>.
/// </summary>
public sealed class ExeImageLayout
{
    /// <summary>
    /// Параграфов между началом _DATA и параграфом первой DATA-релокации в MZ (QuickC / LINK small).
    /// </summary>
    private const int DataRelocParagraphLead = 4;

    /// <summary>
    /// Смещение начала DGROUP/_DATA в байтах от начала <see cref="DosExeParser.Image"/>.
    /// Near-указатель <c>imm16</c> в small model — это смещение относительно DS (база DGROUP).
    /// </summary>
    public int DataSegmentOffset { get; }

    public ExeImageLayout(int dataSegmentOffset)
    {
        DataSegmentOffset = dataSegmentOffset;
    }

    /// <summary>
    /// Строит раскладку из распарсенного образа.
    /// </summary>
    public static ExeImageLayout From(DosExeParser parser)
    {
        if (parser.IsCom)
            return new ExeImageLayout(0);

        return new ExeImageLayout(ComputeDataSegmentOffset(parser));
    }

    /// <summary>
    /// Переводит near-смещение (логический адрес в DGROUP) в индекс байта в <see cref="DosExeParser.Image"/>.
    /// </summary>
    public int ToImageOffset(int nearOffset) => DataSegmentOffset + nearOffset;

    /// <summary>
    /// Вычисляет смещение _DATA в образе.
    /// </summary>
    /// <remarks>
    /// Для QuickC / Microsoft LINK (small model): в файле [CODE][инициализированный _DATA];
    /// near-указатель — смещение от начала DGROUP (совпадает с началом _DATA).
    /// Граница CODE/_DATA берётся из первой MZ-релокации в DATA (segment &gt; 0):
    /// LINK ставит fixup на ~74 байта после начала _DATA, в параграфе на 4 para выше базы DATA.
    /// </remarks>
    private static int ComputeDataSegmentOffset(DosExeParser parser)
    {
        int? fromRelocations = TryComputeFromRelocations(parser.Relocations);
        if (fromRelocations is int offset)
            return offset;

        return ComputeDataSegmentOffsetFromHeader(parser);
    }

    /// <summary>
    /// Граница CODE/_DATA по таблице релокаций MZ (не зависит от InitIP и размера crt0).
    /// </summary>
    private static int? TryComputeFromRelocations(RelocationEntry[] relocations)
    {
        int minDataSegment = int.MaxValue;
        foreach (var reloc in relocations)
        {
            if (reloc.Segment == 0)
                continue;

            if (reloc.Segment < minDataSegment)
                minDataSegment = reloc.Segment;
        }

        if (minDataSegment == int.MaxValue)
            return null;

        int dataParagraph = minDataSegment - DataRelocParagraphLead;
        if (dataParagraph < 0)
            return null;

        return dataParagraph << 4;
    }

    /// <summary>
    /// Запасной расчёт по полям MZ-заголовка, если DATA-релокаций нет.
    /// </summary>
    private static int ComputeDataSegmentOffsetFromHeader(DosExeParser parser)
    {
        var h = parser.DosHeader;
        int imageParagraphs = parser.Image.Length >> 4;
        int stackOffsetParagraphs = h.InitSP >> 4;

        int dataParagraph = h.InitSS
            - stackOffsetParagraphs
            + 5
            - Math.Max(0, imageParagraphs - h.InitSS + h.MinAlloc - stackOffsetParagraphs);

        if (dataParagraph < 0)
            dataParagraph = 0;

        return dataParagraph << 4;
    }
}
