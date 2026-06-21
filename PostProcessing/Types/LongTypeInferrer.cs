using UltraDecompiler.Ir.Helpers;
using UltraDecompiler.PostProcessing.Helpers;
using UltraDecompiler.PostProcessing.Infrastructure;

namespace UltraDecompiler.PostProcessing.Types;

/// <summary>
/// Восстанавливает тип <c>long</c> по вызовам рантайма QuickC и inline ADD/ADC,
/// сворачивает IR в арифметику C.
/// </summary>
public static class LongTypeInferrer
{
    /// <summary>
    /// Помечает long-параметры/локали и переписывает long-арифметику в IR.
    /// </summary>
    public static IReadOnlyList<Operation> Infer(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations,
        ProcedureStorage storage)
    {
        if (procedure.Expressions is null)
        {
            return operations;
        }

        if (!LongRuntimeHelpers.UsesLongArithmetic(procedure.Instructions, storage))
        {
            return operations;
        }

        var sites = LongRuntimeHelpers.CollectArithmeticSites(procedure.Instructions, storage);
        if (sites.Count == 0)
        {
            return operations;
        }

        var variables = procedure.Expressions.Variables;
        RegisterLongPairs(procedure, sites, variables, out var mergedHighOffsets);
        UpdateProcedureSignature(procedure, sites, mergedHighOffsets);

        if (LongRuntimeHelpers.IsStraightLineLongProcedure(procedure.Instructions, sites))
        {
            var rebuilt = BuildStraightLineLongBody(procedure, sites, variables);
            PruneUnusedStackLocals(rebuilt, variables);
            RemoveUnusedLongLocals(rebuilt, variables);
            return rebuilt;
        }

        var replacements = BuildHelperReplacements(procedure, sites, variables, storage);
        var rewritten = RewriteOperations(operations, replacements, variables);
        rewritten = RewriteLongReturn(procedure, rewritten, sites, replacements, variables, storage);
        RemoveUnusedLongLocals(rewritten, variables);
        return rewritten;
    }

    /// <summary>
    /// Возвращает <see langword="true"/>, если процедура использует long-арифметику QuickC.
    /// </summary>
    public static bool ProcedureUsesLongShifts(DisassembledProcedure procedure, ProcedureStorage storage) =>
        LongRuntimeHelpers.UsesLongArithmetic(procedure.Instructions, storage);

    private static void PruneUnusedStackLocals(
        IReadOnlyList<Operation> operations,
        VariableStorage variables)
    {
        var referenced = UsedVariableCollector.CollectReferenced(operations);
        foreach (var (offset, variable) in variables.StackLocals.ToList())
        {
            if (variable.IsMergedLongHigh
                || (variable.Type?.Kind != CTypeKind.Long && !referenced.Contains(variable)))
            {
                variables.RemoveStackLocal(offset);
            }
        }
    }

    private static void RemoveUnusedLongLocals(
        IReadOnlyList<Operation> operations,
        VariableStorage variables)
    {
        var referenced = UsedVariableCollector.CollectReferenced(operations);
        foreach (var (baseOffset, lowVariable, highVariable) in variables.LongLocals.ToList())
        {
            if (referenced.Contains(lowVariable))
            {
                continue;
            }

            variables.RemoveLongLocal(baseOffset, lowVariable, highVariable);
        }
    }

    private static IReadOnlyList<Operation> BuildStraightLineLongBody(
        DisassembledProcedure procedure,
        IReadOnlyList<LongArithmeticSite> sites,
        VariableStorage variables)
    {
        var operations = new List<Operation>();

        foreach (var site in sites.Where(static s => s.EmitAssignment))
        {
            if (site.DestLowOffset >= 0
                || variables.TryGetStackLocal(site.DestLowOffset) is not { } dest)
            {
                continue;
            }

            var expr = BuildSiteExpression(site, variables);
            operations.Add(new SetOperation(dest.ToSet(), expr));
        }

        var returnOperands = LongRuntimeHelpers.CollectLongReturnOperands(procedure.Instructions);
        Expr? returnExpr = null;

        foreach (var offset in returnOperands)
        {
            if (variables.TryGetStackLocal(offset) is not { } local)
            {
                continue;
            }

            var part = LongExpr.FromVariable(local);
            returnExpr = returnExpr is null ? part : returnExpr.Calculate(Math2Operation.Add, part);
        }

        if (returnExpr is null && sites.Any(static s => s.EmitAssignment))
        {
            var lastSite = sites.Last(static s => s.EmitAssignment);
            if (variables.TryGetStackLocal(lastSite.DestLowOffset) is { } lastLocal)
            {
                returnExpr = LongExpr.FromVariable(lastLocal);
            }
        }

        operations.Add(new ReturnOperation(returnExpr ?? ConstExpr.Zero, IsExplicit: true));
        return operations;
    }

    private static Expr BuildSiteExpression(LongArithmeticSite site, VariableStorage variables)
    {
        var left = BuildLongReference(site.LeftLowOffset, variables);
        var right = site.RightLowOffset != 0
            ? BuildLongReference(site.RightLowOffset, variables)
            : null;

        return site.Kind switch
        {
            LongArithmeticKind.Add => left.Calculate(Math2Operation.Add, right!),
            LongArithmeticKind.Sub => left.Calculate(Math2Operation.Sub, right!),
            LongArithmeticKind.Mul => left.Calculate(Math2Operation.Mul, right!),
            LongArithmeticKind.Div => left.Calculate(Math2Operation.Div, right!),
            LongArithmeticKind.Rem => left.Calculate(Math2Operation.Mod, right!),
            LongArithmeticKind.ShiftLeft => left.Calculate(Math2Operation.Shl, new ConstExpr(site.ShiftCount)),
            LongArithmeticKind.ShiftRight => left.Calculate(Math2Operation.Shr, new ConstExpr(site.ShiftCount)),
            LongArithmeticKind.ShiftedSum => BuildShiftedSumExpression(site, variables),
            _ => left,
        };
    }

    private static Expr BuildShiftedSumExpression(LongArithmeticSite site, VariableStorage variables)
    {
        var left = BuildLongReference(site.LeftLowOffset, variables);
        var right = BuildLongReference(site.RightLowOffset, variables);
        var shiftedLeft = left.Calculate(Math2Operation.Shl, new ConstExpr(site.ShiftCount));
        var shiftedRight = right.Calculate(Math2Operation.Shr, new ConstExpr(site.SecondShiftCount));
        return shiftedLeft.Calculate(Math2Operation.Add, shiftedRight);
    }

    private static void RegisterLongPairs(
        DisassembledProcedure procedure,
        IReadOnlyList<LongArithmeticSite> sites,
        VariableStorage variables,
        out HashSet<int> mergedHighOffsets)
    {
        mergedHighOffsets = new HashSet<int>();
        var handledParams = new HashSet<int>();
        var handledLocals = new HashSet<int>();

        foreach (var site in sites)
        {
            RegisterLongPairOffset(site.LeftLowOffset, variables, mergedHighOffsets, handledParams, handledLocals);
            if (site.RightLowOffset != 0)
            {
                RegisterLongPairOffset(site.RightLowOffset, variables, mergedHighOffsets, handledParams, handledLocals);
            }

            if (site.EmitAssignment)
            {
                RegisterLongPairOffset(site.DestLowOffset, variables, mergedHighOffsets, handledParams, handledLocals);
            }
        }

        UpdateFunctionParameters(procedure, variables, mergedHighOffsets);
    }

    private static void UpdateFunctionParameters(
        DisassembledProcedure procedure,
        VariableStorage variables,
        HashSet<int> mergedHighOffsets)
    {
        if (procedure.Expressions is null)
        {
            return;
        }

        var updated = new List<FunctionParameter>();
        var argIndex = 0;

        foreach (var param in procedure.Expressions.Parameters
                     .Where(p => !mergedHighOffsets.Contains(p.StackOffset))
                     .OrderBy(static p => p.StackOffset))
        {
            if (variables.TryGetStackParameter(param.StackOffset) is not { } variable)
            {
                continue;
            }

            variable = variable with { Name = $"arg{argIndex}" };
            variables.RenameStackParameter(param.StackOffset, variable);
            updated.Add(new FunctionParameter(param.StackOffset, variable));
            argIndex++;
        }

        procedure.Expressions.SetParameters(updated);
    }

    private static void RegisterLongPairOffset(
        int lowOffset,
        VariableStorage variables,
        HashSet<int> mergedHighOffsets,
        HashSet<int> handledParams,
        HashSet<int> handledLocals)
    {
        if (lowOffset >= 4
            && !handledParams.Contains(lowOffset)
            && variables.TryGetStackParameter(lowOffset) is { } lowParam
            && variables.TryGetStackParameter(lowOffset + 2) is { } highParam)
        {
            variables.RegisterLongParameter(lowOffset, lowParam, highParam);
            mergedHighOffsets.Add(lowOffset + 2);
            handledParams.Add(lowOffset);
        }

        if (lowOffset < 0
            && !handledLocals.Contains(lowOffset)
            && variables.TryGetStackLocal(lowOffset) is { } lowLocal
            && variables.TryGetStackLocal(lowOffset + 2) is { } highLocal)
        {
            variables.RegisterLongLocal(lowOffset, lowLocal, highLocal);
            mergedHighOffsets.Add(lowOffset + 2);
            handledLocals.Add(lowOffset);
        }
    }

    private static void UpdateProcedureSignature(
        DisassembledProcedure procedure,
        IReadOnlyList<LongArithmeticSite> sites,
        HashSet<int> mergedHighOffsets)
    {
        var parameters = new List<ProcedureParameter>();
        if (procedure.Expressions is not null)
        {
            var argIndex = 0;
            foreach (var param in procedure.Expressions.Parameters
                         .Where(p => !mergedHighOffsets.Contains(p.StackOffset))
                         .OrderBy(static p => p.StackOffset))
            {
                var type = param.Variable.Type?.Kind == CTypeKind.Long
                    ? CType.Long
                    : CType.Int;
                parameters.Add(new ProcedureParameter(type, new StackParameter(param.StackOffset)));
                argIndex++;
            }
        }

        var returnType = sites.Count > 0 && UsesLongReturnValue(procedure.Instructions)
            ? CType.Long
            : procedure.Signature.ReturnType;

        procedure.Signature = new ProcedureSignature(returnType, parameters);
    }

    private static bool UsesLongReturnValue(IReadOnlyList<Instruction> instructions)
    {
        return LongRuntimeHelpers.CollectLongReturnOperands(instructions).Count > 0
            || HasLegacyLongReturnPattern(instructions);
    }

    private static bool HasLegacyLongReturnPattern(IReadOnlyList<Instruction> instructions)
    {
        for (var i = 0; i < instructions.Count - 3; i++)
        {
            if (instructions[i].Mnemonic == Mnemonic.ADD
                && instructions[i + 1].Mnemonic == Mnemonic.ADC
                && instructions[i + 2].Mnemonic == Mnemonic.MOV
                && instructions[i + 3].Mnemonic == Mnemonic.MOV)
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, Expr> BuildHelperReplacements(
        DisassembledProcedure procedure,
        IReadOnlyList<LongArithmeticSite> sites,
        VariableStorage variables,
        ProcedureStorage storage)
    {
        var replacements = new Dictionary<string, Expr>(StringComparer.Ordinal);

        foreach (var site in sites)
        {
            if (site.Kind is LongArithmeticKind.Add or LongArithmeticKind.Sub or LongArithmeticKind.ShiftedSum)
            {
                continue;
            }

            var key = site.CalleeName;
            if (string.IsNullOrEmpty(key))
            {
                if (!LongRuntimeHelpers.TryResolveCallTarget(procedure.Instructions[site.Index], storage, out key))
                {
                    key = $"sub_{procedure.Instructions[site.Index].JumpTarget:X4}";
                }
            }

            replacements[key] = BuildSiteExpression(site, variables);
        }

        return replacements;
    }

    private static Expr BuildLongReference(int lowOffset, VariableStorage variables)
    {
        if (lowOffset >= 4
            && variables.TryGetStackParameter(lowOffset) is { } param
            && param.Type?.Kind == CTypeKind.Long)
        {
            return LongExpr.FromVariable(param);
        }

        if (lowOffset < 0
            && variables.TryGetStackLocal(lowOffset) is { } local
            && local.Type?.Kind == CTypeKind.Long)
        {
            return LongExpr.FromVariable(local);
        }

        var low = ResolveWordReference(lowOffset, variables);
        var high = ResolveWordReference(lowOffset + 2, variables);
        return LongExpr.FromWords(low, high);
    }

    private static Expr ResolveWordReference(int offset, VariableStorage variables)
    {
        if (offset >= 4 && variables.TryGetStackParameter(offset) is { } param)
        {
            return param.ToGet();
        }

        if (offset < 0 && variables.TryGetStackLocal(offset) is { } local)
        {
            return local.ToGet();
        }

        return ConstExpr.Zero;
    }

    private static IReadOnlyList<Operation> RewriteOperations(
        IReadOnlyList<Operation> operations,
        IReadOnlyDictionary<string, Expr> shiftReplacements,
        VariableStorage variables)
    {
        var result = new List<Operation>();

        foreach (var op in operations)
        {
            switch (op)
            {
                case SetOperation { Src: CallExpr call } set when shiftReplacements.TryGetValue(call.Name, out var shift):
                    if (TryGetLongDestFromShiftCall(set, variables, out var dest))
                    {
                        result.Add(new SetOperation(dest.ToSet(), shift));
                    }
                    else
                    {
                        result.Add(new SetOperation(set.Dst, shift));
                    }

                    break;

                case CallOperation call when shiftReplacements.TryGetValue(call.Name, out _):
                    break;

                case StoreOperation store when IsMergedLongHighStore(store, variables):
                    break;

                case SetOperation set when IsMergedLongHighAssignment(set, variables):
                    break;

                default:
                    result.Add(RewriteNestedOperation(op, shiftReplacements));
                    break;
            }
        }

        return result;
    }

    private static bool TryGetLongDestFromShiftCall(
        SetOperation set,
        VariableStorage variables,
        out Variable dest)
    {
        dest = null!;
        if (!AssignmentTarget.TryGetVariable(set.Dst, out var temp) || !temp.IsTemp)
        {
            return false;
        }

        foreach (var (_, lowVariable, _) in variables.LongLocals)
        {
            dest = lowVariable;
            return true;
        }

        return false;
    }

    private static bool IsMergedLongHighStore(StoreOperation store, VariableStorage variables) =>
        store.Address is VariableExpr { Var: var variable }
        && variable.IsMergedLongHigh;

    private static bool IsMergedLongHighAssignment(SetOperation set, VariableStorage variables) =>
        AssignmentTarget.TryGetVariable(set.Dst, out var variable) && variable.IsMergedLongHigh;

    private static IReadOnlyList<Operation> RewriteLongReturn(
        DisassembledProcedure procedure,
        IReadOnlyList<Operation> operations,
        IReadOnlyList<LongArithmeticSite> sites,
        IReadOnlyDictionary<string, Expr> replacements,
        VariableStorage variables,
        ProcedureStorage storage)
    {
        if (procedure.Signature.ReturnType.Kind != CTypeKind.Long)
        {
            return operations;
        }

        Expr? returnExpr = null;
        var shiftedSum = sites.FirstOrDefault(static s => s.Kind == LongArithmeticKind.ShiftedSum);
        if (shiftedSum is not null)
        {
            returnExpr = BuildShiftedSumExpression(shiftedSum, variables);
        }
        else
        {
            var leftShift = sites.FirstOrDefault(static s => s.Kind == LongArithmeticKind.ShiftLeft);
            var rightShift = sites.FirstOrDefault(static s => s.Kind == LongArithmeticKind.ShiftRight);
            if (leftShift is not null && rightShift is not null)
            {
                var leftExpr = BuildSiteExpression(leftShift, variables);
                var rightExpr = BuildSiteExpression(rightShift, variables);
                returnExpr = leftExpr.Calculate(Math2Operation.Add, rightExpr);
            }
        }

        if (returnExpr is null)
        {
            return operations;
        }

        var result = new List<Operation>();
        var emittedReturn = false;

        foreach (var op in operations)
        {
            if (op is ReturnOperation)
            {
                if (!emittedReturn)
                {
                    result.Add(new ReturnOperation(returnExpr, IsExplicit: true));
                    emittedReturn = true;
                }

                continue;
            }

            if (op is SetOperation { Src: CallExpr call } && replacements.ContainsKey(call.Name))
            {
                continue;
            }

            if (op is SetOperation set
                && AssignmentTarget.TryGetVariable(set.Dst, out var variable)
                && variables.LongLocals.Any(e => ReferenceEquals(e.LowVariable, variable)))
            {
                continue;
            }

            if (op is CallOperation callOp && replacements.ContainsKey(callOp.Name))
            {
                continue;
            }

            if (op is StoreOperation store && IsMergedLongHighStore(store, variables))
            {
                continue;
            }

            if (op is SetOperation badSet && IsMergedLongHighAssignment(badSet, variables))
            {
                continue;
            }

            if (op is SetOperation { Src: CallExpr nestedCall } && replacements.ContainsKey(nestedCall.Name))
            {
                continue;
            }

            result.Add(op);
        }

        if (!emittedReturn)
        {
            result.Add(new ReturnOperation(returnExpr, IsExplicit: true));
        }

        return result;
    }

    /// <summary>Нормализует аргументы вызовов (long-параметры, <c>printf(%ld)</c>).</summary>
    public static IReadOnlyList<Operation> RewriteCallArguments(
        IReadOnlyList<Operation> operations,
        ProcedureStorage storage)
    {
        var result = new List<Operation>();

        foreach (var op in operations)
        {
            result.Add(RewriteNestedOperation(op, new Dictionary<string, Expr>()));
        }

        for (var i = 0; i < result.Count; i++)
        {
            result[i] = RewriteCallOperationArgs(result[i], storage);
        }

        return result;
    }

    private static Operation RewriteCallOperationArgs(Operation op, ProcedureStorage storage)
    {
        return op switch
        {
            SetOperation { Src: CallExpr call } set => set with { Src = RewriteCallExpr(call, storage) },
            CallOperation call => call with { Args = RewriteCallArgs(call.Name, call.Args, storage) },
            IfOperation branch => branch with
            {
                ThenBody = branch.ThenBody.Select(o => RewriteCallOperationArgs(o, storage)).ToList(),
                ElseBody = branch.ElseBody?.Select(o => RewriteCallOperationArgs(o, storage)).ToList(),
            },
            WhileOperation loop => loop with
            {
                Body = loop.Body.Select(o => RewriteCallOperationArgs(o, storage)).ToList(),
            },
            ForOperation forLoop => forLoop with
            {
                Body = forLoop.Body.Select(o => RewriteCallOperationArgs(o, storage)).ToList(),
            },
            SwitchOperation sw => OperationTreeMapper.MapSwitchBodies(
                sw,
                bodies => bodies.Select(o => RewriteCallOperationArgs(o, storage)).ToList()),
            _ => op,
        };
    }

    private static CallExpr RewriteCallExpr(CallExpr call, ProcedureStorage storage) =>
        call with { Args = RewriteCallArgs(call.Name, call.Args, storage) };

    private static IReadOnlyList<Expr> RewriteCallArgs(
        string name,
        IReadOnlyList<Expr> args,
        ProcedureStorage storage)
    {
        if (args.Count == 0)
        {
            return args;
        }

        if (storage.TryGetByName(name, out var callee)
            && callee is not null
            && callee.Signature != ProcedureSignature.Unknown)
        {
            return MergeLongArguments(args, callee.Signature.Parameters);
        }

        if (string.Equals(name, "printf", StringComparison.Ordinal))
        {
            return MergePrintfLongArguments(args, storage);
        }

        return args;
    }

    private static IReadOnlyList<Expr> MergeLongArguments(
        IReadOnlyList<Expr> args,
        IReadOnlyList<ProcedureParameter> parameters)
    {
        var result = new List<Expr>();
        var argIndex = 0;

        foreach (var parameter in parameters)
        {
            if (argIndex >= args.Count)
            {
                break;
            }

            if (parameter.Type.Kind == CTypeKind.Long)
            {
                var low = args[argIndex++];
                var high = argIndex < args.Count ? args[argIndex++] : ConstExpr.Zero;
                result.Add(LongExpr.FromWords(low, high));
            }
            else
            {
                result.Add(args[argIndex++]);
            }
        }

        while (argIndex < args.Count)
        {
            result.Add(args[argIndex++]);
        }

        return result;
    }

    private static IReadOnlyList<Expr> MergePrintfLongArguments(
        IReadOnlyList<Expr> args,
        ProcedureStorage storage)
    {
        if (args.Count < 2 || args[0] is not StringExpr format)
        {
            return args;
        }

        if (!format.Value.Contains("%ld", StringComparison.Ordinal))
        {
            return args;
        }

        var working = args.ToList();
        while (working.Count > 2 && working[^1] is ConstExpr { Value: 0 })
        {
            working.RemoveAt(working.Count - 1);
        }

        var result = new List<Expr> { working[0] };
        for (var i = 1; i < working.Count; i++)
        {
            if (working[i] is CallExpr call
                && storage.TryGetByName(call.Name, out var callee)
                && callee is not null
                && callee.Signature.ReturnType.Kind == CTypeKind.Long)
            {
                result.Add(call);
                if (i + 1 < working.Count && working[i + 1] is ConstExpr { Value: 0 })
                {
                    i++;
                }

                continue;
            }

            if (working[i] is VariableExpr { Var: var variable } && variable.Type?.Kind == CTypeKind.Long)
            {
                result.Add(LongExpr.FromVariable(variable));
                if (i + 1 < working.Count && working[i + 1] is ConstExpr { Value: 0 })
                {
                    i++;
                }

                continue;
            }

            if (i + 1 < working.Count && LooksLikeLongWordPair(working[i], working[i + 1]))
            {
                result.Add(LongExpr.FromWords(working[i], working[i + 1]));
                i++;
            }
            else
            {
                result.Add(working[i]);
            }
        }

        while (result.Count > 2 && result[^1] is ConstExpr { Value: 0 })
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static bool LooksLikeLongWordPair(Expr low, Expr high) =>
        high is ConstExpr { Value: 0 } || high is VariableExpr { Var.IsMergedLongHigh: true };

    private static Operation RewriteNestedOperation(
        Operation op,
        IReadOnlyDictionary<string, Expr> shiftReplacements)
    {
        return op switch
        {
            SetOperation { Src: CallExpr call } set when shiftReplacements.TryGetValue(call.Name, out var shift)
                => new SetOperation(set.Dst, shift),
            IfOperation branch => branch with
            {
                ThenBody = branch.ThenBody
                    .Select(o => RewriteNestedOperation(o, shiftReplacements))
                    .ToList(),
                ElseBody = branch.ElseBody?
                    .Select(o => RewriteNestedOperation(o, shiftReplacements))
                    .ToList(),
            },
            WhileOperation loop => loop with
            {
                Body = loop.Body.Select(o => RewriteNestedOperation(o, shiftReplacements)).ToList(),
            },
            ForOperation forLoop => forLoop with
            {
                Body = forLoop.Body.Select(o => RewriteNestedOperation(o, shiftReplacements)).ToList(),
            },
            SwitchOperation sw => OperationTreeMapper.MapSwitchBodies(
                sw,
                bodies => bodies.Select(o => RewriteNestedOperation(o, shiftReplacements)).ToList()),
            ReturnOperation ret => ret with
            {
                Value = ret.Value is null ? null : ReplaceShiftCalls(ret.Value, shiftReplacements),
            },
            _ => op,
        };
    }

    private static Expr ReplaceShiftCalls(Expr expr, IReadOnlyDictionary<string, Expr> shiftReplacements)
    {
        return expr switch
        {
            CallExpr call when shiftReplacements.TryGetValue(call.Name, out var shift) => shift,
            Math2Expr math => math with
            {
                First = ReplaceShiftCalls(math.First, shiftReplacements),
                Second = ReplaceShiftCalls(math.Second, shiftReplacements),
            },
            LongExpr longExpr => longExpr with
            {
                Low = ReplaceShiftCalls(longExpr.Low, shiftReplacements),
                High = ReplaceShiftCalls(longExpr.High, shiftReplacements),
            },
            _ => expr,
        };
    }
}
