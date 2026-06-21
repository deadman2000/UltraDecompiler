namespace UltraDecompiler.Ir.Builder;

public partial class ExpressionBuilder
{
    /// <summary>
    /// Параметры декомпилируемой функции, восстановленные по прологу и обращениям [BP+offset].
    /// Пустой список, если стандартный стековый кадр не обнаружен.
    /// </summary>
    public IReadOnlyList<FunctionParameter> Parameters { get; private set; } = [];

    /// <summary>Обновляет список параметров после пост-обработки (например, нормализация main).</summary>
    public void SetParameters(IReadOnlyList<FunctionParameter> parameters) => Parameters = parameters;

    /// <summary>
    /// Анализирует пролог и обращения к аргументам/локалам на стеке;
    /// создаёт именованные переменные параметров (argN) и локальных переменных (varN).
    /// </summary>
    private void AnalyzeFunctionParameters(ControlFlowGraph graph)
    {
        Parameters = [];

        if (!StackFrameAnalyzer.HasStandardPrologue(graph.EntryBlock))
            return;

        var paramOffsets = StackFrameAnalyzer.CollectParameterOffsets(graph);
        if (paramOffsets.Count > 0)
            Parameters = Variables.ActivateStackFrame(paramOffsets);

        var localOffsets = StackFrameAnalyzer.ExpandWithPrologueAllocation(
            graph,
            StackFrameAnalyzer.CollectLocalOffsets(graph));
        if (localOffsets.Count > 0)
            Variables.ActivateStackLocals(localOffsets);
    }

    /// <summary>
    /// Распознавание пролога QuickC и сбор смещений параметров [BP+offset].
    /// </summary>
    private static class StackFrameAnalyzer
    {
        /// <summary>Минимальное смещение первого параметра относительно BP (ниже — saved BP и return address).</summary>
        private const int FirstParameterOffset = 4;

        /// <summary>
        /// Проверяет наличие типичного пролога: <c>push bp; mov bp, sp</c> или <c>enter</c>.
        /// </summary>
        public static bool HasStandardPrologue(BasicBlock entryBlock)
        {
            var instrs = entryBlock.Instructions;
            if (instrs.Count == 0)
                return false;

            if (instrs[0].Mnemonic == Mnemonic.ENTER)
                return true;

            for (int i = 0; i < instrs.Count - 1; i++)
            {
                if (IsPushBp(instrs[i]) && IsMovBpSp(instrs[i + 1]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Собирает все смещения вида [BP+disp] с disp ≥ 4 (параметры стекового кадра).
        /// </summary>
        public static IReadOnlyList<int> CollectParameterOffsets(ControlFlowGraph graph)
        {
            var offsets = new SortedSet<int>();

            foreach (var block in graph.Blocks)
            {
                foreach (var instr in block.Instructions)
                {
                    CollectFromOperand(instr.Operand1, offsets);
                    CollectFromOperand(instr.Operand2, offsets);
                }
            }

            return offsets.ToList();
        }

        /// <summary>
        /// Дополняет смещения локалов слотами из <c>sub sp, N</c>/<c>enter N, 0</c>, если в IR нет обращений
        /// к части выделенного кадра (неиспользуемые <c>int</c> вроде <c>int a;</c> в func.c).
        /// </summary>
        public static IReadOnlyList<int> ExpandWithPrologueAllocation(
            ControlFlowGraph graph,
            IReadOnlyList<int> memoryOffsets)
        {
            var allocationSize = GetLocalAllocationSize(graph);
            if (allocationSize is null or <= 0)
            {
                return memoryOffsets;
            }

            var size = allocationSize.Value;

            if (HasLeaLocalOffset(graph))
            {
                return memoryOffsets;
            }

            var result = new SortedSet<int>(memoryOffsets);

            if (memoryOffsets.Count == 0 && size > 2)
            {
                // Большой кадр без обращений — одна база (StackLocalArrayInferrer выведет char[N]).
                result.Add(-size);
                return result.ToList();
            }

            for (var offset = -2; offset >= -size; offset -= 2)
            {
                result.Add(offset);
            }

            return result.ToList();
        }

        /// <summary>
        /// Собирает все смещения вида [BP+disp] с disp &lt; 0 (локальные переменные стекового кадра).
        /// Выравнивание по 2 байта (слово).
        /// </summary>
        public static IReadOnlyList<int> CollectLocalOffsets(ControlFlowGraph graph)
        {
            var offsets = new SortedSet<int>();

            foreach (var block in graph.Blocks)
            {
                foreach (var instr in block.Instructions)
                {
                    CollectLocalFromOperand(instr.Operand1, offsets);
                    CollectLocalFromOperand(instr.Operand2, offsets);
                }
            }

            return offsets.ToList();
        }

        private static void CollectFromOperand(Operand operand, SortedSet<int> offsets)
        {
            if (operand.Type != OperandType.Memory)
                return;

            if (operand.BaseReg != AddressRegister.BP || operand.IndexReg != AddressRegister.None)
                return;

            if (operand.Value < FirstParameterOffset)
                return;

            // Near-модель QuickC передаёт 16-битные слова; выравниваем по чётным смещениям.
            if (operand.Value % 2 != 0)
                return;

            offsets.Add(operand.Value);
        }

        private static void CollectLocalFromOperand(Operand operand, SortedSet<int> offsets)
        {
            if (operand.Type != OperandType.Memory)
                return;

            if (operand.BaseReg != AddressRegister.BP || operand.IndexReg != AddressRegister.None)
                return;

            if (operand.Value >= 0)
                return;

            // Локалы — слова, чётные отрицательные смещения (например -2, -4).
            if (operand.Value % 2 != 0)
                return;

            offsets.Add(operand.Value);
        }

        private static bool IsPushBp(Instruction instr) =>
            instr.Mnemonic == Mnemonic.PUSH &&
            instr.Operand1.Type == OperandType.Register16 &&
            instr.Operand1.AsGpRegister16() == GpRegister16.BP;

        private static bool IsMovBpSp(Instruction instr) =>
            instr.Mnemonic == Mnemonic.MOV &&
            instr.Operand1.Type == OperandType.Register16 &&
            instr.Operand1.AsGpRegister16() == GpRegister16.BP &&
            instr.Operand2.Type == OperandType.Register16 &&
            instr.Operand2.AsGpRegister16() == GpRegister16.SP;

        private static int? GetLocalAllocationSize(ControlFlowGraph graph)
        {
            foreach (var block in graph.Blocks)
            {
                var size = TryGetSubSpAllocationSize(block.Instructions)
                    ?? TryGetEnterAllocationSize(block.Instructions);
                if (size is > 0)
                {
                    return size;
                }
            }

            return null;
        }

        private static int? TryGetSubSpAllocationSize(IReadOnlyList<Instruction> instructions)
        {
            for (var i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                if (instr.Mnemonic != Mnemonic.SUB
                    || instr.Operand1.Type != OperandType.Register16
                    || instr.Operand1.AsGpRegister16() != GpRegister16.SP
                    || instr.Operand2.Type != OperandType.Immediate16)
                {
                    continue;
                }

                if (!HasRecentMovBpSp(instructions, i))
                {
                    continue;
                }

                return instr.Operand2.Value;
            }

            return null;
        }

        private static int? TryGetEnterAllocationSize(IReadOnlyList<Instruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if (instr.Mnemonic == Mnemonic.ENTER
                    && instr.Operand1.Type == OperandType.Immediate16
                    && instr.Operand1.Value > 0)
                {
                    return instr.Operand1.Value;
                }
            }

            return null;
        }

        private static bool HasRecentMovBpSp(IReadOnlyList<Instruction> instructions, int subSpIndex)
        {
            for (var i = subSpIndex - 1; i >= 0 && subSpIndex - i <= 4; i--)
            {
                var instr = instructions[i];
                if (IsMovBpSp(instr))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLeaLocalOffset(ControlFlowGraph graph)
        {
            foreach (var block in graph.Blocks)
            {
                foreach (var instr in block.Instructions)
                {
                    if (instr.Mnemonic != Mnemonic.LEA
                        || instr.Operand2.Type != OperandType.Memory
                        || instr.Operand2.BaseReg != AddressRegister.BP
                        || instr.Operand2.IndexReg != AddressRegister.None
                        || instr.Operand2.Value >= 0
                        || instr.Operand2.Value % 2 != 0)
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
