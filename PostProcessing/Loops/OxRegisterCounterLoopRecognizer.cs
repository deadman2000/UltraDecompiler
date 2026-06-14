namespace UltraDecompiler.PostProcessing.Loops;

/// <summary>
/// QuickC /Ox: цикл со счётчиком в регистре (SI/DI/BX/CX) без зеркала на стеке до конца.
/// </summary>
public static class OxRegisterCounterLoopRecognizer
{
    /// <summary>
    /// Заменяет <c>if (0 &lt; N)</c> на <c>for (i = 0; i &lt; N; i++)</c> по паттерну inc reg; cmp reg,N; jb.
    /// </summary>
    public static IReadOnlyList<Operation> Convert(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations)
    {
        if (!TryFindPattern(procedure.Instructions, out _, out var limit, out var bodyHint, out var hasScaledIndex))
        {
            return operations;
        }

        var variables = procedure.Expressions?.Variables;
        if (variables is null)
        {
            return operations;
        }

        var index = variables.CreateVariable("var_loop");
        index.Type = CType.UnsignedInt;
        return ConvertList(operations.ToList(), limit, index, bodyHint, hasScaledIndex);
    }

    private static List<Operation> ConvertList(
        List<Operation> operations,
        int limit,
        Variable index,
        bool bodyHint,
        bool hasScaledIndex)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = ConvertNested(operations[i], limit, index, bodyHint, hasScaledIndex);

            if (operations[i] is not IfOperation branch
                || branch.ElseBody is { Count: > 0 }
                || !IsConstantUpperBound(branch.Condition, limit))
            {
                continue;
            }

            var body = branch.ThenBody.ToList();
            if (bodyHint && body.Count == 1 && body[0] is StoreOperation store)
            {
                var loopBody = hasScaledIndex
                    ? new List<Operation> { RewriteScaledIndexedStore(store, index) }
                    : body;

                operations[i] = new ForOperation(
                    new SetOperation(index, ConstExpr.Zero),
                    new CmpExpr(CmpOperation.Ult, index, new ConstExpr(limit)),
                    new IncOperation(index),
                    loopBody);

                RemoveOrphanZeroAssignmentAfter(operations, i);
            }
        }

        return operations;
    }

    private static Operation ConvertNested(
        Operation operation,
        int limit,
        Variable index,
        bool bodyHint,
        bool hasScaledIndex) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                ConvertList(branch.ThenBody.ToList(), limit, index, bodyHint, hasScaledIndex),
                branch.ElseBody is not null
                    ? ConvertList(branch.ElseBody.ToList(), limit, index, bodyHint, hasScaledIndex)
                    : null),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                ConvertList(loop.Body.ToList(), limit, index, bodyHint, hasScaledIndex)),
            _ => operation,
        };

    /// <summary>
    /// Восстанавливает <c>ptr[i &lt;&lt; 1]</c> для тела цикла с SAL перед записью в видеопамять.
    /// </summary>
    private static StoreOperation RewriteScaledIndexedStore(StoreOperation store, Variable index)
    {
        var scaledIndex = new Math2Expr(Math2Operation.Shl, index, new ConstExpr(1));

        if (store.Segment is null
            && store.Address is Variable farPtr
            && farPtr.Type?.IsCharFarPtr == true)
        {
            return store with { Address = new Math2Expr(Math2Operation.Add, farPtr, scaledIndex) };
        }

        if (store.Segment is Variable segmentVar)
        {
            switch (store.Address)
            {
                case Variable offsetVar when offsetVar.Type?.IsCharFarPtr == true
                    && ReferenceEquals(offsetVar.FarPointerSegmentVariable, segmentVar):
                    return store with
                    {
                        Address = new Math2Expr(Math2Operation.Add, offsetVar, scaledIndex),
                    };

                case Variable offsetVar:
                    return store with
                    {
                        Address = new Math2Expr(Math2Operation.Add, offsetVar, scaledIndex),
                    };

                case Math2Expr { Operation: Math2Operation.Add, First: Variable baseVar, Second: ConstExpr { Value: 0 } }:
                    return store with
                    {
                        Address = new Math2Expr(Math2Operation.Add, baseVar, scaledIndex),
                    };
            }
        }

        return store;
    }

    /// <summary>
    /// Удаляет лишнее <c>var = 0</c> после for, оставшееся от mov reg,0 вне тела цикла при flatten /Ox.
    /// </summary>
    private static void RemoveOrphanZeroAssignmentAfter(List<Operation> operations, int forIndex)
    {
        if (forIndex + 1 >= operations.Count
            || operations[forIndex + 1] is not SetOperation { Src: ConstExpr { Value: 0 } } set
            || !AssignmentTarget.TryGetVariable(set.Dst, out var variable))
        {
            return;
        }

        if (IsVariableReadAfter(operations, forIndex + 1, variable))
        {
            return;
        }

        operations.RemoveAt(forIndex + 1);
    }

    private static bool IsVariableReadAfter(IReadOnlyList<Operation> operations, int defIndex, Variable variable)
    {
        for (var i = defIndex + 1; i < operations.Count; i++)
        {
            if (ReadsVariableDeep(operations[i], variable))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReadsVariableDeep(Operation operation, Variable variable) =>
        operation switch
        {
            SetOperation set => ReferenceEquals(set.Dst, variable) || ExprSubstitution.Contains(set.Src, variable),
            StoreOperation store => ExprSubstitution.Contains(store.Address, variable)
                || (store.Segment is not null && ExprSubstitution.Contains(store.Segment, variable))
                || ExprSubstitution.Contains(store.Value, variable),
            IncOperation inc => ReferenceEquals(inc.Target, variable),
            DecOperation dec => ReferenceEquals(dec.Target, variable),
            IfOperation branch => (branch.Condition is not null && ReadsVariableDeep(branch.Condition, variable))
                || branch.ThenBody.Any(op => ReadsVariableDeep(op, variable))
                || (branch.ElseBody?.Any(op => ReadsVariableDeep(op, variable)) ?? false),
            WhileOperation loop => (loop.Condition is not null && ReadsVariableDeep(loop.Condition, variable))
                || loop.Body.Any(op => ReadsVariableDeep(op, variable)),
            ForOperation loop => (loop.Init is not null && ReadsVariableDeep(loop.Init, variable))
                || (loop.Condition is not null && ReadsVariableDeep(loop.Condition, variable))
                || (loop.Iteration is not null && ReadsVariableDeep(loop.Iteration, variable))
                || loop.Body.Any(op => ReadsVariableDeep(op, variable)),
            ReturnOperation ret when ret.Value is not null => ExprSubstitution.Contains(ret.Value, variable),
            CallOperation call => call.Args.Any(arg => ExprSubstitution.Contains(arg, variable)),
            _ => false,
        };

    private static bool ReadsVariableDeep(Expr expr, Variable variable) =>
        ExprSubstitution.Contains(expr, variable);

    private static bool IsConstantUpperBound(Expr condition, int limit)
    {
        if (condition is CmpExpr { Operation: CmpOperation.Ult, Left: ConstExpr { Value: 0 }, Right: ConstExpr { Value: var right } }
            && right == limit)
        {
            return true;
        }

        if (condition is CmpExpr { Operation: CmpOperation.Ugt, Left: ConstExpr { Value: var left }, Right: ConstExpr { Value: 0 } }
            && left == limit)
        {
            return true;
        }

        return false;
    }

    private static bool TryFindPattern(
        IReadOnlyList<Instruction> instructions,
        out GpRegister16 register,
        out int limit,
        out bool hasBodyStore,
        out bool hasScaledIndex)
    {
        register = default;
        limit = 0;
        hasBodyStore = false;
        hasScaledIndex = false;

        for (var i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (instr.Mnemonic != Mnemonic.CMP
                || instr.Operand1.Type != OperandType.Register16
                || instr.Operand2.Type != OperandType.Immediate16)
            {
                continue;
            }

            if (i + 1 >= instructions.Count
                || instructions[i + 1].Mnemonic != Mnemonic.JB)
            {
                continue;
            }

            var backTarget = instructions[i + 1].JumpTarget;
            if (backTarget >= instr.Offset)
            {
                continue;
            }

            var loopRegister = instr.Operand1.AsGpRegister16();
            if (!TryFindRegisterIncrement(instructions, backTarget, instr.Offset, loopRegister))
            {
                continue;
            }

            register = loopRegister;
            limit = instr.Operand2.Value;
            hasBodyStore = instructions.Any(static x => x.Mnemonic == Mnemonic.MOV && x.Operand2.Type == OperandType.Memory);
            hasScaledIndex = TryFindScaledIndexInBody(instructions, backTarget, instr.Offset);
            return true;
        }

        return false;
    }

    private static bool TryFindRegisterIncrement(
        IReadOnlyList<Instruction> instructions,
        int bodyStart,
        int testOffset,
        GpRegister16 register)
    {
        for (var i = 0; i < instructions.Count; i++)
        {
            if (instructions[i].Offset < bodyStart || instructions[i].Offset >= testOffset)
            {
                continue;
            }

            if (instructions[i].Mnemonic == Mnemonic.INC
                && instructions[i].Operand1.Type == OperandType.Register16
                && instructions[i].Operand1.AsGpRegister16() == register)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindScaledIndexInBody(
        IReadOnlyList<Instruction> instructions,
        int bodyStart,
        int testOffset)
    {
        for (var i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (instr.Offset < bodyStart || instr.Offset >= testOffset)
            {
                continue;
            }

            if (instr.Mnemonic != Mnemonic.SAL)
            {
                continue;
            }

            if (instr.Operand2.Type == OperandType.Immediate8 && instr.Operand2.Value == 1)
            {
                return true;
            }
        }

        return false;
    }
}
