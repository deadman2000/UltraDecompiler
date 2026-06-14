namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Строит высокоуровневые условия для Jcc сразу после CMP по операндам сравнения.
/// </summary>
internal static class CmpJumpConditions
{
    /// <summary>
    /// Если предыдущая инструкция — CMP, возвращает сравнение операндов для данного Jcc.
    /// </summary>
    public static bool TryBuild(ExprBlock block, Mnemonic mnemonic, out Expr? condition)
    {
        condition = null;
        if (block.PreviousMnemonic != Mnemonic.CMP || block.LastComparisonOperands is not { } cmp)
        {
            return false;
        }

        condition = Map(mnemonic, cmp.Left, cmp.Right);
        return condition is not null;
    }

    private static Expr? Map(Mnemonic mnemonic, Expr left, Expr right) =>
        mnemonic switch
        {
            Mnemonic.JE => new CmpExpr(CmpOperation.Eq, left, right),
            Mnemonic.JNE => new CmpExpr(CmpOperation.Ne, left, right),
            Mnemonic.JB => new CmpExpr(CmpOperation.Ult, left, right),
            Mnemonic.JAE => new CmpExpr(CmpOperation.Uge, left, right),
            Mnemonic.JBE => new CmpExpr(CmpOperation.Ule, left, right),
            Mnemonic.JA => new CmpExpr(CmpOperation.Ugt, left, right),
            Mnemonic.JL => new CmpExpr(CmpOperation.Ult, left, right),
            Mnemonic.JGE => new CmpExpr(CmpOperation.Uge, left, right),
            Mnemonic.JLE => new CmpExpr(CmpOperation.Ule, left, right),
            Mnemonic.JG => new CmpExpr(CmpOperation.Ugt, left, right),
            _ => null,
        };
}
