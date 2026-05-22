using System.Runtime.InteropServices;
using UltraDecompiler.Header;

namespace UltraDecompiler;

public class DosExeParser
{
    public ImageDosHeader DosHeader { get; private set; }
    public RelocationEntry[] Relocations { get; private set; }
    public byte[] Image { get; private set; }          // Полный загруженный образ программы
    public long ImageBase { get; private set; }        // Линейный адрес начала программы
    public uint EntryPointOffset { get; private set; } // Смещение точки входа относительно ImageBase

    private readonly string filePath;

    public DosExeParser(string filePath)
    {
        this.filePath = filePath;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // 1. Читаем MZ-заголовок
        DosHeader = ReadStructure<ImageDosHeader>(br);

        if (DosHeader.Magic != 0x5A4D)
            throw new InvalidDataException("Не MZ-файл");

        // 2. Читаем таблицу релокаций
        ReadRelocationTable(br);

        // 3. Вычисляем размер заголовка и образа
        long headerSize = (long)DosHeader.HeaderSizeInParagraphs * 16;
        long fileSize = fs.Length;

        // Реальный размер образа (учитывая неполную последнюю страницу)
        long imageSize = CalculateImageSize();

        // 4. Загружаем программу в память (как это делает DOS)
        fs.Position = headerSize;
        Image = br.ReadBytes((int)imageSize);
        ImageBase = 0; // Обычно загружается по адресу 0x0000 в реальном режиме (но можно сдвигать)

        // 5. Вычисляем точку входа
        EntryPointOffset = (uint)(DosHeader.InitCS * 16 + DosHeader.InitIP);

        // 6. Применяем релокации (очень важно для декомпилятора!)
        ApplyRelocations();
    }

    private void ReadRelocationTable(BinaryReader br)
    {
        if (DosHeader.RelocationsCount == 0) return;

        br.BaseStream.Position = DosHeader.RelocationsTableOffset;

        Relocations = new RelocationEntry[DosHeader.RelocationsCount];

        for (int i = 0; i < DosHeader.RelocationsCount; i++)
        {
            Relocations[i] = new RelocationEntry
            {
                Offset = br.ReadUInt16(),
                Segment = br.ReadUInt16()
            };
        }
    }

    private void ApplyRelocations()
    {
        foreach (var rel in Relocations)
        {
            long linearAddr = (long)rel.Segment * 16 + rel.Offset;

            if (linearAddr + 2 > Image.Length) continue;

            // Читаем текущее значение (16-битный указатель)
            ushort value = BitConverter.ToUInt16(Image, (int)linearAddr);

            // DOS добавляет к сегменту базовый адрес загрузки (обычно PSP:0x10)
            // Для простоты считаем, что базовый адрес загрузки = 0
            value += (ushort)(ImageBase >> 4); // + параграф загрузки

            // Записываем обратно
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Image, (int)linearAddr, 2);
        }
    }

    private long CalculateImageSize()
    {
        long pages = DosHeader.NumberOfPages;
        long lastPageBytes = DosHeader.ExtraPageSize;

        long size = pages * 512;
        if (lastPageBytes != 0)
            size = (pages - 1) * 512 + lastPageBytes;

        long headerSize = (long)DosHeader.HeaderSizeInParagraphs * 16;
        return Math.Min(size - headerSize, new FileInfo(filePath).Length - headerSize);
    }

    private static T ReadStructure<T>(BinaryReader br) where T : struct
    {
        byte[] bytes = br.ReadBytes(Marshal.SizeOf(typeof(T)));
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    public void PrintInfo()
    {
        Console.WriteLine("=== 16-bit MS-DOS EXE для декомпилятора ===");
        Console.WriteLine($"Файл: {filePath}");
        Console.WriteLine($"Размер заголовка: {DosHeader.HeaderSizeInParagraphs * 16} байт");
        Console.WriteLine($"Точка входа     : {DosHeader.InitCS:X4}:{DosHeader.InitIP:X4} (линейно: 0x{EntryPointOffset:X6})");
        Console.WriteLine($"Релокаций       : {Relocations.Length}");
        Console.WriteLine($"Размер образа   : {Image.Length} байт");
        Console.WriteLine($"Мин. память     : {DosHeader.MinAlloc * 16} байт");
        Console.WriteLine($"Макс. память    : {DosHeader.MaxAlloc * 16} байт");
    }
}
