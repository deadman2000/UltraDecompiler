using System.Runtime.InteropServices;
using Common;

namespace UltraDecompiler.Disassembly.Parser;

public class DosExeParser
{
    private readonly string _filePath;

    public ImageDosHeader DosHeader { get; private set; }
    public RelocationEntry[] Relocations { get; private set; }
    public RelocationTable RelocationTable { get; private set; } = RelocationTable.Empty;
    public byte[] Image { get; private set; }          // Образ программы из файла
    public long ImageBase { get; private set; }        // Линейный адрес начала программы
    public uint EntryPointOffset { get; private set; } // Смещение точки входа относительно ImageBase
    public bool IsCom { get; private set; }            // true для .COM файлов (без MZ заголовка)

    public DosExeParser(string filePath)
    {
        _filePath = filePath;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // Проверяем сигнатуру: .COM файлы не имеют MZ заголовка
        if (fs.Length >= 2)
        {
            ushort magic = br.ReadUInt16();
            fs.Position = 0;

            if (magic != 0x5A4D)
            {
                // Это .COM файл (или сырой бинарник)
                IsCom = true;
                Relocations = [];
                RelocationTable = RelocationTable.Empty;
                DosHeader = default; // нет заголовка

                // Для .COM: загружаем весь файл как есть. Точка входа = 0
                Image = br.ReadBytes((int)fs.Length);
                ImageBase = 0;
                EntryPointOffset = 0;

                return;
            }
        }

        // === Обычный MZ EXE ===
        IsCom = false;

        // Читаем MZ-заголовок
        DosHeader = ReadStructure<ImageDosHeader>(br);

        if (DosHeader.Magic != 0x5A4D)
            throw new InvalidDataException("Не MZ-файл");

        // Читаем таблицу релокаций (fixup выполняет дизассемблер при парсинге операций)
        Relocations = ReadRelocationTable(br);
        RelocationTable = new RelocationTable("__image", Relocations);

        // Вычисляем размер заголовка и образа
        long headerSize = (long)DosHeader.HeaderSizeInParagraphs * 16;
        long fileSize = fs.Length;

        // Реальный размер образа (учитывая неполную последнюю страницу)
        long imageSize = CalculateImageSize();

        // Загружаем программу в память (как это делает DOS)
        fs.Position = headerSize;
        Image = br.ReadBytes((int)imageSize);
        ImageBase = 0; // Обычно загружается по адресу 0x0000 в реальном режиме (но можно сдвигать)

        // Вычисляем точку входа
        EntryPointOffset = (uint)(DosHeader.InitCS * 16 + DosHeader.InitIP);
    }

    private RelocationEntry[] ReadRelocationTable(BinaryReader br)
    {
        if (DosHeader.RelocationsCount == 0) return [];

        br.BaseStream.Position = DosHeader.RelocationsTableOffset;

        var relocations = new RelocationEntry[DosHeader.RelocationsCount];

        for (int i = 0; i < DosHeader.RelocationsCount; i++)
        {
            relocations[i] = new RelocationEntry
            {
                Offset = br.ReadUInt16(),
                Segment = br.ReadUInt16()
            };
        }

        return relocations;
    }

    private long CalculateImageSize()
    {
        long pages = DosHeader.NumberOfPages;
        long lastPageBytes = DosHeader.ExtraPageSize;

        long size = pages * 512;
        if (lastPageBytes != 0)
            size = (pages - 1) * 512 + lastPageBytes;

        long headerSize = (long)DosHeader.HeaderSizeInParagraphs * 16;
        return Math.Min(size - headerSize, new FileInfo(_filePath).Length - headerSize);
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
        if (IsCom)
        {
            Console.WriteLine("=== 16-bit MS-DOS .COM ===");
            Console.WriteLine($"Файл: {_filePath}");
            Console.WriteLine($"Тип:  .COM (нет MZ заголовка)");
            Console.WriteLine($"Точка входа     : 0000:0000 (линейно: 0x{EntryPointOffset:X6})");
            Console.WriteLine($"Размер образа   : {Image.Length} байт");
            return;
        }

        Console.WriteLine("=== 16-bit MS-DOS EXE ===");
        Console.WriteLine($"Файл: {_filePath}");
        Console.WriteLine($"Размер заголовка: {DosHeader.HeaderSizeInParagraphs * 16} байт");
        Console.WriteLine($"Точка входа     : {DosHeader.InitCS:X4}:{DosHeader.InitIP:X4} (линейно: 0x{EntryPointOffset:X6})");
        Console.WriteLine($"Релокаций       : {Relocations.Length}");
        Console.WriteLine($"Размер образа   : {Image.Length} байт");
        Console.WriteLine($"Мин. память     : {DosHeader.MinAlloc * 16} байт");
        Console.WriteLine($"Макс. память    : {DosHeader.MaxAlloc * 16} байт");

        /*Console.WriteLine("Релокации:");
        for (int i = 0; i < Relocations.Length; i++)
        {
            var rel = Relocations[i];
            Console.WriteLine($"  {rel.LinearAddress:X4}");
        }*/
    }
}
