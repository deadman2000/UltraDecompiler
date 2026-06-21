using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает INC и DEC (инкремент/декремент на 1).
///
/// Для регистров и локалов по [BP+disp] создаёт <see cref="IncOperation"/> / <see cref="DecOperation"/>.
/// Для прочей памяти — через <see cref="Extensions.EmitIncDec"/>.
/// CF не изменяется (поведение 8086); обновляются ZF, SF, OF.
/// </summary>
public class IncDecHandler(bool isInc) : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var dest = instr.Operand1;
        var destExpr = dest.GetExpression(block, instr.Segment);
        var result = isInc
            ? destExpr.Calculate(Math2Operation.Add, ConstExpr.One)
            : destExpr.Calculate(Math2Operation.Sub, ConstExpr.One);

        switch (dest.Type)
        {
            case OperandType.Register16:
                EmitRegisterIncDec(block, dest.AsGpRegister16());
                break;

            case OperandType.Register8:
                block.Set(dest.AsGpRegister8(), result.LowByte());
                break;

            case OperandType.Memory:
                dest.EmitIncDec(block, instr.Segment, isInc);
                break;

            default:
                throw new NotImplementedException($"INC/DEC with destination type {dest.Type} is not yet supported");
        }

        UpdateFlags(block, destExpr, result, IsWordOperation(instr), isInc);
    }

    private void EmitRegisterIncDec(ExprBlock block, GpRegister16 reg)
    {
        var variable = block.Variables.Get(reg);
        block.Operations.Add(isInc ? new IncOperation(variable.ToSet()) : new DecOperation(variable.ToSet()));
    }

    private static bool IsWordOperation(Instruction instr) =>
        instr.Operand1.Type switch
        {
            OperandType.Register16 => true,
            OperandType.Register8 => false,
            _ => instr.Bytes.Contains((byte)0xFF),
        };

    /// <summary>
    /// Обновляет ZF, SF, OF. CF для INC/DEC не затрагивается.
    /// </summary>
    private static void UpdateFlags(ExprBlock block, Expr dest, Expr result, bool isWord, bool isInc)
    {
        block.Set(block.Variables.ZF, new CmpExpr(CmpOperation.Eq, result, ConstExpr.Zero));

        var signMask = new ConstExpr(isWord ? 0x8000 : 0x80);
        block.Set(block.Variables.SF, new CmpExpr(CmpOperation.Ne, result.Calculate(Math2Operation.And, signMask), ConstExpr.Zero));

        var overflowBound = new ConstExpr(isWord ? 0x7FFF : 0x7F);
        var underflowBound = new ConstExpr(isWord ? 0x8000 : 0x80);
        block.Set(block.Variables.OF, isInc
            ? new CmpExpr(CmpOperation.Eq, dest, overflowBound)
            : new CmpExpr(CmpOperation.Eq, dest, underflowBound));
    }
}
