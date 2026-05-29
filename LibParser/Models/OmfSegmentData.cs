namespace LibParser.Models;

/// <summary>Данные одного логического сегмента модуля (объединённые LEDATA/LIDATA).</summary>
public sealed class OmfSegmentData
{
    public required int SegmentIndex { get; init; }

    public required string SegmentName { get; init; }

    public required string ClassName { get; init; }

    /// <summary>Содержимое сегмента (разреженные области заполнены нулями).</summary>
    public required byte[] Data { get; init; }

    /// <summary>Сегмент с классом CODE (типичный кодовый сегмент QuickC).</summary>
    public bool IsCode => string.Equals(ClassName, "CODE", StringComparison.OrdinalIgnoreCase);
}
