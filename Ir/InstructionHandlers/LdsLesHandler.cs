using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.InstructionHandlers;

/// <summary>
/// Обрабатывает LDS и LES — загрузку far-указателя (DWORD) из памяти.
/// 
/// Младшее слово (offset) загружается в gp-регистр (Operand1).
/// Старшее слово (segment) загружается в DS (для LDS) или ES (для LES).
/// 
/// Сегментный префикс инструкции (если есть) применяется к адресу самого указателя в памяти.
/// 
/// Это относительно редкая инструкция, но важная для работы с far-указателями в DOS.
/// </summary>
public class LdsLesHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        if (instr.Operand2.Type != OperandType.Memory)
        {
            throw new NotImplementedException("LDS/LES с регистром в качестве источника не поддерживается (недопустимая кодировка на 8086)");
        }

        // Far-указатель на стеке: LES/LDS BX, [BP-4] загружает offset в BX и segment в ES/DS.
        if (instr.Operand2.BaseReg == AddressRegister.BP && instr.Operand2.IndexReg == AddressRegister.None)
        {
            var baseDisp = instr.Operand2.Value;
            var offsetVar = block.Variables.TryGetStackLocal(baseDisp);
            var segmentVar = block.Variables.TryGetStackLocal(baseDisp + 2);
            if (offsetVar is not null && segmentVar is not null)
            {
                block.Variables.RegisterFarPointerLocal(baseDisp, offsetVar, segmentVar);
                var segReg = instr.Mnemonic == Mnemonic.LDS ? CpuSegmentRegister.DS : CpuSegmentRegister.ES;
                block.EndRegisters = block.EndRegisters.Set16(instr.Operand1.AsGpRegister16(), offsetVar);
                block.EndRegisters = block.EndRegisters.SetSegment(segReg, segmentVar);
                return;
            }
        }

        // Адрес в памяти, по которому лежит far-указатель (DWORD: offset + segment)
        var (ptrAddr, ptrSeg) = instr.Operand2.BuildMemoryReference(block.EndRegisters, instr.Segment);

        // Младшее слово — offset, загружается в целевой gp-регистр
        var knownOffset = block.Variables.TryGetKnownMemoryVariable(ptrAddr, ptrSeg);
        Expr offsetExpr = knownOffset != null ? knownOffset : new MemExpr(ptrAddr, ptrSeg);

        // Старшее слово (+2) — значение сегмента
        Expr highAddr = ptrAddr.Calculate(Math2Operation.Add, new ConstExpr(2));
        var knownSegVal = block.Variables.TryGetKnownMemoryVariable(highAddr, ptrSeg);
        Expr segValue = knownSegVal != null ? knownSegVal : new MemExpr(highAddr, ptrSeg);

        // Загружаем offset в gp-регистр (аналогично MOV/LEA — без создания SetOperation)
        block.EndRegisters = block.EndRegisters.Set16(instr.Operand1.AsGpRegister16(), offsetExpr);

        // Выбираем целевой сегментный регистр
        var targetSegReg = instr.Mnemonic == Mnemonic.LDS ? CpuSegmentRegister.DS : CpuSegmentRegister.ES;
        block.EndRegisters = block.EndRegisters.SetSegment(targetSegReg, segValue);
    }
}
