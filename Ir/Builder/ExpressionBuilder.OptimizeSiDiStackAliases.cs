using UltraDecompiler.Ir.Variables;

namespace UltraDecompiler.Ir.Builder;

public partial class ExpressionBuilder
{
    /// <summary>
    /// /Ox: QuickC держит локальные переменные циклов в SI/DI.
    /// По парам <c>reg = var</c> / <c>var = reg</c> подставляет <c>varN</c> вместо <c>regSI</c>/<c>regDI</c>.
    /// </summary>
    protected void OptimizeSiDiStackAliases()
    {
        var aliases = DetectSiDiStackAliases();
        if (aliases.Count == 0)
        {
            return;
        }

        foreach (var block in Blocks)
        {
            SubstituteSiDiAliasesInBlock(block, aliases);
            RemoveTautologicalSets(block);
        }
    }

    private Dictionary<Variable, Variable> DetectSiDiStackAliases()
    {
        var loads = new Dictionary<Variable, Variable>(ReferenceEqualityComparer.Instance);
        var stores = new Dictionary<Variable, Variable>(ReferenceEqualityComparer.Instance);

        foreach (var block in Blocks)
        {
            foreach (var operation in block.Operations)
            {
                if (operation is not SetOperation set)
                {
                    continue;
                }

                if (AssignmentTarget.TryGetVariable(set.Dst, out var dstVar) &&
                    IsSiOrDi(dstVar) &&
                    set.Src is VariableExpr { Var: { IsStack: true } loadStackVar })
                {
                    if (loads.TryGetValue(dstVar, out var existingLoad) &&
                        !ReferenceEquals(existingLoad, loadStackVar))
                    {
                        loads.Remove(dstVar);
                    }
                    else
                    {
                        loads[dstVar] = loadStackVar;
                    }
                }

                if (AssignmentTarget.TryGetVariable(set.Dst, out var storeDst) &&
                    storeDst.IsStack &&
                    set.Src is VariableExpr { Var: var srcReg } &&
                    IsSiOrDi(srcReg))
                {
                    if (stores.TryGetValue(srcReg, out var existingStore) &&
                        !ReferenceEquals(existingStore, storeDst))
                    {
                        stores.Remove(srcReg);
                    }
                    else
                    {
                        stores[srcReg] = storeDst;
                    }
                }
            }
        }

        var aliases = new Dictionary<Variable, Variable>(ReferenceEqualityComparer.Instance);
        foreach (var register in EnumerateSiDi())
        {
            if (!stores.TryGetValue(register, out var stackVar))
            {
                continue;
            }

            if (loads.TryGetValue(register, out var loadVar) &&
                !ReferenceEquals(loadVar, stackVar))
            {
                continue;
            }

            aliases[register] = stackVar;
        }

        return aliases;
    }

    private IEnumerable<Variable> EnumerateSiDi()
    {
        yield return Variables.SI;
        yield return Variables.DI;
    }

    private static bool IsSiOrDi(Variable variable) =>
        variable.IsRegister &&
        (variable.Name is "regSI" or "regDI");

    private static void SubstituteSiDiAliasesInBlock(
        ExprBlock block,
        IReadOnlyDictionary<Variable, Variable> aliases)
    {
        for (var i = 0; i < block.Operations.Count; i++)
        {
            block.Operations[i] = SubstituteSiDiInOperation(block.Operations[i], aliases);
        }

        if (block.Condition is not null)
        {
            block.Condition = SubstituteSiDiInExpr(block.Condition, aliases);
        }

        if (block.EndStack.Count > 0)
        {
            var endStack = block.EndStack.ToArray();
            for (var i = 0; i < endStack.Length; i++)
            {
                endStack[i] = SubstituteSiDiInExpr(endStack[i], aliases);
            }

            block.EndStack = new Stack<Expr>(endStack.Reverse());
        }
    }

    private static Operation SubstituteSiDiInOperation(
        Operation operation,
        IReadOnlyDictionary<Variable, Variable> aliases) =>
        operation switch
        {
            SetOperation set => new SetOperation(
                SubstituteSiDiInExpr(set.Dst, aliases),
                SubstituteSiDiInExpr(set.Src, aliases)),
            StoreOperation store => new StoreOperation(
                SubstituteSiDiInExpr(store.Address, aliases),
                store.Segment is null ? null : SubstituteSiDiInExpr(store.Segment, aliases),
                SubstituteSiDiInExpr(store.Value, aliases)),
            IncOperation inc => new IncOperation(
                SubstituteSiDiInExpr(inc.Target, aliases),
                inc.Segment is null ? null : SubstituteSiDiInExpr(inc.Segment, aliases)),
            DecOperation dec => new DecOperation(
                SubstituteSiDiInExpr(dec.Target, aliases),
                dec.Segment is null ? null : SubstituteSiDiInExpr(dec.Segment, aliases)),
            AddAssignOperation add => new AddAssignOperation(
                SubstituteSiDiInExpr(add.Target, aliases),
                SubstituteSiDiInExpr(add.Value, aliases),
                add.Segment is null ? null : SubstituteSiDiInExpr(add.Segment, aliases)),
            SubAssignOperation sub => new SubAssignOperation(
                SubstituteSiDiInExpr(sub.Target, aliases),
                SubstituteSiDiInExpr(sub.Value, aliases),
                sub.Segment is null ? null : SubstituteSiDiInExpr(sub.Segment, aliases)),
            ReturnOperation ret => new ReturnOperation(
                ret.Value is null ? null : SubstituteSiDiInExpr(ret.Value, aliases),
                ret.IsExplicit),
            CallOperation call => new CallOperation(
                call.Name,
                call.Args.Select(arg => SubstituteSiDiInExpr(arg, aliases)).ToList()),
            _ => operation,
        };

    private static Expr SubstituteSiDiInExpr(Expr expr, IReadOnlyDictionary<Variable, Variable> aliases)
    {
        if (expr is VariableExpr { Var: var variable } &&
            aliases.TryGetValue(variable, out var replacement))
        {
            return replacement.ToGet();
        }

        return expr switch
        {
            ConstExpr or CharConstExpr or StringExpr or ImageOffsetExpr => expr,
            VariableExpr => expr,
            MemberExpr member => member with { Base = SubstituteSiDiInExpr(member.Base, aliases) },
            IncDecExpr inc => inc with { Operand = SubstituteSiDiInExpr(inc.Operand, aliases) },
            AddressOfExpr addr => addr with { Operand = SubstituteSiDiInExpr(addr.Operand, aliases) },
            Math1Expr math1 => math1 with { Op = SubstituteSiDiInExpr(math1.Op, aliases) },
            Math2Expr math2 => math2 with
            {
                First = SubstituteSiDiInExpr(math2.First, aliases),
                Second = SubstituteSiDiInExpr(math2.Second, aliases),
            },
            MemExpr mem => mem with
            {
                Address = SubstituteSiDiInExpr(mem.Address, aliases),
                Segment = mem.Segment is null ? null : SubstituteSiDiInExpr(mem.Segment, aliases),
            },
            CmpExpr cmp => cmp with
            {
                Left = SubstituteSiDiInExpr(cmp.Left, aliases),
                Right = SubstituteSiDiInExpr(cmp.Right, aliases),
            },
            CallExpr call => call with
            {
                Args = call.Args.Select(arg => SubstituteSiDiInExpr(arg, aliases)).ToList(),
            },
            LongExpr longExpr => longExpr with
            {
                Low = SubstituteSiDiInExpr(longExpr.Low, aliases),
                High = SubstituteSiDiInExpr(longExpr.High, aliases),
            },
            _ => expr,
        };
    }

    private static void RemoveTautologicalSets(ExprBlock block)
    {
        block.Operations.RemoveAll(static operation =>
        {
            if (operation is not SetOperation set ||
                !AssignmentTarget.TryGetVariable(set.Dst, out var dstVar) ||
                set.Src is not VariableExpr { Var: var srcVar })
            {
                return false;
            }

            return ReferenceEquals(dstVar, srcVar);
        });
    }
}
