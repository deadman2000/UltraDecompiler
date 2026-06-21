using UltraDecompiler.CodeGeneration;

namespace UltraDecompiler.PostProcessing.Normalization;

/// <summary>
/// Р—Р°РјРµРЅСЏРµС‚ СЂРµРіРёСЃС‚СЂРѕРІС‹Рµ РїРµСЂРµРјРµРЅРЅС‹Рµ (<c>regAX</c>, <c>regBX</c> Рё С‚.Рґ.) РЅР° РѕР±С‹С‡РЅС‹Рµ
/// СЃС‚РµРєРѕРІС‹Рµ Р»РѕРєР°Р»Рё, РєРѕС‚РѕСЂС‹Рµ Р±СѓРґСѓС‚ РѕР±СЉСЏРІР»РµРЅС‹ РІ СЃРіРµРЅРµСЂРёСЂРѕРІР°РЅРЅРѕРј C-РєРѕРґРµ.
/// </summary>
public static class RegisterVariableEliminator
{
    /// <summary>РџСЂРёРјРµРЅСЏРµС‚ Р·Р°РјРµРЅСѓ РєРѕ РІСЃРµРј РѕРїРµСЂР°С†РёСЏРј РїСЂРѕС†РµРґСѓСЂС‹.</summary>
    public static IReadOnlyList<Operation> Eliminate(
        IReadOnlyList<Operation> operations,
        VariableStorage variables) =>
        EliminateList(operations.ToList(), variables);

    private static List<Operation> EliminateList(
        List<Operation> operations,
        VariableStorage variables)
    {
        // РЎРѕР±РёСЂР°РµРј РІСЃРµ СЂРµРіРёСЃС‚СЂРѕРІС‹Рµ РїРµСЂРµРјРµРЅРЅС‹Рµ, РєРѕС‚РѕСЂС‹Рµ РІСЃС‚СЂРµС‡Р°СЋС‚СЃСЏ РІ РѕРїРµСЂР°С†РёСЏС…
        var registerVars = CollectRegisterVariables(operations);

        if (registerVars.Count == 0)
        {
            return operations;
        }

        // РЎРѕР·РґР°С‘Рј СЃС‚РµРєРѕРІС‹Рµ Р»РѕРєР°Р»Рё РІРјРµСЃС‚Рѕ СЂРµРіРёСЃС‚СЂРѕРІС‹С…
        var replacements = new Dictionary<Variable, Variable>(ReferenceEqualityComparer.Instance);
        foreach (var regVar in registerVars)
        {
            var local = variables.CreateStackVariable();
            replacements[regVar] = local;
        }

        // РџСЂРёРјРµРЅСЏРµРј Р·Р°РјРµРЅС‹ РєРѕ РІСЃРµРј РѕРїРµСЂР°С†РёСЏРј
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = SubstituteInOperation(operations[i], replacements);
        }

        // Р РµРєСѓСЂСЃРёРІРЅРѕ РѕР±СЂР°Р±Р°С‚С‹РІР°РµРј РІР»РѕР¶РµРЅРЅС‹Рµ СЃС‚СЂСѓРєС‚СѓСЂС‹
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = SubstituteNested(operations[i], replacements);
        }

        return operations;
    }

    private static HashSet<Variable> CollectRegisterVariables(IEnumerable<Operation> operations)
    {
        var result = new HashSet<Variable>(ReferenceEqualityComparer.Instance);

        foreach (var op in OperationFlattener.EnumerateNested(operations))
        {
            CollectFromOperation(op, result);
        }

        return result;
    }

    private static void CollectFromOperation(Operation op, HashSet<Variable> result)
    {
        switch (op)
        {
            case SetOperation set:
                CollectFromExpr(set.Dst, result);
                CollectFromExpr(set.Src, result);
                break;
            case StoreOperation store:
                CollectFromExpr(store.Address, result);
                CollectFromExpr(store.Segment, result);
                CollectFromExpr(store.Value, result);
                break;
            case IncOperation inc:
                CollectFromExpr(inc.Target, result);
                CollectFromExpr(inc.Segment, result);
                break;
            case DecOperation dec:
                CollectFromExpr(dec.Target, result);
                CollectFromExpr(dec.Segment, result);
                break;
            case AddAssignOperation add:
                CollectFromExpr(add.Target, result);
                CollectFromExpr(add.Segment, result);
                CollectFromExpr(add.Value, result);
                break;
            case SubAssignOperation sub:
                CollectFromExpr(sub.Target, result);
                CollectFromExpr(sub.Segment, result);
                CollectFromExpr(sub.Value, result);
                break;
            case CallOperation call:
                foreach (var arg in call.Args)
                {
                    CollectFromExpr(arg, result);
                }

                break;
            case ReturnOperation ret:
                CollectFromExpr(ret.Value, result);
                break;
            case WhileOperation loop:
                CollectFromExpr(loop.Condition, result);
                break;
            case DoWhileOperation loop:
                CollectFromExpr(loop.Condition, result);
                break;
            case ForOperation loop:
                CollectFromExpr(loop.Condition, result);
                if (loop.Init is not null)
                {
                    CollectFromOperation(loop.Init, result);
                }

                if (loop.Iteration is not null)
                {
                    CollectFromOperation(loop.Iteration, result);
                }

                break;
            case IfOperation branch:
                CollectFromExpr(branch.Condition, result);
                break;
            case SwitchOperation sw:
                CollectFromExpr(sw.Discriminant, result);
                break;
        }
    }

    private static void CollectFromExpr(Expr? expr, HashSet<Variable> result)
    {
        if (expr is null)
        {
            return;
        }

        if (expr is VariableExpr { Var: var variable } && variable.IsRegister)
        {
            result.Add(variable);
            return;
        }

        switch (expr)
        {
            case MemberExpr member:
                CollectFromExpr(member.Base, result);
                break;
            case AddressOfExpr addr:
                CollectFromExpr(addr.Operand, result);
                break;
            case IncDecExpr inc:
                CollectFromExpr(inc.Operand, result);
                break;
            case Math1Expr m1:
                CollectFromExpr(m1.Op, result);
                break;
            case Math2Expr m2:
                CollectFromExpr(m2.First, result);
                CollectFromExpr(m2.Second, result);
                break;
            case MemExpr mem:
                CollectFromExpr(mem.Address, result);
                CollectFromExpr(mem.Segment, result);
                break;
            case CmpExpr cmp:
                CollectFromExpr(cmp.Left, result);
                CollectFromExpr(cmp.Right, result);
                break;
            case CallExpr call:
                foreach (var arg in call.Args)
                {
                    CollectFromExpr(arg, result);
                }

                break;
            case LongExpr longExpr:
                CollectFromExpr(longExpr.Low, result);
                CollectFromExpr(longExpr.High, result);
                break;
            case SyntheticLoadExpr synthetic:
                CollectFromVariable(synthetic.Array, result);
                CollectFromVariable(synthetic.Index, result);
                break;
        }
    }

    private static void CollectFromVariable(Variable? variable, HashSet<Variable> result)
    {
        if (variable is not null && variable.IsRegister)
        {
            result.Add(variable);
        }
    }

    private static Operation SubstituteInOperation(
        Operation operation,
        IDictionary<Variable, Variable> replacements) =>
        operation switch
        {
            SetOperation set => new SetOperation(
                SubstituteExpr(set.Dst, replacements),
                SubstituteExpr(set.Src, replacements)),
            StoreOperation store => new StoreOperation(
                SubstituteExpr(store.Address, replacements),
                store.Segment is null ? null : SubstituteExpr(store.Segment, replacements),
                SubstituteExpr(store.Value, replacements)),
            IncOperation inc => new IncOperation(
                SubstituteExpr(inc.Target, replacements),
                inc.Segment is null ? null : SubstituteExpr(inc.Segment, replacements)),
            DecOperation dec => new DecOperation(
                SubstituteExpr(dec.Target, replacements),
                dec.Segment is null ? null : SubstituteExpr(dec.Segment, replacements)),
            AddAssignOperation add => new AddAssignOperation(
                SubstituteExpr(add.Target, replacements),
                SubstituteExpr(add.Value, replacements),
                add.Segment is null ? null : SubstituteExpr(add.Segment, replacements)),
            SubAssignOperation sub => new SubAssignOperation(
                SubstituteExpr(sub.Target, replacements),
                SubstituteExpr(sub.Value, replacements),
                sub.Segment is null ? null : SubstituteExpr(sub.Segment, replacements)),
            ReturnOperation ret => new ReturnOperation(
                ret.Value is null ? null : SubstituteExpr(ret.Value, replacements),
                ret.IsExplicit),
            IfOperation branch => new IfOperation(
                SubstituteExpr(branch.Condition, replacements),
                branch.ThenBody.Select(op => SubstituteInOperation(op, replacements)).ToList(),
                branch.ElseBody?.Select(op => SubstituteInOperation(op, replacements)).ToList()),
            WhileOperation loop => new WhileOperation(
                SubstituteExpr(loop.Condition, replacements),
                loop.Body.Select(op => SubstituteInOperation(op, replacements)).ToList()),
            DoWhileOperation loop => new DoWhileOperation(
                SubstituteExpr(loop.Condition, replacements),
                loop.Body.Select(op => SubstituteInOperation(op, replacements)).ToList()),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? SubstituteInOperation(loop.Init, replacements) : null,
                loop.Condition is null ? null : SubstituteExpr(loop.Condition, replacements),
                loop.Iteration is not null ? SubstituteInOperation(loop.Iteration, replacements) : null,
                loop.Body.Select(op => SubstituteInOperation(op, replacements)).ToList()),
            SwitchOperation sw => new SwitchOperation(
                SubstituteExpr(sw.Discriminant, replacements),
                sw.Cases.Select(c => new SwitchCase(
                    c.Value,
                    c.Body.Select(op => SubstituteInOperation(op, replacements)).ToList())).ToList()),
            CallOperation call => new CallOperation(
                call.Name,
                call.Args.Select(arg => SubstituteExpr(arg, replacements)).ToList()),
            _ => operation,
        };

    private static Operation SubstituteNested(
        Operation operation,
        IDictionary<Variable, Variable> replacements) =>
        operation switch
        {
            IfOperation branch => new IfOperation(
                branch.Condition,
                SubstituteList(branch.ThenBody.ToList(), replacements),
                branch.ElseBody is not null ? SubstituteList(branch.ElseBody.ToList(), replacements) : null),
            WhileOperation loop => new WhileOperation(
                loop.Condition,
                SubstituteList(loop.Body.ToList(), replacements)),
            DoWhileOperation loop => new DoWhileOperation(
                loop.Condition,
                SubstituteList(loop.Body.ToList(), replacements)),
            ForOperation loop => new ForOperation(
                loop.Init is not null ? SubstituteInOperation(loop.Init, replacements) : null,
                loop.Condition,
                loop.Iteration is not null ? SubstituteInOperation(loop.Iteration, replacements) : null,
                SubstituteList(loop.Body.ToList(), replacements)),
            SwitchOperation sw => new SwitchOperation(
                sw.Discriminant,
                sw.Cases.Select(c => new SwitchCase(
                    c.Value,
                    SubstituteList(c.Body.ToList(), replacements))).ToList()),
            _ => operation,
        };

    private static List<Operation> SubstituteList(
        List<Operation> operations,
        IDictionary<Variable, Variable> replacements)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            operations[i] = SubstituteInOperation(operations[i], replacements);
        }

        return operations;
    }

    private static Expr SubstituteExpr(Expr expr, IDictionary<Variable, Variable> replacements)
    {
        if (expr is VariableExpr { Var: var variable } && replacements.TryGetValue(variable, out var replacement))
        {
            return replacement.ToGet();
        }

        return expr switch
        {
            VariableExpr => expr,
            MemberExpr member => member with { Base = SubstituteExpr(member.Base, replacements) },
            IncDecExpr inc => inc with { Operand = SubstituteExpr(inc.Operand, replacements) },
            AddressOfExpr addr => addr with { Operand = SubstituteExpr(addr.Operand, replacements) },
            Math1Expr m => m with { Op = SubstituteExpr(m.Op, replacements) },
            Math2Expr m => m with
            {
                First = SubstituteExpr(m.First, replacements),
                Second = SubstituteExpr(m.Second, replacements),
            },
            MemExpr mem => mem with
            {
                Address = SubstituteExpr(mem.Address, replacements),
                Segment = mem.Segment is null ? null : SubstituteExpr(mem.Segment, replacements),
            },
            CmpExpr cmp => cmp with
            {
                Left = SubstituteExpr(cmp.Left, replacements),
                Right = SubstituteExpr(cmp.Right, replacements),
            },
            CallExpr call => call with
            {
                Args = call.Args.Select(arg => SubstituteExpr(arg, replacements)).ToList(),
            },
            LongExpr longExpr => longExpr with
            {
                Low = SubstituteExpr(longExpr.Low, replacements),
                High = SubstituteExpr(longExpr.High, replacements),
            },
            _ => expr,
        };
    }
}
