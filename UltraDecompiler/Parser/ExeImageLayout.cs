namespace UltraDecompiler.Parser;

/// <summary>
/// Раскладка сегментов в плоском образе MZ EXE/COM после загрузки (без PSP).
/// Нужна для перевода near-указателей DGROUP в смещение в <see cref="DosExeParser.Image"/>.
/// </summary>
public sealed class ExeImageLayout
{
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
    /// Вычисляет смещение _DATA в образе по полям MZ-заголовка.
    /// </summary>
    /// <remarks>
    /// Для QuickC / Microsoft LINK (small model, DGROUP = DS = SS):
    /// <list type="bullet">
    /// <item>в файле: [CODE][инициализированный _DATA];</item>
    /// <item>после загрузки добавляется BSS (<see cref="ImageDosHeader.MinAlloc"/>);</item>
    /// <item><see cref="ImageDosHeader.InitSS"/> — параграф DGROUP, <see cref="ImageDosHeader.InitSP"/> — вершина стека внутри группы;</item>
    /// <item>near-указатель — смещение от начала DGROUP (совпадает с началом _DATA).</item>
    /// </list>
    /// Формула выведена из инвариантов layout (проверено на PROGRAMS/HELLO_S.EXE, ADD_S.EXE).
    /// </remarks>
    private static int ComputeDataSegmentOffset(DosExeParser parser)
    {
        var h = parser.DosHeader;
        int imageParagraphs = parser.Image.Length >> 4;
        int stackOffsetParagraphs = h.InitSP >> 4;
        int entryLinear = (h.InitCS << 4) + h.InitIP;

        // Параграф начала _DATA внутри образа модуля.
        int dataParagraph = h.InitSS
            - stackOffsetParagraphs
            + (h.InitIP >> 4)
            + 5
            - Math.Max(0, imageParagraphs - h.InitSS + h.MinAlloc - stackOffsetParagraphs)
            - Math.Max(0, (entryLinear - 0x32) >> 4);

        if (dataParagraph < 0)
            dataParagraph = 0;

        return dataParagraph << 4;
    }
}
