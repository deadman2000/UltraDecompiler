using System.Text;

namespace UltraDecompiler.CodeGeneration.Rendering;

/// <summary>Рендеринг <see cref="Expr"/> в синтаксис C.</summary>
public static class CExprRenderer
{
    public static string Render(Expr expr, int parentPrec = 0) =>
        expr switch
        {
            VariableExpr { Var: var v } => RenderVariable(v),
            MemberExpr m => $"{Render(m.Base, 0)}.{m.FieldName}",
            IncDecExpr inc => RenderIncDec(inc, parentPrec),
            AddressOfExpr addr => RenderAddressOf(addr, parentPrec),
            ConstExpr c => c.Value.ToString(),
            ImageOffsetExpr io => $"{io.BaseName} + 0x{io.Value:X4}",
            CharConstExpr ch => RenderCharConst(ch),
            StringExpr s => RenderStringLiteral(s.Value),
            MemExpr mem => RenderMem(mem, parentPrec),
            Math1Expr m1 => RenderMath1(m1, parentPrec),
            Math2Expr m2 => RenderMath2(m2, parentPrec),
            CallExpr call => RenderCall(call),
            CmpExpr cmp => RenderCmp(cmp, parentPrec),
            SyntheticLoadExpr synthetic => synthetic.Text,
            _ => expr.ToString(),
        };

    private static string RenderVariable(Variable v)
    {
        if (v.Name is not null)
            return v.Name;

        if (v.IsStack || v.IsTemp)
            return v.IsTemp ? $"temp{v.Number}" : $"var{v.Number}";

        return $"var{v.Number}";
    }

    private static string RenderIncDec(IncDecExpr inc, int parentPrec)
    {
        var operandPrec = inc.Kind is IncDecKind.PreInc or IncDecKind.PreDec ? Prec.Unary : Prec.Postfix;
        var operandStr = Render(inc.Operand, operandPrec);
        return inc.Kind switch
        {
            IncDecKind.PreInc => $"++{operandStr}",
            IncDecKind.PostInc => $"{operandStr}++",
            IncDecKind.PreDec => $"--{operandStr}",
            IncDecKind.PostDec => $"{operandStr}--",
            _ => throw new InvalidOperationException(),
        };
    }

    private static string RenderAddressOf(AddressOfExpr addr, int parentPrec)
    {
        var operandStr = Render(addr.Operand, addr.GetPrecedence());
        var result = parentPrec >= addr.GetPrecedence() ? $"(&{operandStr})" : $"&{operandStr}";
        return result;
    }

    private static string RenderCharConst(CharConstExpr ch) => ch.Value switch
    {
        '\\' => "'\\\\'",
        '\'' => "'\\''",
        '\n' => "'\\n'",
        '\r' => "'\\r'",
        '\t' => "'\\t'",
        _ => $"'{ch.Value}'",
    };

    private static string RenderStringLiteral(string s)
    {
        var sb = new StringBuilder();
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c >= 32 && c < 127)
                        sb.Append(c);
                    else
                        sb.Append($"\\x{(int)c:X2}");
                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    private static string RenderMem(MemExpr mem, int parentPrec)
    {
        if (PointerDerefFormatter.TryFormatLoad(mem, out var deref))
            return deref;

        string addrStr = Render(mem.Address, 0);
        string segPrefix = mem.Segment is null ? "" : $"{Render(mem.Segment, Prec.Atom)}:";
        string result = $"{segPrefix}[{addrStr}]";

        int myPrecMem = mem.GetPrecedence();
        if (parentPrec > 0 && (myPrecMem < parentPrec || (myPrecMem > parentPrec && myPrecMem < Prec.Atom)))
            result = $"({result})";

        return result;
    }

    private static string RenderMath1(Math1Expr m1, int parentPrec)
    {
        string opSym = m1.Operation switch
        {
            Math1Operation.Neg => "-",
            Math1Operation.Not => m1.Op is CmpExpr ? "!" : "~",
            _ => throw new NotImplementedException(),
        };

        string operandStr = Render(m1.Op, m1.GetPrecedence());
        string result = $"{opSym}{operandStr}";

        int myPrec = m1.GetPrecedence();
        if (parentPrec > 0 && (myPrec < parentPrec || (myPrec > parentPrec && myPrec < Prec.Atom)))
            result = $"({result})";

        return result;
    }

    private static string RenderMath2(Math2Expr m2, int parentPrec)
    {
        int myPrec = m2.GetPrecedence();

        string opSym = m2.Operation switch
        {
            Math2Operation.Add => "+",
            Math2Operation.Sub => "-",
            Math2Operation.Shl => "<<",
            Math2Operation.Shr => ">>",
            Math2Operation.And when LooksLikeBooleanAnd(m2.First, m2.Second) => "&&",
            Math2Operation.And => "&",
            Math2Operation.Or => "|",
            Math2Operation.Xor => "^",
            Math2Operation.Mul => "*",
            Math2Operation.Div => "/",
            Math2Operation.Mod => "%",
            _ => throw new NotImplementedException(),
        };

        string leftStr = Render(m2.First, myPrec);
        string rightStr = Render(m2.Second, myPrec + 1);
        string result = $"{leftStr} {opSym} {rightStr}";

        if (parentPrec > 0 && (myPrec < parentPrec || (myPrec > parentPrec && myPrec < Prec.Atom)))
            result = $"({result})";

        return result;
    }

    private static string RenderCall(CallExpr call)
    {
        var args = string.Join(", ", call.Args.Select(a => Render(a, 0)));
        return $"{call.Name}({args})";
    }

    private static string RenderCmp(CmpExpr cmp, int parentPrec)
    {
        int myPrec = cmp.GetPrecedence();

        string opSym = cmp.Operation switch
        {
            CmpOperation.Eq => "==",
            CmpOperation.Ne => "!=",
            CmpOperation.Ult => "<",
            CmpOperation.Ule => "<=",
            CmpOperation.Ugt => ">",
            CmpOperation.Uge => ">=",
            _ => throw new NotImplementedException(),
        };

        string leftStr = Render(cmp.Left, myPrec);
        string rightStr = Render(cmp.Right, myPrec + 1);
        string result = $"{leftStr} {opSym} {rightStr}";

        if (parentPrec > 0 && (myPrec < parentPrec || (myPrec > parentPrec && myPrec < Prec.Atom)))
            result = $"({result})";

        return result;
    }

    private static bool LooksLikeBooleanAnd(Expr left, Expr right) =>
        IsBooleanLike(left) && IsBooleanLike(right);

    private static bool IsBooleanLike(Expr expr) =>
        expr is CmpExpr or Math2Expr { Operation: Math2Operation.And };

    private static class Prec
    {
        public const int Atom = 100;
        public const int Postfix = 16;
        public const int Unary = 15;
        public const int MulDiv = 14;
        public const int AddSub = 13;
        public const int Shift = 12;
        public const int Compare = 11;
        public const int BitAnd = 9;
        public const int BitXor = 8;
        public const int BitOr = 7;
    }
}
