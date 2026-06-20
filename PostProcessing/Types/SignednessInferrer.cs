namespace UltraDecompiler.PostProcessing.Types;

/// <summary>
/// Синхронизирует знаковость сигнатуры пользовательской процедуры с выведенными типами переменных IR.
/// </summary>
public static class SignednessInferrer
{
    /// <summary>
    /// Обновляет тип возврата и параметры в <see cref="DisassembledProcedure.Signature"/>
    /// по <see cref="Variable.Type"/> у аргументов и выражения return.
    /// </summary>
    public static void Infer(DisassembledProcedure procedure, IEnumerable<Operation> operations)
    {
        if (procedure.Expressions is null)
        {
            return;
        }

        var returnType = InferReturnType(operations) ?? procedure.Signature.ReturnType;
        var parameters = BuildParameters(procedure);
        procedure.Signature = new ProcedureSignature(returnType, parameters);
    }

    private static IReadOnlyList<ProcedureParameter> BuildParameters(DisassembledProcedure procedure)
    {
        if (procedure.Expressions!.Parameters.Count == 0)
        {
            return procedure.Signature.Parameters;
        }

        var result = new List<ProcedureParameter>(procedure.Expressions.Parameters.Count);

        foreach (var parameter in procedure.Expressions.Parameters)
        {
            var type = parameter.Variable.Type ?? CType.Int;
            result.Add(new ProcedureParameter(type, new StackParameter(parameter.StackOffset)));
        }

        return result;
    }

    private static CType? InferReturnType(IEnumerable<Operation> operations)
    {
        CType? inferred = null;

        foreach (var op in OperationFlattener.EnumerateNested(operations))
        {
            if (op is not ReturnOperation { Value: { } value })
            {
                continue;
            }

            var candidate = InferTypeFromExpr(value);
            if (candidate is null)
            {
                continue;
            }

            inferred = inferred is null || candidate.Kind == CTypeKind.Unsigned
                ? candidate
                : inferred;
        }

        return inferred;
    }

    private static CType? InferTypeFromExpr(Expr expr)
    {
        foreach (var variable in ExprSubstitution.CollectVariables(expr))
        {
            if (variable.Type?.Kind == CTypeKind.Unsigned)
            {
                return CType.UnsignedInt;
            }
        }

        if (HasUnsignedShift(expr))
        {
            return CType.UnsignedInt;
        }

        return null;
    }

    /// <summary>Результат беззнакового SHR в выражении return трактуем как <c>unsigned</c>.</summary>
    private static bool HasUnsignedShift(Expr expr) =>
        expr switch
        {
            Math2Expr { Operation: Math2Operation.Shr, First: var shifted }
                when ExprSubstitution.CollectVariables(shifted).Any(static v => v.Type?.Kind == CTypeKind.Unsigned)
                => true,
            Math1Expr m => HasUnsignedShift(m.Op),
            Math2Expr m => HasUnsignedShift(m.First) || HasUnsignedShift(m.Second),
            CmpExpr cmp => HasUnsignedShift(cmp.Left) || HasUnsignedShift(cmp.Right),
            MemExpr mem => HasUnsignedShift(mem.Address) || (mem.Segment is not null && HasUnsignedShift(mem.Segment)),
            CallExpr call => call.Args.Any(HasUnsignedShift),
            LongExpr longExpr => HasUnsignedShift(longExpr.Low) || HasUnsignedShift(longExpr.High),
            MemberExpr member => HasUnsignedShift(member.Base),
            IncDecExpr inc => HasUnsignedShift(inc.Operand),
            AddressOfExpr addr => HasUnsignedShift(addr.Operand),
            _ => false,
        };
}
