using UltraDecompiler.LibMatching;

namespace UltraDecompiler.Decompilation;

/// <summary>Тип long-операции QuickC, восстанавливаемой из ассемблера.</summary>
public enum LongArithmeticKind
{
    Add,
    Sub,
    Mul,
    Div,
    Rem,
    ShiftLeft,
    ShiftRight,
    ShiftedSum,
}

/// <summary>Сайт long-арифметики в теле процедуры.</summary>
/// <param name="Index">Индекс ключевой инструкции в списке.</param>
/// <param name="Kind">Вид операции.</param>
/// <param name="LeftLowOffset">Смещение [BP+n] младшего слова левого операнда.</param>
/// <param name="RightLowOffset">Смещение [BP+n] младшего слова правого операнда.</param>
/// <param name="DestLowOffset">Смещение [BP+n] младшего слова результата (&lt; 0 — локаль).</param>
/// <param name="ShiftCount">Количество бит для сдвига (для ShiftLeft/ShiftRight).</param>
/// <param name="SecondShiftCount">Второй сдвиг для <see cref="LongArithmeticKind.ShiftedSum"/>.</param>
/// <param name="CalleeName">Имя helper'а для CALL-сайтов.</param>
/// <param name="EmitAssignment">Нужно ли генерировать присваивание локали (ложь для временных).</param>
public sealed record LongArithmeticSite(
    int Index,
    LongArithmeticKind Kind,
    int LeftLowOffset,
    int RightLowOffset,
    int DestLowOffset,
    int ShiftCount = 0,
    int SecondShiftCount = 0,
    string CalleeName = "",
    bool EmitAssignment = true);

/// <summary>
/// Распознавание long-арифметики рантайма QuickC (<c>__aNlmul</c>, <c>__aNldiv</c>, …) и inline ADD/ADC.
/// </summary>
internal static class LongRuntimeHelpers
{
    private static readonly HashSet<string> ShiftLeftNames = new(StringComparer.Ordinal)
    {
        "_aNlshl",
        "__aNlshl",
        "aNlshl",
    };

    private static readonly HashSet<string> ShiftRightNames = new(StringComparer.Ordinal)
    {
        "_aNlshr",
        "__aNlshr",
        "aNlshr",
    };

    private static readonly HashSet<string> MulNames = new(StringComparer.Ordinal)
    {
        "_aNlmul",
        "__aNlmul",
        "aNlmul",
    };

    private static readonly HashSet<string> DivNames = new(StringComparer.Ordinal)
    {
        "_aNldiv",
        "__aNldiv",
        "aNldiv",
    };

    private static readonly HashSet<string> RemNames = new(StringComparer.Ordinal)
    {
        "_aNlrem",
        "__aNlrem",
        "aNlrem",
    };

    /// <summary>Является ли имя функцией сдвига <c>long</c> влево.</summary>
    public static bool IsLongShiftLeft(string name) => ShiftLeftNames.Contains(name);

    /// <summary>Является ли имя функцией сдвига <c>long</c> вправо.</summary>
    public static bool IsLongShiftRight(string name) => ShiftRightNames.Contains(name);

    /// <summary>Является ли имя любым long-сдвигом рантайма.</summary>
    public static bool IsLongShiftHelper(string name) =>
        IsLongShiftLeft(name) || IsLongShiftRight(name);

    /// <summary>Классифицирует имя helper'а long-арифметики.</summary>
    public static LongArithmeticKind? ClassifyHelperName(string name)
    {
        if (IsLongShiftLeft(name))
        {
            return LongArithmeticKind.ShiftLeft;
        }

        if (IsLongShiftRight(name))
        {
            return LongArithmeticKind.ShiftRight;
        }

        if (MulNames.Contains(name))
        {
            return LongArithmeticKind.Mul;
        }

        if (DivNames.Contains(name))
        {
            return LongArithmeticKind.Div;
        }

        if (RemNames.Contains(name))
        {
            return LongArithmeticKind.Rem;
        }

        return null;
    }

    /// <summary>Есть ли в процедуре признаки long-арифметики.</summary>
    public static bool UsesLongArithmetic(IReadOnlyList<Instruction> instructions, ProcedureStorage storage) =>
        CollectArithmeticSites(instructions, storage).Count > 0;

    /// <summary>Собирает все сайты long-арифметики в порядке исполнения.</summary>
    public static List<LongArithmeticSite> CollectArithmeticSites(
        IReadOnlyList<Instruction> instructions,
        ProcedureStorage storage)
    {
        var sites = new List<LongArithmeticSite>();
        var handledInline = new HashSet<int>();

        for (var i = 0; i < instructions.Count; i++)
        {
            if (TryParseInlineLongBinary(instructions, i, out var inlineKind, out var left, out var right, out var dest)
                && handledInline.Add(i))
            {
                sites.Add(new LongArithmeticSite(i, inlineKind, left, right, dest));
                continue;
            }

            if (instructions[i].Mnemonic != Mnemonic.CALL)
            {
                continue;
            }

            if (TryParseRemCall(instructions, i, storage, out var remSite))
            {
                sites.Add(remSite);
                continue;
            }

            if (TryParseMulDivCall(instructions, i, storage, out var mulDivSite))
            {
                sites.Add(mulDivSite);
                continue;
            }

            if (TryParseShiftCall(instructions, i, storage, out var shiftSite))
            {
                sites.Add(shiftSite);
            }
        }

        MergeShiftedSumSites(instructions, sites);
        return sites;
    }

    /// <summary>Процедура без ветвлений, только long-присваивания и return.</summary>
    public static bool IsStraightLineLongProcedure(
        IReadOnlyList<Instruction> instructions,
        IReadOnlyList<LongArithmeticSite> sites)
    {
        if (sites.Count == 0)
        {
            return false;
        }

        foreach (var instr in instructions)
        {
            if (instr.Mnemonic is Mnemonic.JE or Mnemonic.JNE
                or Mnemonic.JL or Mnemonic.JLE or Mnemonic.JG or Mnemonic.JGE
                or Mnemonic.JA or Mnemonic.JAE or Mnemonic.JB or Mnemonic.JBE
                or Mnemonic.LOOP or Mnemonic.LOOPNE or Mnemonic.LOOPE)
            {
                return false;
            }
        }

        return CollectLongReturnOperands(instructions).Count > 0;
    }

    /// <summary>
    /// Возвращает смещения [BP+n] long-локалей, суммируемых в return (ADD/ADC-цепочка перед эпилогом).
    /// </summary>
    public static List<int> CollectLongReturnOperands(IReadOnlyList<Instruction> instructions)
    {
        var chainStart = FindLongReturnChainStart(instructions);
        if (chainStart < 0)
        {
            return [];
        }

        var operands = new List<int>();
        if (!TryParseBpMovLoad(instructions[chainStart], GpRegister16.AX, out var firstLow))
        {
            return [];
        }

        operands.Add(firstLow);
        var index = chainStart + 2;

        while (index + 1 < instructions.Count)
        {
            if (instructions[index].Mnemonic != Mnemonic.ADD
                || !TryParseBpLoad(instructions[index], GpRegister16.AX, out var addLow)
                || instructions[index + 1].Mnemonic != Mnemonic.ADC)
            {
                break;
            }

            operands.Add(addLow);
            index += 2;
        }

        return operands;
    }

    /// <summary>Разрешает имя callee для near CALL (релокация или смещение в образе).</summary>
    public static bool TryResolveCallTarget(
        Instruction call,
        ProcedureStorage storage,
        out string calleeName)
    {
        calleeName = string.Empty;
        if (call.Mnemonic != Mnemonic.CALL)
        {
            return false;
        }

        var op = call.Operand1.IsSet ? call.Operand1 : call.Operand2;
        if (op.Relocation is { Length: > 0 } symbol)
        {
            calleeName = LinkerSymbolNames.ToCName(symbol);
            return true;
        }

        if (call.JumpTarget < 0)
        {
            return false;
        }

        if (storage.TryGet(call.JumpTarget, out var procedure) && procedure is not null)
        {
            calleeName = procedure.Name;
            return true;
        }

        calleeName = $"sub_{call.JumpTarget:X4}";
        return true;
    }

    /// <summary>
    /// Эвристика long-сдвига по подготовке регистров перед CALL
    /// (<c>mov ax,[bp+n]; mov dx,[bp+n+2]; mov cx,imm; call</c>).
    /// </summary>
    public static bool LooksLikeLongShiftCallSite(
        IReadOnlyList<Instruction> instructions,
        int callIndex,
        ProcedureStorage storage,
        out int shiftCount,
        out int sourceLowOffset,
        out int sourceHighOffset,
        out bool isLeft)
    {
        shiftCount = 0;
        sourceLowOffset = 0;
        sourceHighOffset = 0;
        isLeft = false;

        return TryParseShiftCall(instructions, callIndex, storage, out var site)
            && (shiftCount = site.ShiftCount) >= 0
            && (sourceLowOffset = site.LeftLowOffset) >= 0
            && (sourceHighOffset = site.LeftLowOffset + 2) >= 0
            && (isLeft = site.Kind == LongArithmeticKind.ShiftLeft);
    }

    /// <summary>Ищет запись пары слов long после CALL (<c>mov [bp+n],ax; mov [bp+n+2],dx</c>).</summary>
    public static bool TryFindLongPairStoreAfterCall(
        IReadOnlyList<Instruction> instructions,
        int callIndex,
        out int destLowOffset,
        out int destHighOffset)
    {
        destLowOffset = 0;
        destHighOffset = 0;

        for (var i = callIndex + 1; i < instructions.Count && i <= callIndex + 4; i++)
        {
            if (!TryParseBpStore(instructions[i], GpRegister16.AX, out destLowOffset))
            {
                continue;
            }

            for (var j = i + 1; j < instructions.Count && j <= i + 3; j++)
            {
                if (TryParseBpStore(instructions[j], GpRegister16.DX, out destHighOffset)
                    && destHighOffset == destLowOffset + 2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void MergeShiftedSumSites(IReadOnlyList<Instruction> instructions, List<LongArithmeticSite> sites)
    {
        var shifts = sites
            .Where(static s => s.Kind is LongArithmeticKind.ShiftLeft or LongArithmeticKind.ShiftRight)
            .OrderBy(static s => s.Index)
            .ToList();

        if (shifts.Count < 2)
        {
            return;
        }

        var first = shifts[0];
        var second = shifts[1];
        if (!TryFindShiftedSumStore(instructions, second.Index, first.DestLowOffset, out var destLow))
        {
            return;
        }

        for (var i = 0; i < sites.Count; i++)
        {
            if (sites[i].DestLowOffset == first.DestLowOffset && sites[i].Kind is LongArithmeticKind.ShiftLeft)
            {
                sites[i] = sites[i] with { EmitAssignment = false };
            }
        }

        sites.Add(new LongArithmeticSite(
            second.Index,
            LongArithmeticKind.ShiftedSum,
            first.LeftLowOffset,
            second.LeftLowOffset,
            destLow,
            first.ShiftCount,
            second.ShiftCount,
            EmitAssignment: true));

        sites.Sort(static (a, b) => a.Index.CompareTo(b.Index));
    }

    private static bool TryFindShiftedSumStore(
        IReadOnlyList<Instruction> instructions,
        int shiftCallIndex,
        int tempLowOffset,
        out int destLowOffset)
    {
        destLowOffset = 0;

        for (var i = shiftCallIndex + 1; i < instructions.Count && i <= shiftCallIndex + 10; i++)
        {
            if (!TryParseBpLoad(instructions[i], GpRegister16.CX, out var leftLow)
                || leftLow != tempLowOffset)
            {
                continue;
            }

            if (i + 5 >= instructions.Count
                || !TryParseBpLoad(instructions[i + 1], GpRegister16.BX, out var leftHigh)
                || leftHigh != tempLowOffset + 2)
            {
                continue;
            }

            if (instructions[i + 2].Mnemonic != Mnemonic.ADD
                || !TargetIsRegister16(instructions[i + 2].Operand1, GpRegister16.CX)
                || !TargetIsRegister16(instructions[i + 2].Operand2, GpRegister16.AX))
            {
                continue;
            }

            if (instructions[i + 3].Mnemonic != Mnemonic.ADC
                || !TargetIsRegister16(instructions[i + 3].Operand1, GpRegister16.BX)
                || !TargetIsRegister16(instructions[i + 3].Operand2, GpRegister16.DX))
            {
                continue;
            }

            if (!TryParseBpStore(instructions[i + 4], GpRegister16.CX, out destLowOffset)
                || !TryParseBpStore(instructions[i + 5], GpRegister16.BX, out var destHigh)
                || destHigh != destLowOffset + 2)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryParseShiftCall(
        IReadOnlyList<Instruction> instructions,
        int callIndex,
        ProcedureStorage storage,
        out LongArithmeticSite site)
    {
        site = null!;

        if (!TryFindShiftCount(instructions, callIndex, out var shiftCount))
        {
            return false;
        }

        if (!TryFindLongPairLoad(instructions, callIndex, out var sourceLow, out _))
        {
            return false;
        }

        TryFindLongPairStoreAfterCall(instructions, callIndex, out var destLow, out _);

        TryResolveCallTarget(instructions[callIndex], storage, out var name);
        var kind = LongArithmeticKind.ShiftLeft;
        if (!string.IsNullOrEmpty(name))
        {
            var classified = ClassifyHelperName(name);
            if (classified is LongArithmeticKind.ShiftLeft or LongArithmeticKind.ShiftRight)
            {
                kind = classified.Value;
            }
            else if (IsLongShiftRight(name))
            {
                kind = LongArithmeticKind.ShiftRight;
            }
        }
        else if (CountPreviousShiftCalls(instructions, callIndex) % 2 == 1)
        {
            kind = LongArithmeticKind.ShiftRight;
        }

        site = new LongArithmeticSite(
            callIndex,
            kind,
            sourceLow,
            0,
            destLow,
            shiftCount,
            CalleeName: name);

        return true;
    }

    private static int CountPreviousShiftCalls(IReadOnlyList<Instruction> instructions, int callIndex)
    {
        var count = 0;
        for (var i = 0; i < callIndex; i++)
        {
            if (instructions[i].Mnemonic == Mnemonic.CALL
                && TryFindShiftCount(instructions, i, out _)
                && TryFindLongPairLoad(instructions, i, out _, out _))
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryParseMulDivCall(
        IReadOnlyList<Instruction> instructions,
        int callIndex,
        ProcedureStorage storage,
        out LongArithmeticSite site)
    {
        site = null!;

        if (!TryFindMulDivOperands(instructions, callIndex, out var leftLow, out var rightLow))
        {
            return false;
        }

        if (!TryFindFourPushesBeforeCall(instructions, callIndex))
        {
            return false;
        }

        TryFindLongPairStoreAfterCall(instructions, callIndex, out var destLow, out _);

        TryResolveCallTarget(instructions[callIndex], storage, out var calleeName);
        var kind = LongArithmeticKind.Mul;
        if (!string.IsNullOrEmpty(calleeName))
        {
            var classified = ClassifyHelperName(calleeName);
            if (classified is LongArithmeticKind.Mul or LongArithmeticKind.Div)
            {
                kind = classified.Value;
            }
            else if (CountPreviousMulDivCalls(instructions, callIndex) % 2 == 1)
            {
                kind = LongArithmeticKind.Div;
            }
        }

        site = new LongArithmeticSite(callIndex, kind, leftLow, rightLow, destLow, CalleeName: calleeName);
        return true;
    }

    private static int CountPreviousMulDivCalls(IReadOnlyList<Instruction> instructions, int callIndex)
    {
        var count = 0;
        for (var i = 0; i < callIndex; i++)
        {
            if (instructions[i].Mnemonic == Mnemonic.CALL
                && TryFindMulDivOperands(instructions, i, out _, out _)
                && TryFindFourPushesBeforeCall(instructions, i))
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryParseRemCall(
        IReadOnlyList<Instruction> instructions,
        int callIndex,
        ProcedureStorage storage,
        out LongArithmeticSite site)
    {
        site = null!;

        if (!TryFindRemOperands(instructions, callIndex, out var leftLow, out var rightLow))
        {
            return false;
        }

        TryFindLongPairStoreAfterCall(instructions, callIndex, out var destLow, out _);

        TryResolveCallTarget(instructions[callIndex], storage, out var calleeName);

        site = new LongArithmeticSite(
            callIndex,
            LongArithmeticKind.Rem,
            leftLow,
            rightLow,
            destLow,
            CalleeName: calleeName);

        return true;
    }

    private static bool TryParseInlineLongBinary(
        IReadOnlyList<Instruction> instructions,
        int startIndex,
        out LongArithmeticKind kind,
        out int leftLowOffset,
        out int rightLowOffset,
        out int destLowOffset)
    {
        kind = LongArithmeticKind.Add;
        leftLowOffset = 0;
        rightLowOffset = 0;
        destLowOffset = 0;

        if (startIndex + 5 >= instructions.Count)
        {
            return false;
        }

        if (!TryParseBpLoad(instructions[startIndex], GpRegister16.AX, out leftLowOffset)
            || !TryParseBpLoad(instructions[startIndex + 1], GpRegister16.DX, out var leftHigh)
            || leftHigh != leftLowOffset + 2)
        {
            return false;
        }

        var arith = instructions[startIndex + 2].Mnemonic;
        var carry = instructions[startIndex + 3].Mnemonic;
        if (arith == Mnemonic.ADD && carry == Mnemonic.ADC)
        {
            kind = LongArithmeticKind.Add;
        }
        else if (arith == Mnemonic.SUB && carry == Mnemonic.SBB)
        {
            kind = LongArithmeticKind.Sub;
        }
        else
        {
            return false;
        }

        if (!TryParseBpLoad(instructions[startIndex + 2], GpRegister16.AX, out rightLowOffset)
            || !TryParseBpLoad(instructions[startIndex + 3], GpRegister16.DX, out var rightHigh)
            || rightHigh != rightLowOffset + 2)
        {
            return false;
        }

        if (!TryParseBpStore(instructions[startIndex + 4], GpRegister16.AX, out destLowOffset)
            || !TryParseBpStore(instructions[startIndex + 5], GpRegister16.DX, out var destHigh)
            || destHigh != destLowOffset + 2)
        {
            return false;
        }

        return destLowOffset < 0;
    }

    private static bool TryFindMulDivOperands(
        IReadOnlyList<Instruction> instructions,
        int callIndex,
        out int leftLow,
        out int rightLow)
    {
        leftLow = 0;
        rightLow = 0;

        int? axOffset = null;
        int? cxOffset = null;

        for (var i = callIndex - 1; i >= 0 && i >= callIndex - 10; i--)
        {
            if (TryParseBpLoad(instructions[i], GpRegister16.AX, out var axDisp))
            {
                axOffset ??= axDisp;
            }
            else if (TryParseBpLoad(instructions[i], GpRegister16.CX, out var cxDisp))
            {
                cxOffset ??= cxDisp;
            }
        }

        if (axOffset is not int ro || cxOffset is not int lo)
        {
            return false;
        }

        rightLow = ro;
        leftLow = lo;
        return true;
    }

    private static bool TryFindRemOperands(
        IReadOnlyList<Instruction> instructions,
        int callIndex,
        out int leftLow,
        out int rightLow)
    {
        leftLow = 0;
        rightLow = 0;

        var pushes = new List<int>();
        for (var i = callIndex - 1; i >= 0 && i >= callIndex - 6; i--)
        {
            if (instructions[i].Mnemonic != Mnemonic.PUSH
                || instructions[i].Operand1.Type != OperandType.Memory
                || instructions[i].Operand1.BaseReg != AddressRegister.BP)
            {
                continue;
            }

            pushes.Add(instructions[i].Operand1.Value);
            if (pushes.Count == 4)
            {
                break;
            }
        }

        if (pushes.Count != 4)
        {
            return false;
        }

        leftLow = pushes[0];
        rightLow = pushes[2];
        return pushes[1] == leftLow + 2 && pushes[3] == rightLow + 2;
    }

    private static bool TryFindFourPushesBeforeCall(IReadOnlyList<Instruction> instructions, int callIndex)
    {
        var pushCount = 0;
        for (var i = callIndex - 1; i >= 0 && i >= callIndex - 8; i--)
        {
            if (instructions[i].Mnemonic == Mnemonic.PUSH
                && instructions[i].Operand1.Type != OperandType.Memory)
            {
                pushCount++;
                if (pushCount == 4)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int FindLongReturnChainStart(IReadOnlyList<Instruction> instructions)
    {
        for (var i = instructions.Count - 1; i >= 0; i--)
        {
            if (instructions[i].Mnemonic is Mnemonic.JMP or Mnemonic.RET)
            {
                for (var j = i - 1; j >= 0 && j >= i - 20; j--)
                {
                    if (TryParseBpMovLoad(instructions[j], GpRegister16.AX, out var low)
                        && low < 0
                        && j + 1 < instructions.Count
                        && TryParseBpMovLoad(instructions[j + 1], GpRegister16.DX, out var high)
                        && high == low + 2)
                    {
                        return j;
                    }
                }
            }
        }

        return -1;
    }

    private static bool TryFindShiftCount(IReadOnlyList<Instruction> instructions, int callIndex, out int count)
    {
        count = 0;
        for (var i = callIndex - 1; i >= 0 && i >= callIndex - 6; i--)
        {
            var instr = instructions[i];
            if (instr.Mnemonic == Mnemonic.MOV
                && TargetIsRegister16(instr.Operand1, GpRegister16.CX)
                && instr.Operand2.Type == OperandType.Immediate16)
            {
                count = instr.Operand2.Value;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindLongPairLoad(
        IReadOnlyList<Instruction> instructions,
        int callIndex,
        out int lowOffset,
        out int highOffset)
    {
        lowOffset = 0;
        highOffset = 0;

        int? axOffset = null;
        int? dxOffset = null;

        for (var i = callIndex - 1; i >= 0 && i >= callIndex - 8; i--)
        {
            if (TryParseBpLoad(instructions[i], GpRegister16.AX, out var axDisp))
            {
                axOffset ??= axDisp;
            }
            else if (TryParseBpLoad(instructions[i], GpRegister16.DX, out var dxDisp))
            {
                dxOffset ??= dxDisp;
            }
        }

        if (axOffset is not int lo || dxOffset is not int hi || hi != lo + 2)
        {
            return false;
        }

        lowOffset = lo;
        highOffset = hi;
        return true;
    }

    private static bool TryParseBpMovLoad(
        Instruction instr,
        GpRegister16 dstReg,
        out int bpDisp)
    {
        bpDisp = 0;
        if (instr.Mnemonic != Mnemonic.MOV
            || !TargetIsRegister16(instr.Operand1, dstReg)
            || instr.Operand2.Type != OperandType.Memory
            || instr.Operand2.BaseReg != AddressRegister.BP
            || instr.Operand2.IndexReg != AddressRegister.None)
        {
            return false;
        }

        bpDisp = instr.Operand2.Value;
        return true;
    }

    private static bool TryParseBpLoad(
        Instruction instr,
        GpRegister16 dstReg,
        out int bpDisp)
    {
        bpDisp = 0;
        if (instr.Mnemonic is not (Mnemonic.MOV or Mnemonic.ADD or Mnemonic.SUB or Mnemonic.ADC or Mnemonic.SBB))
        {
            return false;
        }

        if (!TargetIsRegister16(instr.Operand1, dstReg)
            || instr.Operand2.Type != OperandType.Memory
            || instr.Operand2.BaseReg != AddressRegister.BP
            || instr.Operand2.IndexReg != AddressRegister.None)
        {
            return false;
        }

        bpDisp = instr.Operand2.Value;
        return true;
    }

    private static bool TryParseBpStore(Instruction instr, GpRegister16 srcReg, out int bpDisp)
    {
        bpDisp = 0;
        if (instr.Mnemonic != Mnemonic.MOV
            || !TargetIsRegister16(instr.Operand2, srcReg)
            || instr.Operand1.Type != OperandType.Memory
            || instr.Operand1.BaseReg != AddressRegister.BP
            || instr.Operand1.IndexReg != AddressRegister.None)
        {
            return false;
        }

        bpDisp = instr.Operand1.Value;
        return true;
    }

    private static bool TargetIsRegister16(Operand operand, GpRegister16 register) =>
        operand.Type == OperandType.Register16 && operand.AsGpRegister16() == register;
}
