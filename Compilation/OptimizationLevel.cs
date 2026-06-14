using Common;
using UltraDecompiler.Disassembler;
using UltraDecompiler.Disassembly.Disassembler;

namespace UltraDecompiler.Compilation;

/// <summary>Уровень оптимизации QuickC (<c>/Od</c>, <c>/Ot</c>, <c>/Ol</c>, <c>/Ox</c>).</summary>
public enum OptimizationLevel
{
    /// <summary>Оптимизация отключена (<c>/Od</c>).</summary>
    Disabled,

    /// <summary>Оптимизация по скорости (<c>/Ot</c>).</summary>
    Enabled,

    /// <summary>Оптимизация циклов (<c>/Ol</c>).</summary>
    EnableLoop,

    /// <summary>Максимальная оптимизация (<c>/Ox</c>).</summary>
    EnabledFull,
}

/// <summary>Преобразует <see cref="OptimizationLevel"/> в флаги и описания QuickC.</summary>
public static class OptimizationLevelDetector
{
    /// <summary>Возвращает флаг компилятора QuickC для уровня оптимизации.</summary>
    public static string GetCompilerFlag(OptimizationLevel level) =>
        level switch
        {
            OptimizationLevel.Disabled => "/Od",
            OptimizationLevel.Enabled => "/Ot",
            OptimizationLevel.EnableLoop => "/Ol",
            OptimizationLevel.EnabledFull => "/Ox",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };

    /// <summary>Краткое описание уровня оптимизации для вывода в консоль.</summary>
    public static string GetDisplayName(OptimizationLevel level) =>
        level switch
        {
            OptimizationLevel.Disabled => "отключена (/Od)",
            OptimizationLevel.Enabled => "по скорости (/Ot)",
            OptimizationLevel.EnableLoop => "циклы (/Ol)",
            OptimizationLevel.EnabledFull => "максимальная (/Ox)",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };

    /// <summary>
    /// Детектирует уровень оптимизации по списку инструкций процедуры (main или user-функции).
    /// Эвристика: наличие стандартного пролога QuickC (push bp; mov bp, sp или enter) характерно для /Od.
    /// При наличии пролога возвращает <see cref="OptimizationLevel.Disabled"/>, иначе — <see cref="OptimizationLevel.EnabledFull"/>.
    /// </summary>
    public static OptimizationLevel DetectFromInstructions(IReadOnlyList<Instruction> instructions)
    {
        if (instructions == null || instructions.Count == 0)
        {
            return OptimizationLevel.Disabled;
        }

        // Используем общий детектор пролога (shared с остальным кодом).
        return PrologueDetector.HasStandardPrologue(instructions)
            ? OptimizationLevel.Disabled
            : OptimizationLevel.EnabledFull;
    }

    /// <summary>
    /// Детектирует уровень оптимизации по образу, таблице релокаций и смещению точки входа/ main.
    /// Выполняет лёгкий дизассемблинг только целевой процедуры (без построения CFG/IR).
    /// </summary>
    public static OptimizationLevel Detect(
        byte[] image,
        RelocationTable relocationTable,
        int offset,
        RegisterState initState)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(relocationTable);

        if (offset < 0 || offset >= image.Length)
        {
            return OptimizationLevel.Disabled;
        }

        var instructions = X86Disassembler.Disassemble(
            image,
            relocationTable,
            offset,
            initState);

        return DetectFromInstructions(instructions);
    }
}
