namespace UltraDecompiler.Decompilation.InstructionHandlers;

/// <summary>
/// Обрабатывает INT (программное прерывание).
///
/// Non-exit INT представляются как CallExpr, вызывающие функции из msdos.h
/// (dos_open, dos_read, dos_print_string и др.) либо fallback к int86/intdos.
///
/// Логика выбора операции:
///   - Если функция в msdos.h объявлена как <c>void</c> (dos_print_string, dos_char_output,
///     dos_set_current_drive, dos_exit и т.п.) → порождаем <b>CallOperation</b>.
///   - Если функция возвращает значение (dos_open, dos_read, dos_lseek и т.д.) →
///     порождаем <b>SetOperation(resultVar, CallExpr)</b> и кладём результат в AX.
///
/// Это позволяет корректно моделировать как "fire-and-forget" прерывания,
/// так и те, чей результат важен для дальнейшего кода.
/// </summary>
public class InterruptHandler : IInstructionHandler
{
    public void Handle(ExprBlock block, Instruction instr)
    {
        if (instr.Operand1.Type != OperandType.Immediate8 &&
            instr.Operand1.Type != OperandType.Immediate16)
        {
            // TODO Подставлять CallExpr на intdos, int86 и т.д.
            throw new NotImplementedException($"INT with non-immediate operand is not supported: {instr}");
        }

        int vector = instr.Operand1.Value;

        var callExpr = DosInterruptHelper.CreateForInt(vector, block.EndRegisters);

        if (DosInterruptHelper.ShouldEmitAsCallOperation(vector, block.EndRegisters, callExpr))
        {
            // Функция объявлена как void в msdos.h (например dos_print_string, dos_set_current_drive и т.д.)
            // Порождаем чистый CallOperation без захвата результата.
            block.Operations.Add(new CallOperation(callExpr.Procedure, callExpr.Args));
        }
        else
        {
            // Функция возвращает значение (handle, код ошибки и т.д.) — захватываем в переменную.
            // Это позволяет использовать результат INT 21h в дальнейшем коде.
            var resultVar = block.Variables.CreateVariable();
            block.Operations.Add(new SetOperation(resultVar, callExpr));

            // Символическое значение AX после INT — это результат вызова
            block.EndRegisters = block.EndRegisters.Set16(GpRegister16.AX, resultVar);
        }

        // Дополнительные эффекты (clobber регистров, CF и т.д.) — через хелпер
        block.EndRegisters = DosInterruptHelper.ApplyPostInterruptEffects(
            vector, block.EndRegisters, block.Variables, callExpr);
    }
}
