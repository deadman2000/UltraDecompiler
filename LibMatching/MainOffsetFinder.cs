using Common;
using LibParser.Models;
using UltraDecompiler.Disassembler;

namespace LibMatching;

/// <summary>Определяет смещение <c>_main</c> в образе EXE по вызову из crt0 <c>__astart</c>.</summary>
public static class MainOffsetFinder
{
    private const string MainSymbol = "_main";

    /// <summary>
    /// Находит смещение <c>_main</c>, следуя вызову из crt0 по FIXUPP библиотеки.
    /// </summary>
    /// <param name="astartOffset">Смещение <c>__astart</c> в образе.</param>
    /// <param name="astartModuleCodeOffset">Смещение <c>__astart</c> в кодовом сегменте crt0 (обычно 0).</param>
    public static int FindFromAstart(
        byte[] image,
        RelocationTable imageRelocations,
        OmfLibrary library,
        int astartOffset,
        RegisterState initRegisters,
        int astartModuleCodeOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(imageRelocations);
        ArgumentNullException.ThrowIfNull(library);

        var crt0 = GetCrt0Module(library);
        var mainCallFixup = crt0.Fixups.FirstOrDefault(IsMainCallFixup)
            ?? throw new InvalidOperationException(
                $"В crt0 библиотеки не найден FIXUP для вызова {MainSymbol}.");

        var crt0BaseInImage = astartOffset - astartModuleCodeOffset;
        var callOffsetInImage = crt0BaseInImage + mainCallFixup.SegmentOffset - 1;
        var entryPoint = astartOffset;

        return ResolveMainOffset(
            image,
            imageRelocations,
            callOffsetInImage,
            mainCallFixup,
            initRegisters,
            entryPoint);
    }

    private static bool IsMainCallFixup(OmfFixup fixup)
    {
        if (fixup.Target.Kind != OmfFixupDatumKind.Extdef)
        {
            return false;
        }

        if (!string.Equals(fixup.Target.Name, MainSymbol, StringComparison.Ordinal))
        {
            return false;
        }

        return fixup.LocationType is OmfFixupLocationType.Offset16 or OmfFixupLocationType.Pointer32;
    }

    private static int ResolveMainOffset(
        byte[] image,
        RelocationTable imageRelocations,
        int callOffset,
        OmfFixup mainCallFixup,
        RegisterState initRegisters,
        int entryPoint)
    {
        if (callOffset < 0 || callOffset >= image.Length)
        {
            throw new InvalidOperationException(
                $"Смещение вызова {MainSymbol} 0x{callOffset:X} вне образа.");
        }

        return mainCallFixup.LocationType switch
        {
            OmfFixupLocationType.Offset16 => ResolveNearCallTarget(
                image,
                imageRelocations,
                callOffset,
                initRegisters),
            OmfFixupLocationType.Pointer32 => ResolveFarCallOffset(image, callOffset, entryPoint),
            _ => throw new InvalidOperationException(
                $"Неподдерживаемый тип FIXUP для {MainSymbol}: {mainCallFixup.LocationType}."),
        };
    }

    private static int ResolveNearCallTarget(
        byte[] image,
        RelocationTable imageRelocations,
        int callOffset,
        RegisterState initRegisters)
    {
        if (image[callOffset] != 0xE8)
        {
            throw new InvalidOperationException(
                $"Ожидался near CALL (E8) по смещению 0x{callOffset:X}, получен 0x{image[callOffset]:X2}.");
        }

        var disassembler = new X86Disassembler(image, imageRelocations);
        disassembler.Disassemble(callOffset, initRegisters);

        var callInstruction = disassembler.Instructions.FirstOrDefault(i => i.Offset == callOffset)
            ?? throw new InvalidOperationException(
                $"Не удалось дизасsemblировать CALL {MainSymbol} по 0x{callOffset:X}.");

        var target = callInstruction.GetJumpTarget();
        if (target < 0 || target >= image.Length)
        {
            throw new InvalidOperationException(
                $"Некорректный адрес {MainSymbol}: 0x{target:X}.");
        }

        return target;
    }

    private static int ResolveFarCallOffset(byte[] image, int callOffset, int entryPoint)
    {
        if (image[callOffset] != 0x9A)
        {
            throw new InvalidOperationException(
                $"Ожидался far CALL (9A) по смещению 0x{callOffset:X}, получен 0x{image[callOffset]:X2}.");
        }

        // CALL FAR ptr16:16 — IP по +1, CS по +3 (little-endian).
        var offset = image[callOffset + 1] | (image[callOffset + 2] << 8);
        var segment = image[callOffset + 3] | (image[callOffset + 4] << 8);

        if (offset == 0 && segment == 0)
        {
            return FindUserMainBeforeEntry(image, entryPoint);
        }

        var linear = segment * 16 + offset;
        if (linear <= 0 || linear >= image.Length)
        {
            throw new InvalidOperationException(
                $"Некорректный адрес {MainSymbol} в far CALL: 0x{segment:X4}:0x{offset:X4}.");
        }

        return linear;
    }

    /// <summary>Ищет prologue QuickC <c>push bp; mov bp, sp</c> перед точкой входа crt0.</summary>
    private static int FindUserMainBeforeEntry(byte[] image, int entryPoint)
    {
        for (var offset = 0; offset < entryPoint; offset++)
        {
            if (offset + 2 >= image.Length)
            {
                continue;
            }

            if (image[offset] == 0x55 && image[offset + 1] == 0x8B && image[offset + 2] == 0xEC)
            {
                return offset;
            }
        }

        throw new InvalidOperationException(
            $"Не удалось найти prologue {MainSymbol} (push bp; mov bp, sp) перед точкой входа 0x{entryPoint:X}.");
    }

    private static OmfModule GetCrt0Module(OmfLibrary library) =>
        library.Modules.FirstOrDefault(static m =>
            m.DisplayName.Equals("crt0", StringComparison.OrdinalIgnoreCase)
            || m.HeaderName.Contains("CRT0", StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("Модуль crt0 не найден в библиотеке.");
}
