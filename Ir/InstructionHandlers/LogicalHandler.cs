using UltraDecompiler.Decompilation;
using UltraDecompiler.Ir.Expressions;
using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает побитовые логические операции: AND, OR, XOR.
/// 
/// Логика аналогична арифметике, но:
/// - Использует Math2Operation.And/Or/Xor
/// - На x86 эти операции всегда сбрасывают CF и OF в 0.
/// 
/// Специальная оптимизация:
/// - XOR reg, reg → результат всегда 0
/// </summary>
public class LogicalHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        var dst = instr.Operand1;
        var srcExpr = instr.Operand2.GetExpression(block, instr.Segment);
        var dstCurrent = dst.GetExpression(block, instr.Segment);

        var op = instr.Mnemonic switch
        {
            Mnemonic.AND => Math2Operation.And,
            Mnemonic.OR => Math2Operation.Or,
            Mnemonic.XOR => Math2Operation.Xor,
            _ => throw new InvalidOperationException($"Unexpected logical mnemonic: {instr.Mnemonic}")
        };

        // Специальная обработка: XOR reg, reg  →  результат всегда 0
        if (instr.Mnemonic == Mnemonic.XOR && dst.ReferToSameLocation(instr.Operand2))
        {
            if (dst.Type == OperandType.Register16)
                block.EndRegisters = block.EndRegisters.Set16(dst.AsGpRegister16(), ConstExpr.Zero);
            else if (dst.Type == OperandType.Register8)
                block.EndRegisters = block.EndRegisters.Set8(dst.AsGpRegister8(), ConstExpr.Zero);
            else if (dst.Type == OperandType.Memory)
            {
                dst.EmitStore(block, instr.Segment, ConstExpr.Zero);
            }

            block.EndRegisters = block.EndRegisters with { CF = ConstExpr.Zero, OF = ConstExpr.Zero };
            block.EndRegisters = block.EndRegisters.ApplyArithmeticFlags(ConstExpr.Zero);
            return;
        }

        Expr result = dstCurrent.Calculate(op, srcExpr);

        if (result is not ConstExpr)
        {
            var resultVar = block.Variables.CreateTempVariable();
            block.Operations.Add(new SetOperation(resultVar, result));
            result = resultVar;
        }

        if (dst.Type == OperandType.Register16)
        {
            block.EndRegisters = block.EndRegisters.Set16(dst.AsGpRegister16(), result);
        }
        else if (dst.Type == OperandType.Register8)
        {
            block.EndRegisters = block.EndRegisters.Set8(dst.AsGpRegister8(), result);
        }
        else if (dst.Type == OperandType.Memory)
        {
            dst.EmitStore(block, instr.Segment, result);
        }
        else
        {
            throw new NotImplementedException($"Logical {instr.Mnemonic} with destination {dst.Type} is not supported");
        }

        block.EndRegisters = block.EndRegisters.ApplyArithmeticFlags(result);

        // На реальном x86 AND, OR, XOR сбрасывают Carry и Overflow
        block.EndRegisters = block.EndRegisters with
        {
            CF = ConstExpr.Zero,
            OF = ConstExpr.Zero
        };
    }
}
