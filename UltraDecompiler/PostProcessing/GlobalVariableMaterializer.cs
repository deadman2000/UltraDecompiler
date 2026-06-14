using UltraDecompiler.Decompilation;
using UltraDecompiler.Parser;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Заменяет обращения к near-DGROUP по константному смещению (<c>_psp:[NN]</c>) на именованные глобалы.
/// </summary>
public static class GlobalVariableMaterializer
{
    private static readonly HashSet<int> PspKnownOffsets = new()
    {
        0x02, 0x2C, 0x80, 0x81,
    };

    /// <summary>
    /// Материализует глобалы во всех операциях процедуры, используя общий <paramref name="registry"/>.
    /// </summary>
    public static IReadOnlyList<Operation> Materialize(
        IReadOnlyList<Operation> operations,
        GlobalVariableRegistry registry,
        byte[] image,
        ExeImageLayout layout)
    {
        var list = operations.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            list[i] = MaterializeNested(list[i], registry, image, layout);
        }

        return list;
    }

    private static Operation MaterializeNested(
        Operation operation,
        GlobalVariableRegistry registry,
        byte[] image,
        ExeImageLayout layout) =>
        operation switch
        {
            SetOperation set => new SetOperation(
                MaterializeExpr(set.Dst, registry, image, layout),
                MaterializeExpr(set.Src, registry, image, layout)),
            StoreOperation store when TryGetGlobal(store.Address, store.Segment, registry, image, layout, out var global) =>
                new SetOperation(global, MaterializeExpr(store.Value, registry, image, layout)),
            StoreOperation store => store with
            {
                Address = MaterializeExpr(store.Address, registry, image, layout),
                Segment = store.Segment is null ? null : MaterializeExpr(store.Segment, registry, image, layout),
                Value = MaterializeExpr(store.Value, registry, image, layout),
            },
            IncOperation inc when TryGetGlobal(inc.Target, inc.Segment, registry, image, layout, out var global) =>
                new IncOperation(global),
            DecOperation dec when TryGetGlobal(dec.Target, dec.Segment, registry, image, layout, out var global) =>
                new DecOperation(global),
            IncOperation inc => inc with
            {
                Target = MaterializeExpr(inc.Target, registry, image, layout),
                Segment = inc.Segment is null ? null : MaterializeExpr(inc.Segment, registry, image, layout),
            },
            DecOperation dec => dec with
            {
                Target = MaterializeExpr(dec.Target, registry, image, layout),
                Segment = dec.Segment is null ? null : MaterializeExpr(dec.Segment, registry, image, layout),
            },
            CallOperation call => call with
            {
                Args = call.Args.Select(arg => MaterializeExpr(arg, registry, image, layout)).ToList(),
            },
            ReturnOperation ret => ret with
            {
                Value = ret.Value is null ? null : MaterializeExpr(ret.Value, registry, image, layout),
            },
            IfOperation branch => new IfOperation(
                MaterializeExpr(branch.Condition, registry, image, layout),
                Materialize(branch.ThenBody, registry, image, layout),
                branch.ElseBody is null
                    ? null
                    : Materialize(branch.ElseBody, registry, image, layout)),
            WhileOperation loop => new WhileOperation(
                MaterializeExpr(loop.Condition, registry, image, layout),
                Materialize(loop.Body, registry, image, layout)),
            ForOperation loop => new ForOperation(
                loop.Init is null ? null : MaterializeNested(loop.Init, registry, image, layout),
                loop.Condition is null ? null : MaterializeExpr(loop.Condition, registry, image, layout),
                loop.Iteration is null ? null : MaterializeNested(loop.Iteration, registry, image, layout),
                Materialize(loop.Body, registry, image, layout)),
            SwitchOperation sw => new SwitchOperation(
                MaterializeExpr(sw.Discriminant, registry, image, layout),
                sw.Cases.Select(c => new SwitchCase(
                    c.Value,
                    Materialize(c.Body, registry, image, layout))).ToList()),
            _ => operation,
        };

    private static bool TryGetGlobal(
        Expr address,
        Expr? segment,
        GlobalVariableRegistry registry,
        byte[] image,
        ExeImageLayout layout,
        out Variable global)
    {
        global = null!;
        if (!IsGlobalCandidate(address, segment) || !TryGetNearOffset(address, out var nearOffset))
        {
            return false;
        }

        global = registry.GetOrCreate(nearOffset, image, layout);
        return true;
    }

    private static Expr MaterializeExpr(
        Expr expr,
        GlobalVariableRegistry registry,
        byte[] image,
        ExeImageLayout layout)
    {
        if (expr is MemExpr mem && TryGetGlobal(mem.Address, mem.Segment, registry, image, layout, out var global))
        {
            return global;
        }

        return expr switch
        {
            MemExpr otherMem => otherMem with
            {
                Address = MaterializeExpr(otherMem.Address, registry, image, layout),
                Segment = otherMem.Segment is null ? null : MaterializeExpr(otherMem.Segment, registry, image, layout),
            },
            Math1Expr m => m with { Op = MaterializeExpr(m.Op, registry, image, layout) },
            Math2Expr m => m with
            {
                First = MaterializeExpr(m.First, registry, image, layout),
                Second = MaterializeExpr(m.Second, registry, image, layout),
            },
            CmpExpr cmp => cmp with
            {
                Left = MaterializeExpr(cmp.Left, registry, image, layout),
                Right = MaterializeExpr(cmp.Right, registry, image, layout),
            },
            MemberExpr member => member with { Base = MaterializeExpr(member.Base, registry, image, layout) },
            AddressOfExpr addr => addr with { Operand = MaterializeExpr(addr.Operand, registry, image, layout) },
            IncDecExpr inc => inc with { Operand = MaterializeExpr(inc.Operand, registry, image, layout) },
            CallExpr call => call with
            {
                Args = call.Args.Select(arg => MaterializeExpr(arg, registry, image, layout)).ToList(),
            },
            _ => expr,
        };
    }

    private static bool IsGlobalCandidate(Expr address, Expr? segment)
    {
        if (!PointerDerefFormatter.IsNearDataSegment(segment))
        {
            return false;
        }

        if (!TryGetNearOffset(address, out var nearOffset))
        {
            return false;
        }

        return !PspKnownOffsets.Contains(nearOffset);
    }

    private static bool TryGetNearOffset(Expr address, out int nearOffset)
    {
        switch (address)
        {
            case ConstExpr constant:
                nearOffset = constant.Value;
                return true;
            case ImageOffsetExpr imageOffset:
                nearOffset = imageOffset.Value;
                return true;
            default:
                nearOffset = 0;
                return false;
        }
    }
}
