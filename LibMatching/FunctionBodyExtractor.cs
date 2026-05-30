using Common;
using UltraDecompiler.Disassembler;

namespace LibMatching;

/// <summary>Извлекает линейное тело функции из образа (до первого RET, без переходов по веткам).</summary>
internal static class FunctionBodyExtractor
{
    /// <summary>
    /// Дизассемблирует последовательность инструкций от <paramref name="startOffset"/>
    /// до первого RET/выхода, не заходя в цели переходов и не продолжая после RET.
    /// </summary>
    public static IReadOnlyList<Instruction> Extract(
        byte[] image,
        RelocationTable relocations,
        int startOffset,
        RegisterState initRegisters)
    {
        // Рекурсивный обход CFG (BFS) нужен дизассемблеру для корректного декодирования,
        // но для сопоставления берём только «прямой» путь от точки входа — без веток Jcc/JMP.
        var disassembler = new X86Disassembler(image, relocations);
        disassembler.Disassemble(startOffset, initRegisters);

        // DB-заглушки возникают на невыровненных смещениях; для сравнения тел они не нужны.
        var ordered = disassembler.Instructions
            .Where(static i => i.Mnemonic != Mnemonic.DB)
            .OrderBy(static i => i.Offset)
            .ToList();

        var startIndex = ordered.FindIndex(i => i.Offset == startOffset);
        if (startIndex < 0)
        {
            return [];
        }

        var body = new List<Instruction>();
        for (var i = startIndex; i < ordered.Count; i++)
        {
            var instruction = ordered[i];

            // Разрыв в адресах означает, что дальше идёт другая ветка CFG (цель перехода
            // или код после CALL, если бы мы в него зашли). Линейное тело на этом заканчивается.
            if (body.Count > 0)
            {
                var expectedOffset = body[^1].Offset + body[^1].Size;
                if (instruction.Offset != expectedOffset)
                {
                    break;
                }
            }

            body.Add(instruction);

            // RET / INT 21h с AH=4Ch — естественная граница функции QuickC.
            if (instruction.IsReturn || instruction.IsExit)
            {
                break;
            }
        }

        return body;
    }
}
