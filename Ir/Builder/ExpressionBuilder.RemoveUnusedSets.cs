using UltraDecompiler.Ir.Variables;

namespace UltraDecompiler.Ir.Builder;

public partial class ExpressionBuilder
{
    /// <summary>
    /// Удаляет неиспользуемые присваивания по живости по CFG (Next / ConditionalBlock).
    /// Повторяет, пока удаляются мёртвые <see cref="SetOperation"/>.
    /// </summary>
    private void RemoveUnusedSets()
    {
        if (Blocks.Count == 0)
        {
            return;
        }

        while (RemoveUnusedSetsOnce())
        {
        }
    }

    /// <returns><see langword="true"/>, если хотя бы одна операция была удалена или заменена.</returns>
    private bool RemoveUnusedSetsOnce()
    {
        var liveOut = ComputeLiveOut();
        var changed = false;

        foreach (var block in Blocks)
        {
            if (RemoveUnusedSetsInBlock(block, liveOut[block]))
            {
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Живость по CFG: liveOut[b] = liveIn[next] ∪ liveIn[conditional].
    /// </summary>
    private Dictionary<ExprBlock, HashSet<Variable>> ComputeLiveOut()
    {
        var gen = Blocks.ToDictionary(static block => block, CollectBlockGen);
        var kill = Blocks.ToDictionary(static block => block, CollectBlockKill);
        var liveOut = Blocks.ToDictionary(static block => block, static _ => new HashSet<Variable>());
        var liveIn = Blocks.ToDictionary(static block => block, static _ => new HashSet<Variable>());

        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var block in Blocks)
            {
                var newLiveOut = new HashSet<Variable>();
                if (block.Next is not null)
                {
                    newLiveOut.UnionWith(liveIn[block.Next]);
                }

                if (block.ConditionalBlock is not null)
                {
                    newLiveOut.UnionWith(liveIn[block.ConditionalBlock]);
                }

                if (!newLiveOut.SetEquals(liveOut[block]))
                {
                    liveOut[block] = newLiveOut;
                    changed = true;
                }

                var newLiveIn = new HashSet<Variable>(gen[block]);
                newLiveIn.UnionWith(liveOut[block]);
                newLiveIn.ExceptWith(kill[block]);

                if (!newLiveIn.SetEquals(liveIn[block]))
                {
                    liveIn[block] = newLiveIn;
                    changed = true;
                }
            }
        }

        return liveOut;
    }

    /// <summary>
    /// Обратный проход по операциям с <paramref name="liveOut"/> на выходе блока.
    /// </summary>
    private static bool RemoveUnusedSetsInBlock(ExprBlock block, HashSet<Variable> liveOut)
    {
        var live = new HashSet<Variable>(liveOut);
        AddExprUses(live, block.Condition);

        foreach (var expr in block.EndStack)
        {
            AddExprUses(live, expr);
        }

        var changed = false;

        for (var i = block.Operations.Count - 1; i >= 0; i--)
        {
            if (block.Operations[i] is SetOperation set
                && AssignmentTarget.TryGetVariable(set.Dst, out var dstVar)
                && !dstVar.IsStack
                && !live.Contains(dstVar))
            {
                ReplaceOrRemoveDeadSet(block.Operations, i, set);
                changed = true;
                continue;
            }

            ApplyOperationToLive(block.Operations[i], live);
        }

        return changed;
    }

    private static void ReplaceOrRemoveDeadSet(List<Operation> operations, int index, SetOperation set)
    {
        if (set.Src is CallExpr call)
        {
            operations[index] = new CallOperation(call.Name, call.Args);
        }
        else
        {
            operations.RemoveAt(index);
        }
    }

    private static HashSet<Variable> CollectBlockGen(ExprBlock block)
    {
        var uses = new HashSet<Variable>();

        foreach (var expr in block.InitStack)
        {
            AddExprUses(uses, expr);
        }

        foreach (var operation in block.Operations)
        {
            AddOperationUses(uses, operation);
        }

        AddExprUses(uses, block.Condition);
        return uses;
    }

    private static HashSet<Variable> CollectBlockKill(ExprBlock block)
    {
        var defs = new HashSet<Variable>();

        foreach (var operation in block.Operations)
        {
            if (operation is SetOperation set && AssignmentTarget.TryGetVariable(set.Dst, out var variable))
            {
                defs.Add(variable);
            }
        }

        return defs;
    }

    private static void AddExprUses(HashSet<Variable> live, Expr? expr)
    {
        foreach (var variable in ExprSubstitution.CollectVariables(expr))
        {
            live.Add(variable);
        }
    }

    private static void ApplyOperationToLive(Operation operation, HashSet<Variable> live)
    {
        switch (operation)
        {
            case SetOperation set when AssignmentTarget.TryGetVariable(set.Dst, out var dstVar):
                live.Remove(dstVar);
                AddExprUses(live, set.Src);
                break;
            default:
                AddOperationUses(live, operation);
                break;
        }
    }

    private static void AddOperationUses(HashSet<Variable> live, Operation operation)
    {
        switch (operation)
        {
            case SetOperation set:
                AddExprUses(live, set.Dst);
                AddExprUses(live, set.Src);
                break;
            case CallOperation call:
                foreach (var arg in call.Args)
                {
                    AddExprUses(live, arg);
                }

                break;
            case StoreOperation store:
                AddExprUses(live, store.Address);
                AddExprUses(live, store.Segment);
                AddExprUses(live, store.Value);
                break;
            case IncOperation inc:
                AddExprUses(live, inc.Target);
                AddExprUses(live, inc.Segment);
                break;
            case DecOperation dec:
                AddExprUses(live, dec.Target);
                AddExprUses(live, dec.Segment);
                break;
            case AddAssignOperation add:
                AddExprUses(live, add.Target);
                AddExprUses(live, add.Segment);
                AddExprUses(live, add.Value);
                break;
            case SubAssignOperation sub:
                AddExprUses(live, sub.Target);
                AddExprUses(live, sub.Segment);
                AddExprUses(live, sub.Value);
                break;
            case ReturnOperation ret:
                AddExprUses(live, ret.Value);
                break;
        }
    }
}
