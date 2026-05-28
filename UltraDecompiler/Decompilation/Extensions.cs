namespace UltraDecompiler.Decompilation;

public static class Extensions
{
    extension(Expr expr)
    {
        /// <summary>
        /// Булево И с агрессивным constant folding (0/1) и упрощением Cmp.
        /// Предпочтительный способ использования — через перегрузку оператора &amp;.
        /// </summary>
        public Expr BoolAnd(Expr other)
        {
            if (expr is ConstExpr ca)
            {
                if (ca.Value == 0) return ConstExpr.Zero;
                if (ca.Value != 0) return other;
            }
            if (other is ConstExpr cb)
            {
                if (cb.Value == 0) return ConstExpr.Zero;
                if (cb.Value != 0) return expr;
            }
            return ExpressionBuilder.Calculate(Math2Operation.And, expr, other);
        }

        /// <summary>
        /// Булево ИЛИ с агрессивным constant folding (0/1) и упрощением Cmp.
        /// Предпочтительный способ использования — через перегрузку оператора |.
        /// </summary>
        public Expr BoolOr(Expr other)
        {
            if (expr is ConstExpr ca)
            {
                if (ca.Value != 0) return ConstExpr.One;
                if (ca.Value == 0) return other;
            }
            if (other is ConstExpr cb)
            {
                if (cb.Value != 0) return ConstExpr.One;
                if (cb.Value == 0) return expr;
            }
            return ExpressionBuilder.Calculate(Math2Operation.Or, expr, other);
        }

        /// <summary>
        /// Булево НЕ с агрессивным constant folding и инверсией известных CmpExpr
        /// (Eq↔Ne, Ult↔Uge и т.д.). Даёт более чистые условия.
        /// Предпочтительный способ использования — через перегрузку оператора !.
        /// </summary>
        public Expr BoolNot()
        {
            if (expr is ConstExpr c)
            {
                return c.Value == 0 ? ConstExpr.One : ConstExpr.Zero;
            }

            // Инверсия известных сравнений — даёт более чистые условия
            if (expr is CmpExpr cmp)
            {
                var invertedOp = cmp.Operation switch
                {
                    CmpOperation.Eq => CmpOperation.Ne,
                    CmpOperation.Ne => CmpOperation.Eq,
                    CmpOperation.Ult => CmpOperation.Uge,
                    CmpOperation.Uge => CmpOperation.Ult,
                    CmpOperation.Ule => CmpOperation.Ugt,
                    CmpOperation.Ugt => CmpOperation.Ule,
                    _ => (CmpOperation?)null
                };

                if (invertedOp.HasValue)
                {
                    return new CmpExpr(invertedOp.Value, cmp.Left, cmp.Right);
                }
            }

            return ExpressionBuilder.Calculate(Math1Operation.Not, expr);
        }

        /// <summary>
        /// Булево XOR с constant folding. Полезно для моделирования SF ^ OF.
        /// Предпочтительный способ использования — через перегрузку оператора ^.
        /// </summary>
        public Expr BoolXor(Expr other)
        {
            if (expr is ConstExpr ca)
            {
                if (ca.Value == 0) return other;
                if (ca.Value != 0) return !other;
            }
            if (other is ConstExpr cb)
            {
                if (cb.Value == 0) return expr;
                if (cb.Value != 0) return !expr;
            }
            return ExpressionBuilder.Calculate(Math2Operation.Xor, expr, other);
        }
    }

    extension(RegisterExpressions registers)
    {
        /// <summary>
        /// SF == OF (эквивалентность). Используется для JGE / JG / JLE.
        /// </summary>
        public Expr SfEqOf() => !(registers.SF ^ registers.OF);

        /// <summary>
        /// SF != OF. Используется для JL / JLE.
        /// </summary>
        public Expr SfNeOf() => registers.SF ^ registers.OF;
    }
}
