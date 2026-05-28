namespace UltraDecompiler.Disassembler;

public partial class Instruction
{
    public RegisterState ApplyRegisters(RegisterState state)
    {
        Registers = ModifyRegisters(state);
        return Registers;
    }

    private RegisterState ModifyRegisters(RegisterState state)
        => Mnemonic switch
        {
            Mnemonic.MOV => ModifyRegistersMov(state),
            Mnemonic.ADD => ModifyRegistersAdd(state),
            Mnemonic.ADC => ModifyRegistersAdc(state),
            Mnemonic.SUB => ModifyRegistersSub(state),
            Mnemonic.SBB => ModifyRegistersSbb(state),
            Mnemonic.AND => ModifyRegistersAnd(state),
            Mnemonic.OR => ModifyRegistersOr(state),
            Mnemonic.XOR => ModifyRegistersXor(state),
            Mnemonic.INC => ModifyRegistersInc(state),
            Mnemonic.DEC => ModifyRegistersDec(state),
            Mnemonic.NOT => ModifyRegistersNot(state),
            Mnemonic.NEG => ModifyRegistersNeg(state),
            Mnemonic.XCHG => ModifyRegistersXchg(state),
            Mnemonic.CBW => ModifyRegistersCbw(state),
            Mnemonic.CWD => ModifyRegistersCwd(state),
            Mnemonic.PUSH => ModifyRegistersPush(state),
            Mnemonic.POP => ModifyRegistersPop(state),

            // Строковые инструкции
            Mnemonic.MOVSB => ModifyRegistersString(state, size: 1, updatesCx: false),
            Mnemonic.MOVSW => ModifyRegistersString(state, size: 2, updatesCx: false),
            Mnemonic.CMPSB => ModifyRegistersString(state, size: 1, updatesCx: false),
            Mnemonic.CMPSW => ModifyRegistersString(state, size: 2, updatesCx: false),
            Mnemonic.SCASB => ModifyRegistersString(state, size: 1, updatesCx: false),
            Mnemonic.SCASW => ModifyRegistersString(state, size: 2, updatesCx: false),
            Mnemonic.LODSB => ModifyRegistersString(state, size: 1, updatesCx: false),
            Mnemonic.LODSW => ModifyRegistersString(state, size: 2, updatesCx: false),
            Mnemonic.STOSB => ModifyRegistersString(state, size: 1, updatesCx: false),
            Mnemonic.STOSW => ModifyRegistersString(state, size: 2, updatesCx: false),

            // Флаги направления (влияют на последующие строковые инструкции)
            Mnemonic.CLD => state with { DF = false },
            Mnemonic.STD => state with { DF = true },

            Mnemonic.ENTER => ModifyRegistersEnter(state),
            Mnemonic.ROL => ModifyRegistersRotate(state),
            Mnemonic.ROR => ModifyRegistersRotate(state),

            // TODO: MUL, IMUL, DIV, IDIV, RCL/RCR, DAA, DAS, AAA, AAS, AAM, AAD, LEA, PUSHF/POPF и др.
            _ => state,
        };

    private RegisterState ModifyRegistersMov(RegisterState state)
    {
        // TODO остальные кейсы
        if (Operand1.Type == OperandType.Register8 && Operand2.Type == OperandType.Immediate8)
        {
            return Operand1.Value switch
            {
                0 => state with { AL = (byte)Operand2.Value },
                1 => state with { CL = (byte)Operand2.Value },
                2 => state with { DL = (byte)Operand2.Value },
                3 => state with { BL = (byte)Operand2.Value },
                4 => state with { AH = (byte)Operand2.Value },
                5 => state with { CH = (byte)Operand2.Value },
                6 => state with { DH = (byte)Operand2.Value },
                7 => state with { BH = (byte)Operand2.Value },
                _ => state
            };
        }
        if (Operand1.Type == OperandType.Register16 && Operand2.Type == OperandType.Immediate16)
        {
            ushort val = (ushort)Operand2.Value;
            byte low = (byte)val;
            byte high = (byte)(val >> 8);
            return Operand1.Value switch
            {
                0 => state with { AL = low, AH = high }, // AX
                1 => state with { CL = low, CH = high }, // CX
                2 => state with { DL = low, DH = high }, // DX
                3 => state with { BL = low, BH = high }, // BX
                4 => state with { SP = val }, // SP
                5 => state with { BP = val }, // BP
                6 => state with { SI = val }, // SI
                7 => state with { DI = val }, // DI
                _ => state
            };
        }

        // Поддержка MOV reg, reg (копирование известных значений)
        if (Operand1.Type == OperandType.Register8 && Operand2.Type == OperandType.Register8)
        {
            byte? srcVal = Operand2.Value switch
            {
                0 => state.AL,
                1 => state.CL,
                2 => state.DL,
                3 => state.BL,
                4 => state.AH,
                5 => state.CH,
                6 => state.DH,
                7 => state.BH,
                _ => null
            };

            return Operand1.Value switch
            {
                0 => state with { AL = srcVal },
                1 => state with { CL = srcVal },
                2 => state with { DL = srcVal },
                3 => state with { BL = srcVal },
                4 => state with { AH = srcVal },
                5 => state with { CH = srcVal },
                6 => state with { DH = srcVal },
                7 => state with { BH = srcVal },
                _ => state
            };
        }

        if (Operand1.Type == OperandType.Register16 && Operand2.Type == OperandType.Register16)
        {
            ushort? srcVal = Operand2.Value switch
            {
                0 => state.AX,
                1 => state.CX,
                2 => state.DX,
                3 => state.BX,
                4 => state.SP,
                5 => state.BP,
                6 => state.SI,
                7 => state.DI,
                _ => null
            };
            if (srcVal.HasValue)
            {
                return Operand1.Value switch
                {
                    0 => state with { AL = (byte)srcVal.Value, AH = (byte)(srcVal.Value >> 8) },
                    1 => state with { CL = (byte)srcVal.Value, CH = (byte)(srcVal.Value >> 8) },
                    2 => state with { DL = (byte)srcVal.Value, DH = (byte)(srcVal.Value >> 8) },
                    3 => state with { BL = (byte)srcVal.Value, BH = (byte)(srcVal.Value >> 8) },
                    4 => state with { SP = srcVal.Value },
                    5 => state with { BP = srcVal.Value },
                    6 => state with { SI = srcVal.Value },
                    7 => state with { DI = srcVal.Value },
                    _ => state
                };
            }
            else
            {
                // При присвоении неизвестного значения, результат становится неизвестным
                return Operand1.Value switch
                {
                    0 => state with { AL = null, AH = null },
                    1 => state with { CL = null, CH = null },
                    2 => state with { DL = null, DH = null },
                    3 => state with { BL = null, BH = null },
                    4 => state with { SP = null },
                    5 => state with { BP = null },
                    6 => state with { SI = null },
                    7 => state with { DI = null },
                    _ => state
                };
            }
        }

        // Поддержка сегментных регистров (ES=0, CS=1, SS=2, DS=3)
        if (Operand1.Type == OperandType.SegmentRegister && Operand2.Type == OperandType.Register16)
        {
            ushort? srcVal = GetReg16(state, Operand2.Value);
            return SetSreg(state, Operand1.Value, srcVal);
        }
        if (Operand1.Type == OperandType.Register16 && Operand2.Type == OperandType.SegmentRegister)
        {
            ushort? srcVal = GetSreg(state, Operand2.Value);
            return SetReg16(state, Operand1.Value, srcVal);
        }
        if (Operand1.Type == OperandType.SegmentRegister && Operand2.Type == OperandType.Immediate16)
        {
            return SetSreg(state, Operand1.Value, (ushort)Operand2.Value);
        }

        return state;
    }

    private byte? GetReg8(RegisterState state, int idx) => idx switch
    {
        0 => state.AL,
        1 => state.CL,
        2 => state.DL,
        3 => state.BL,
        4 => state.AH,
        5 => state.CH,
        6 => state.DH,
        7 => state.BH,
        _ => null
    };

    private ushort? GetReg16(RegisterState state, int idx) => idx switch
    {
        0 => state.AX,
        1 => state.CX,
        2 => state.DX,
        3 => state.BX,
        4 => state.SP,
        5 => state.BP,
        6 => state.SI,
        7 => state.DI,
        _ => null
    };

    private RegisterState SetReg8(RegisterState state, int idx, byte? val) => idx switch
    {
        0 => state with { AL = val },
        1 => state with { CL = val },
        2 => state with { DL = val },
        3 => state with { BL = val },
        4 => state with { AH = val },
        5 => state with { CH = val },
        6 => state with { DH = val },
        7 => state with { BH = val },
        _ => state
    };

    private RegisterState SetReg16(RegisterState state, int idx, ushort? val)
    {
        if (!val.HasValue)
        {
            return idx switch
            {
                0 => state with { AL = null, AH = null },
                1 => state with { CL = null, CH = null },
                2 => state with { DL = null, DH = null },
                3 => state with { BL = null, BH = null },
                4 => state with { SP = null },
                5 => state with { BP = null },
                6 => state with { SI = null },
                7 => state with { DI = null },
                _ => state
            };
        }
        byte lo = (byte)val.Value;
        byte hi = (byte)(val.Value >> 8);
        return idx switch
        {
            0 => state with { AL = lo, AH = hi },
            1 => state with { CL = lo, CH = hi },
            2 => state with { DL = lo, DH = hi },
            3 => state with { BL = lo, BH = hi },
            4 => state with { SP = val.Value },
            5 => state with { BP = val.Value },
            6 => state with { SI = val.Value },
            7 => state with { DI = val.Value },
            _ => state
        };
    }

    private ushort? GetSreg(RegisterState state, int idx) => idx switch
    {
        0 => state.ES,
        1 => state.CS,
        2 => state.SS,
        3 => state.DS,
        _ => null
    };

    private RegisterState SetSreg(RegisterState state, int idx, ushort? val) => idx switch
    {
        0 => state with { ES = val },
        1 => state with { CS = val },
        2 => state with { SS = val },
        3 => state with { DS = val },
        _ => state
    };

    private RegisterState ModifyBinary(RegisterState state, Func<byte, byte, byte> op8, Func<ushort, ushort, ushort> op16)
    {
        if (Operand1.Type == OperandType.Register8)
        {
            byte? dst = GetReg8(state, Operand1.Value);
            byte? src = Operand2.Type == OperandType.Register8 ? GetReg8(state, Operand2.Value) :
                        Operand2.Type == OperandType.Immediate8 ? (byte?)Operand2.Value : null;
            if (dst.HasValue && src.HasValue)
                return SetReg8(state, Operand1.Value, op8(dst.Value, src.Value));
            return SetReg8(state, Operand1.Value, null);
        }
        if (Operand1.Type == OperandType.Register16)
        {
            ushort? dst = GetReg16(state, Operand1.Value);
            ushort? src = Operand2.Type == OperandType.Register16 ? GetReg16(state, Operand2.Value) :
                        Operand2.Type == OperandType.Immediate16 ? (ushort?)Operand2.Value : null;
            if (dst.HasValue && src.HasValue)
                return SetReg16(state, Operand1.Value, op16(dst.Value, src.Value));
            return SetReg16(state, Operand1.Value, null);
        }
        return state;
    }

    private RegisterState ModifyRegistersAdd(RegisterState state) => ModifyBinary(state, (a, b) => (byte)(a + b), (a, b) => (ushort)(a + b));
    private RegisterState ModifyRegistersAdc(RegisterState state) => ModifyBinary(state, (a, b) => (byte)(a + b), (a, b) => (ushort)(a + b)); // approx without CF
    private RegisterState ModifyRegistersSub(RegisterState state) => ModifyBinary(state, (a, b) => (byte)(a - b), (a, b) => (ushort)(a - b));
    private RegisterState ModifyRegistersSbb(RegisterState state) => ModifyBinary(state, (a, b) => (byte)(a - b), (a, b) => (ushort)(a - b)); // approx
    private RegisterState ModifyRegistersAnd(RegisterState state) => ModifyBinary(state, (a, b) => (byte)(a & b), (a, b) => (ushort)(a & b));
    private RegisterState ModifyRegistersOr(RegisterState state) => ModifyBinary(state, (a, b) => (byte)(a | b), (a, b) => (ushort)(a | b));
    private RegisterState ModifyRegistersXor(RegisterState state) => ModifyBinary(state, (a, b) => (byte)(a ^ b), (a, b) => (ushort)(a ^ b));

    private RegisterState ModifyRegistersInc(RegisterState state)
    {
        if (Operand1.Type == OperandType.Register8)
        {
            byte? v = GetReg8(state, Operand1.Value);
            return SetReg8(state, Operand1.Value, v.HasValue ? (byte)(v.Value + 1) : null);
        }
        if (Operand1.Type == OperandType.Register16)
        {
            ushort? v = GetReg16(state, Operand1.Value);
            return SetReg16(state, Operand1.Value, v.HasValue ? (ushort)(v.Value + 1) : null);
        }
        return state;
    }

    private RegisterState ModifyRegistersDec(RegisterState state)
    {
        if (Operand1.Type == OperandType.Register8)
        {
            byte? v = GetReg8(state, Operand1.Value);
            return SetReg8(state, Operand1.Value, v.HasValue ? (byte)(v.Value - 1) : null);
        }
        if (Operand1.Type == OperandType.Register16)
        {
            ushort? v = GetReg16(state, Operand1.Value);
            return SetReg16(state, Operand1.Value, v.HasValue ? (ushort)(v.Value - 1) : null);
        }
        return state;
    }

    private RegisterState ModifyRegistersNot(RegisterState state)
    {
        if (Operand1.Type == OperandType.Register8)
        {
            byte? v = GetReg8(state, Operand1.Value);
            return SetReg8(state, Operand1.Value, v.HasValue ? (byte)~v.Value : null);
        }
        if (Operand1.Type == OperandType.Register16)
        {
            ushort? v = GetReg16(state, Operand1.Value);
            return SetReg16(state, Operand1.Value, v.HasValue ? (ushort)~v.Value : null);
        }
        return state;
    }

    private RegisterState ModifyRegistersNeg(RegisterState state)
    {
        if (Operand1.Type == OperandType.Register8)
        {
            byte? v = GetReg8(state, Operand1.Value);
            return SetReg8(state, Operand1.Value, v.HasValue ? (byte)(0 - v.Value) : null);
        }
        if (Operand1.Type == OperandType.Register16)
        {
            ushort? v = GetReg16(state, Operand1.Value);
            return SetReg16(state, Operand1.Value, v.HasValue ? (ushort)(0 - v.Value) : null);
        }
        return state;
    }

    private RegisterState ModifyRegistersXchg(RegisterState state)
    {
        if (Operand1.Type == OperandType.Register8 && Operand2.Type == OperandType.Register8)
        {
            byte? v1 = GetReg8(state, Operand1.Value);
            byte? v2 = GetReg8(state, Operand2.Value);
            state = SetReg8(state, Operand1.Value, v2);
            return SetReg8(state, Operand2.Value, v1);
        }
        if (Operand1.Type == OperandType.Register16 && Operand2.Type == OperandType.Register16)
        {
            ushort? v1 = GetReg16(state, Operand1.Value);
            ushort? v2 = GetReg16(state, Operand2.Value);
            state = SetReg16(state, Operand1.Value, v2);
            return SetReg16(state, Operand2.Value, v1);
        }
        if (Operand1.Type == OperandType.Register8 && Operand2.Type == OperandType.Register8)
        {
            state = SetReg8(state, Operand1.Value, null);
            return SetReg8(state, Operand2.Value, null);
        }
        if (Operand1.Type == OperandType.Register16 && Operand2.Type == OperandType.Register16)
        {
            state = SetReg16(state, Operand1.Value, null);
            return SetReg16(state, Operand2.Value, null);
        }
        return state;
    }

    private RegisterState ModifyRegistersCbw(RegisterState state)
    {
        if (state.AL.HasValue)
        {
            byte ah = (state.AL.Value & 0x80) != 0 ? (byte)0xFF : (byte)0;
            return state with { AH = ah };
        }
        return state with { AH = null };
    }

    private RegisterState ModifyRegistersCwd(RegisterState state)
    {
        if (state.AX.HasValue)
        {
            byte sign = (state.AX.Value & 0x8000) != 0 ? (byte)0xFF : (byte)0;
            return state with { DH = sign, DL = sign };
        }
        return state with { DH = null, DL = null };
    }

    private RegisterState ModifyRegistersEnter(RegisterState state)
    {
        // ENTER всегда меняет BP и SP. Делаем их неизвестными.
        return state with { BP = null, SP = null };
    }

    private RegisterState ModifyRegistersRotate(RegisterState state)
    {
        // Ротация меняет целевой регистр. Делаем его неизвестным.
        // (точное моделирование ротации возможно, но дорого)
        if (Operand1.Type == OperandType.Register8)
        {
            return SetReg8(state, Operand1.Value, null);
        }
        if (Operand1.Type == OperandType.Register16)
        {
            return SetReg16(state, Operand1.Value, null);
        }
        // Для памяти — ничего не делаем с регистрами
        return state;
    }

    /// <summary>
    /// PUSH: уменьшаем SP на 2. Значение, уходящее в стек, мы не моделируем в RegisterState
    /// (для этого нужен полноценный теневой стек во время дизассемблирования).
    /// </summary>
    private RegisterState ModifyRegistersPush(RegisterState state)
    {
        if (state.SP.HasValue)
            return state with { SP = (ushort)(state.SP.Value - 2) };
        return state with { SP = null };
    }

    /// <summary>
    /// POP: увеличиваем SP на 2.
    /// Если POP в регистр — сбрасываем его значение в null (мы не знаем, что лежало на стеке).
    /// Для POP в память просто меняем SP.
    /// </summary>
    private RegisterState ModifyRegistersPop(RegisterState state)
    {
        // Сначала корректируем SP
        if (state.SP.HasValue)
            state = state with { SP = (ushort)(state.SP.Value + 2) };
        else
            state = state with { SP = null };

        // Если POP в 16-битный регистр общего назначения — сбрасываем его (неизвестное значение со стека)
        if (Operand1.Type == OperandType.Register16)
        {
            return Operand1.Value switch
            {
                0 => state with { AL = null, AH = null },
                1 => state with { CL = null, CH = null },
                2 => state with { DL = null, DH = null },
                3 => state with { BL = null, BH = null },
                4 => state with { SP = null }, // POP SP — особый случай, но всё равно сбрасываем
                5 => state with { BP = null },
                6 => state with { SI = null },
                7 => state with { DI = null },
                _ => state
            };
        }

        // POP в сегментный регистр — тоже сбрасываем
        if (Operand1.Type == OperandType.SegmentRegister)
        {
            return Operand1.Value switch
            {
                0 => state with { ES = null },
                1 => state, // CS — POP CS на 8086 недопустим, но если встретился — не трогаем
                2 => state with { SS = null },
                3 => state with { DS = null },
                _ => state
            };
        }

        return state;
    }

    /// <summary>
    /// Обработка строковых инструкций (MOVS, CMPS, SCAS, LODS, STOS).
    /// 
    /// Правила:
    /// - SI и DI обновляются в зависимости от DF (+/- size).
    /// - Если есть REP/REPZ/REPNZ префикс — CX сбрасывается в null (мы не знаем точное количество итераций,
    ///   особенно для CMPS/SCAS с досрочным выходом).
    /// - Для MOVS/LODS/STOS без REP CX не трогаем.
    /// </summary>
    private RegisterState ModifyRegistersString(RegisterState state, int size, bool updatesCx)
    {
        // Определяем направление
        int delta = 0;
        if (state.DF.HasValue)
        {
            delta = state.DF.Value ? -size : +size;
        }

        // Обновляем SI (источник)
        bool updatesSi = Mnemonic is Mnemonic.MOVSB or Mnemonic.MOVSW or Mnemonic.CMPSB or Mnemonic.CMPSW or Mnemonic.LODSB or Mnemonic.LODSW;

        if (updatesSi)
        {
            if (state.SI.HasValue && delta != 0)
                state = state with { SI = (ushort)(state.SI.Value + delta) };
            else if (delta != 0)
                state = state with { SI = null };
        }

        // Обновляем DI (приёмник)
        bool updatesDi = Mnemonic is Mnemonic.MOVSB or Mnemonic.MOVSW or Mnemonic.CMPSB or Mnemonic.CMPSW or Mnemonic.SCASB or Mnemonic.SCASW or Mnemonic.STOSB or Mnemonic.STOSW;

        if (updatesDi)
        {
            if (state.DI.HasValue && delta != 0)
                state = state with { DI = (ushort)(state.DI.Value + delta) };
            else if (delta != 0)
                state = state with { DI = null };
        }

        // Обработка REP-префиксов
        bool hasRepPrefix = Prefix.HasFlag(InstructionPrefix.REPZ) || Prefix.HasFlag(InstructionPrefix.REPNZ);

        if (hasRepPrefix || updatesCx)
        {
            // При REP* CX почти всегда становится неизвестным (особенно для CMPS/SCAS).
            // Даже для MOVS/LODS/STOS с REP, если мы не знаем начальное CX — результат неизвестен.
            state = state with { CH = null, CL = null };
        }

        return state;
    }
}