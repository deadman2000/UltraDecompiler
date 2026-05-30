using Common;
using UltraDecompiler.Disassembler;

namespace LibMatching;

/// <summary>
/// Сравнивает тела функций с учётом неразрешённых релокаций библиотеки и линковки EXE.
/// </summary>
/// <remarks>
/// В .LIB код хранится до линковки: near CALL/JMP содержат 0000h, а MOV reg, imm16 —
/// символические смещения (__iob+8). В EXE эти слова уже пропатчены. Поэтому сравнение
/// строится на структуре инструкций, а значения 16-битных слов «маскируются», если
/// FIXUPP модуля библиотеки помечает их как rel16/seg16. Короткие Jcc/JMP сравниваются
/// по disp8 из байтов инструкции, т.к. Operand хранит абсолютную цель.
/// </remarks>
internal static class FunctionBodyComparer
{
    /// <summary>
    /// Сравнивает линейные тела функций: совпадают мнемоники, типы операндов и значения,
    /// кроме слов с FIXUP в модуле библиотеки и near CALL/JMP (разные адреса после линковки).
    /// </summary>
    public static bool AreEquivalent(
        IReadOnlyList<Instruction> imageBody,
        IReadOnlyList<Instruction> libraryBody,
        RelocationTable libraryRelocations)
    {
        // Разная длина — разные функции (или смещение попало в середину инструкции).
        if (imageBody.Count != libraryBody.Count)
        {
            return false;
        }

        for (var i = 0; i < imageBody.Count; i++)
        {
            if (!AreEquivalentInstruction(imageBody[i], libraryBody[i], libraryRelocations))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalentInstruction(
        Instruction image,
        Instruction library,
        RelocationTable libraryRelocations)
    {
        if (image.Mnemonic != library.Mnemonic)
        {
            return false;
        }

        if (image.Prefix != library.Prefix || image.Segment != library.Segment)
        {
            return false;
        }

        return AreEquivalentOperands(image.Operand1, library.Operand1, image, library, libraryRelocations)
            && AreEquivalentOperands(image.Operand2, library.Operand2, image, library, libraryRelocations);
    }

    private static bool AreEquivalentOperands(
        Operand image,
        Operand library,
        Instruction imageInstruction,
        Instruction libraryInstruction,
        RelocationTable libraryRelocations)
    {
        if (image.Type != library.Type)
        {
            return false;
        }

        if (image.Type == OperandType.None)
        {
            return true;
        }

        return image.Type switch
        {
            // Регистры и 8-битные immediate одинаковы в EXE и .LIB — линковка их не меняет.
            OperandType.Register8 or OperandType.Register16 or OperandType.SegmentRegister =>
                image.Value == library.Value,
            OperandType.Immediate8 => image.Value == library.Value,

            // Короткий rel8 (Jcc, LOOP): в Operand — абсолютная цель; сравниваем disp8 из байтов.
            OperandType.Relative8 => AreEquivalentRelative8(
                image,
                library,
                imageInstruction,
                libraryInstruction),

            // 16-битный immediate: либо точное совпадение, либо слово помечено FIXUP в .LIB.
            OperandType.Immediate16 => AreEquivalentImmediate16(
                image,
                library,
                libraryInstruction,
                libraryRelocations),

            // rel16: near CALL/JMP и прочие дальние смещения с FIXUP.
            OperandType.Relative16 => AreEquivalentRelative16(
                library,
                libraryInstruction,
                libraryRelocations),

            // [BP+disp], [SI+disp] — регистровая часть жёсткая; disp может быть символическим.
            OperandType.Memory => image.BaseReg == library.BaseReg
                && image.IndexReg == library.IndexReg
                && AreEquivalentDisplacement(
                    image,
                    library,
                    libraryInstruction,
                    libraryRelocations),
            _ => false,
        };
    }

    private static bool AreEquivalentImmediate16(
        Operand image,
        Operand library,
        Instruction libraryInstruction,
        RelocationTable libraryRelocations)
    {
        if (IsWildcardImmediate(library, libraryInstruction, libraryRelocations))
        {
            return true;
        }

        return (ushort)image.Value == (ushort)library.Value;
    }

    private static bool AreEquivalentRelative8(
        Operand image,
        Operand library,
        Instruction imageInstruction,
        Instruction libraryInstruction)
    {
        var imageDisp = TryGetShortJumpDisplacement(imageInstruction, image);
        var libraryDisp = TryGetShortJumpDisplacement(libraryInstruction, library);
        if (imageDisp is not null && libraryDisp is not null)
        {
            return imageDisp == libraryDisp;
        }

        return image.Value == library.Value;
    }

    /// <summary>Извлекает signed disp8 короткого перехода (Jcc, JMP short, LOOP).</summary>
    private static sbyte? TryGetShortJumpDisplacement(Instruction instruction, Operand relOperand)
    {
        if (relOperand.Type != OperandType.Relative8 || instruction.Bytes.Length < 2)
        {
            return null;
        }

        return (sbyte)instruction.Bytes[^1];
    }

    private static bool AreEquivalentRelative16(
        Operand library,
        Instruction libraryInstruction,
        RelocationTable libraryRelocations)
    {
        // Near CALL/JMP: в EXE уже пропатчен rel16, в .LIB — 0000h + FIXUPP.
        // Значения заведомо разные, поэтому достаточно совпадения мнемоники и типа операнда.
        if (libraryInstruction.Mnemonic is Mnemonic.CALL or Mnemonic.JMP)
        {
            return true;
        }

        if (IsWildcardImmediate(library, libraryInstruction, libraryRelocations))
        {
            return true;
        }

        return false;
    }

    private static bool AreEquivalentDisplacement(
        Operand image,
        Operand library,
        Instruction libraryInstruction,
        RelocationTable libraryRelocations)
    {
        // Аналогично imm16: INC WORD PTR [__cflush] в .LIB vs INC [0104h] в EXE.
        if (IsWildcardImmediate(library, libraryInstruction, libraryRelocations))
        {
            return true;
        }

        return (short)image.Value == (short)library.Value;
    }

    /// <summary>
    /// Определяет, нужно ли игнорировать 16-битное слово операнда библиотеки при сравнении.
    /// </summary>
    private static bool IsWildcardImmediate(
        Operand libraryOperand,
        Instruction libraryInstruction,
        RelocationTable libraryRelocations)
    {
        // Дизассемблер уже подставил имя символа (__iob, __stbuf) в Operand.Relocation.
        if (!string.IsNullOrEmpty(libraryOperand.Relocation))
        {
            return true;
        }

        // Запасной путь: FIXUPP мог не попасть в операнд, но есть в таблице релокаций модуля.
        var wordOffset = FindWordOffset(libraryInstruction, libraryOperand);
        return wordOffset is int offset && libraryRelocations.ContainsLinearAddress(offset);
    }

    /// <summary>
    /// Находит линейное смещение 16-битного слова операнда в кодовом сегменте модуля .LIB.
    /// </summary>
    private static int? FindWordOffset(Instruction instruction, Operand operand)
    {
        if (operand.Type is not (OperandType.Immediate16 or OperandType.Relative16))
        {
            return null;
        }

        var bytes = instruction.Bytes;
        if (bytes.Length < 2)
        {
            return null;
        }

        // Ищем little-endian представление значения операнда внутри байтов инструкции.
        var lo = (byte)(operand.Value & 0xFF);
        var hi = (byte)((operand.Value >> 8) & 0xFF);

        for (var i = 1; i < bytes.Length - 1; i++)
        {
            if (bytes[i] == lo && bytes[i + 1] == hi)
            {
                return instruction.Offset + i;
            }
        }

        // Неразрешённый CALL/JMP в .LIB: E8/E9 00 00 — слово всегда на смещении +1.
        if (bytes[0] is 0xE8 or 0xE9)
        {
            return instruction.Offset + 1;
        }

        // MOV AX..DI, imm16: B8..BF imm16 lo hi — immediate начинается с +1.
        if (bytes[0] is >= 0xB8 and <= 0xBF)
        {
            return instruction.Offset + 1;
        }

        // PUSH imm16: 68 lo hi
        if (bytes[0] == 0x68)
        {
            return instruction.Offset + 1;
        }

        // PUSH [disp16]: FF /6 mod=00 rm=110 (FF 36 disp16) или mod=10 (FF B6 disp16)
        if (bytes[0] == 0xFF && bytes.Length >= 2 && ((bytes[1] >> 3) & 7) == 6)
        {
            var mod = (bytes[1] >> 6) & 3;
            if (mod is 0 or 2)
            {
                return instruction.Offset + 2;
            }
        }

        // Последние два байта — наиболее вероятное расположение immediate в сложных формах.
        return instruction.Offset + bytes.Length - 2;
    }
}
