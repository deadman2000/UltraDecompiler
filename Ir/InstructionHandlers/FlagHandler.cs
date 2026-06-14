using UltraDecompiler.Decompilation;
using UltraDecompiler.Ir.Expressions;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает флаговые инструкции:
/// - CLI / STI → вызов _disable() / _enable()
/// - CLD / STD → установка DF = 0 / 1
/// - CLC / STC → установка CF = 0 / 1
/// - CMC → инверсия CF
/// </summary>
public class FlagHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        switch (instr.Mnemonic)
        {
            case Mnemonic.CLI:
                // CLI → _disable() (отключение аппаратных прерываний)
                block.Operations.Add(new CallOperation("_disable", []));
                break;

            case Mnemonic.STI:
                // STI → _enable() (включение аппаратных прерываний)
                block.Operations.Add(new CallOperation("_enable", []));
                break;

            case Mnemonic.CLD:
                // DF = 0 → инкремент SI/DI при строковых операциях
                block.EndRegisters = block.EndRegisters with { DF = ConstExpr.Zero };
                break;

            case Mnemonic.STD:
                // DF = 1 → декремент SI/DI при строковых операциях
                block.EndRegisters = block.EndRegisters with { DF = ConstExpr.One };
                break;

            case Mnemonic.CLC:
                block.EndRegisters = block.EndRegisters with { CF = ConstExpr.Zero };
                break;

            case Mnemonic.STC:
                block.EndRegisters = block.EndRegisters with { CF = ConstExpr.One };
                break;

            case Mnemonic.CMC:
                block.EndRegisters = block.EndRegisters with { CF = !block.EndRegisters.CF };
                break;

            default:
                throw new NotImplementedException($"Flag instruction {instr.Mnemonic} is not supported in FlagHandler");
        }
    }
}
