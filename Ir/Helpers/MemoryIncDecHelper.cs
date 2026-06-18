namespace UltraDecompiler.Ir.Helpers;

/// <summary>
/// Поддержка декомпиляции постфиксного <c>a++</c> в выражении: перед <c>inc [a]</c>
/// сохраняет копию в temp, если регистр ещё держит старое значение <c>a</c>.
/// </summary>
internal static class MemoryIncDecHelper
{
    /// <summary>
    /// Если GP-регистр содержит ту же стековую переменную, что инкрементируется в памяти,
    /// фиксирует значение во временной переменной (паттерн <c>mov ax,[a]; inc [a]</c>).
    /// </summary>
    public static void SnapshotRegistersHoldingStackSlot(ExprBlock block, Operand memoryOperand)
    {
        if (memoryOperand.Type != OperandType.Memory
            || memoryOperand.BaseReg != AddressRegister.BP
            || memoryOperand.IndexReg != AddressRegister.None)
        {
            return;
        }

        var slot = block.Variables.TryGetStackParameter(memoryOperand.Value)
                   ?? block.Variables.TryGetStackLocal(memoryOperand.Value);
        if (slot is null)
        {
            return;
        }

        SnapshotRegistersHoldingVariable(block, slot);
    }

    private static void SnapshotRegistersHoldingVariable(ExprBlock block, Variable variable)
    {
        foreach (GpRegister16 reg in Enum.GetValues<GpRegister16>())
        {
            if (block.EndRegisters.Get16(reg) is not Variable held || !ReferenceEquals(held, variable))
            {
                continue;
            }

            var snapshot = block.Variables.CreateTempVariable();
            block.Operations.Add(new SetOperation(snapshot, variable));
            block.EndRegisters = block.EndRegisters.Set16(reg, snapshot);
        }
    }
}