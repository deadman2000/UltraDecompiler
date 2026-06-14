using Common;
using LibParser.Models;

namespace UltraDecompiler.LibMatching;

/// <summary>
/// Разрешает адреса целевых символов по вызовам (CALL) из кода библиотечных функций
/// с использованием метаданных FIXUPP внутри OMF-модулей .LIB.
/// 
/// Основной метод — <see cref="FindCalledSymbol"/> (универсальный поиск любого символа).
/// Для классического случая crt0 есть удобный метод <see cref="FindMainFromAstart"/>.
/// </summary>
public static class LibraryCallResolver
{
    /// <summary>
    /// Находит смещение <c>_main</c>, следуя по вызову из <c>__astart</c> (crt0) по информации FIXUPP библиотеки.
    /// </summary>
    public static int FindMainFromAstart(
        byte[] image,
        RelocationTable imageRelocations,
        OmfLibrary library,
        int astartOffset,
        RegisterState initRegisters,
        int astartModuleCodeOffset = 0) =>
        FindCalledSymbol(
            image,
            imageRelocations,
            library,
            callerSymbolName: "__astart",
            targetSymbolName: "_main",
            callerImageOffset: astartOffset,
            initRegisters: initRegisters,
            callerModuleCodeOffset: astartModuleCodeOffset);

    /// <summary>
    /// Универсальный поиск: находит адрес целевого символа <paramref name="targetSymbolName"/>,
    /// на который ссылается вызов (near/far CALL) из кода <paramref name="callerSymbolName"/>
    /// внутри соответствующего модуля библиотеки.
    /// </summary>
    /// <param name="callerSymbolName">Имя символа-источника вызова (например "__astart"). Используется для поиска модуля.</param>
    /// <param name="targetSymbolName">Имя искомого целевого символа (например "_main", "_printf").</param>
    /// <param name="callerImageOffset">Смещение начала caller'а в образе EXE/COM.</param>
    /// <param name="callerModuleCodeOffset">Смещение начала caller'а внутри CODE модуля библиотеки (обычно 0).</param>
    public static int FindCalledSymbol(
        byte[] image,
        RelocationTable imageRelocations,
        OmfLibrary library,
        string callerSymbolName,
        string targetSymbolName,
        int callerImageOffset,
        RegisterState initRegisters,
        int callerModuleCodeOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerSymbolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSymbolName);

        var module = GetModuleForCaller(library, callerSymbolName);
        var callFixup = module.Fixups.FirstOrDefault(f => IsCallFixupTo(f, targetSymbolName))
            ?? throw new InvalidOperationException(
                $"В модуле символа '{callerSymbolName}' библиотеки не найден FIXUP для вызова '{targetSymbolName}'.");

        var baseInImage = callerImageOffset - callerModuleCodeOffset;
        var callOffsetInImage = baseInImage + callFixup.SegmentOffset - 1;

        return ResolveCallTarget(
            image,
            imageRelocations,
            callOffsetInImage,
            callFixup,
            initRegisters,
            callerImageOffset,
            targetSymbolName);
    }

    private static OmfModule GetModuleForCaller(OmfLibrary library, string callerSymbolName)
    {
        // Лучший способ — найти модуль по публичному символу (словарь библиотеки).
        var module = library.FindModuleBySymbol(callerSymbolName);
        if (module is not null)
        {
            return module;
        }

        // Fallback для типичного случая crt0 (на случай алиасов или если символ не опубликован в словаре).
        if (string.Equals(callerSymbolName, "__astart", StringComparison.OrdinalIgnoreCase)
            || callerSymbolName.Contains("astart", StringComparison.OrdinalIgnoreCase)
            || callerSymbolName.Equals("crt0", StringComparison.OrdinalIgnoreCase))
        {
            return GetCrt0Module(library);
        }

        throw new InvalidOperationException(
            $"Не удалось определить модуль для символа-источника '{callerSymbolName}' в библиотеке.");
    }

    private static bool IsCallFixupTo(OmfFixup fixup, string targetName)
    {
        if (fixup.Target.Kind != OmfFixupDatumKind.Extdef)
        {
            return false;
        }

        if (!string.Equals(fixup.Target.Name, targetName, StringComparison.Ordinal))
        {
            return false;
        }

        return fixup.LocationType is OmfFixupLocationType.Offset16 or OmfFixupLocationType.Pointer32;
    }

    private static int ResolveCallTarget(
        byte[] image,
        RelocationTable imageRelocations,
        int callOffset,
        OmfFixup callFixup,
        RegisterState initRegisters,
        int callerImageOffset,
        string targetSymbol)
    {
        if (callOffset < 0 || callOffset >= image.Length)
        {
            throw new InvalidOperationException(
                $"Смещение вызова {targetSymbol} 0x{callOffset:X} вне образа.");
        }

        return callFixup.LocationType switch
        {
            OmfFixupLocationType.Offset16 => ResolveNearCallTarget(
                image,
                imageRelocations,
                callOffset,
                initRegisters,
                targetSymbol),
            OmfFixupLocationType.Pointer32 => ResolveFarCallOffset(
                image,
                callOffset,
                callerImageOffset,
                targetSymbol),
            _ => throw new InvalidOperationException(
                $"Неподдерживаемый тип FIXUP для {targetSymbol}: {callFixup.LocationType}."),
        };
    }

    private static int ResolveNearCallTarget(
        byte[] image,
        RelocationTable imageRelocations,
        int callOffset,
        RegisterState initRegisters,
        string targetSymbol)
    {
        if (image[callOffset] != 0xE8)
        {
            throw new InvalidOperationException(
                $"Ожидался near CALL (E8) по смещению 0x{callOffset:X} для {targetSymbol}, получен 0x{image[callOffset]:X2}.");
        }

        var disassembler = new X86Disassembler(image, imageRelocations);
        disassembler.Disassemble(callOffset, initRegisters);

        var callInstruction = disassembler.Instructions.FirstOrDefault(i => i.Offset == callOffset)
            ?? throw new InvalidOperationException(
                $"Не удалось дизассемблировать CALL {targetSymbol} по 0x{callOffset:X}.");

        var target = callInstruction.GetJumpTarget();
        if (target < 0 || target >= image.Length)
        {
            throw new InvalidOperationException(
                $"Некорректный адрес {targetSymbol}: 0x{target:X}.");
        }

        return target;
    }

    private static int ResolveFarCallOffset(byte[] image, int callOffset, int callerImageOffset, string targetSymbol)
    {
        if (image[callOffset] != 0x9A)
        {
            throw new InvalidOperationException(
                $"Ожидался far CALL (9A) по смещению 0x{callOffset:X} для {targetSymbol}, получен 0x{image[callOffset]:X2}.");
        }

        // CALL FAR ptr16:16 — IP по +1, CS по +3 (little-endian).
        var offset = image[callOffset + 1] | (image[callOffset + 2] << 8);
        var segment = image[callOffset + 3] | (image[callOffset + 4] << 8);

        var linear = segment * 16 + offset;
        if (linear < 0 || linear >= image.Length)
        {
            throw new InvalidOperationException(
                $"Некорректный адрес {targetSymbol} в far CALL: 0x{segment:X4}:0x{offset:X4}.");
        }

        return linear;
    }

    private static OmfModule GetCrt0Module(OmfLibrary library) =>
        library.Modules.FirstOrDefault(static m =>
            m.DisplayName.Equals("crt0", StringComparison.OrdinalIgnoreCase)
            || m.HeaderName.Contains("CRT0", StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("Модуль crt0 не найден в библиотеке.");
}
