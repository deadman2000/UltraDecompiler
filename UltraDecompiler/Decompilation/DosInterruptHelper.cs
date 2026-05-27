namespace UltraDecompiler.Decompilation;

/// <summary>
/// Хелпер для представления x86 INT прерываний в виде CallExpr,
/// с использованием функций-обёрток из заголовка DOS.H компилятора Quick C
/// и расширенного набора высокоуровневых обёрток над INT 21h (MS-DOS).
///
/// Опирается на:
///   - D:\Dos\PROGRAMS\QuickC\INCLUDE\DOS.H (оригинальный QuickC)
///   - UltraDecompiler/assets/msdos.h (собственный заголовок проекта с
///     дружественными именами для распространённых сервисов INT 21h)
///
/// Для INT 21h выполняется диспетчеризация по значению AH (если оно
/// известно на этапе декомпиляции как константа). Это позволяет
/// вместо универсального intdos() генерировать осмысленные вызовы
/// вида dos_open(), dos_read(), dos_print_string() и т.д.
/// </summary>
public static class DosInterruptHelper
{
    /// <summary>
    /// Имена функций из msdos.h, которые объявлены как void
    /// (не возвращают полезного значения в AX).
    /// Для таких прерываний мы порождаем CallOperation, а не SetOperation.
    /// </summary>
    private static readonly HashSet<string> VoidInterruptFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "dos_char_output",
        "dos_print_string",
        "dos_set_current_drive",
        "dos_exit",
        // При добавлении новых void-функций в msdos.h — добавляй сюда:
        // "dos_set_time",
        // "dos_set_date",
        // "dos_set_dta",
    };

    /// <summary>
    /// Создаёт CallExpr, моделирующий вызов прерывания.
    /// Для вектора 21h — выполняет расширенное распознавание по AH.
    /// </summary>
    public static CallExpr CreateForInt(int vector, in RegisterExpressions inputRegisters)
    {
        string name;
        List<Expr> args = new();

        if (vector == 0x21)
        {
            var resolved = ResolveInt21hFunction(inputRegisters);
            name = resolved.Name;
            args.AddRange(resolved.Args);
        }
        else
        {
            name = "int86";
            args.Add(new ConstExpr(vector));
        }

        var proc = new Procedure { Name = name };
        return new CallExpr(proc, args);
    }

    /// <summary>
    /// Возвращает true, если данное прерывание следует представлять как
    /// CallOperation (без захвата возвращаемого значения), а не как
    /// SetOperation с результатом.
    ///
    /// Основывается на том, как функция объявлена в msdos.h
    /// (void vs возвращающая int).
    /// </summary>
    public static bool ShouldEmitAsCallOperation(int vector, in RegisterExpressions regs, CallExpr callExpr)
    {
        if (VoidInterruptFunctions.Contains(callExpr.Procedure.Name))
            return true;

        // Для не-21h прерываний (INT 10h, 16h и т.д.) по умолчанию считаем void,
        // если не будет специальной логики позже.
        if (vector != 0x21)
            return true;

        // Для INT 21h: если имя не в списке void — значит, функция возвращает значение (int).
        return false;
    }

    /// <summary>
    /// Основная логика расширенного распознавания INT 21h.
    /// Если AH — константа, выбираем специализированную функцию из msdos.h.
    /// Иначе fallback на "intdos".
    /// </summary>
    private static (string Name, IReadOnlyList<Expr> Args) ResolveInt21hFunction(in RegisterExpressions regs)
    {
        Expr ahExpr = regs.Get8(4); // AH
        if (ahExpr is not ConstExpr c)
            return ("intdos", Array.Empty<Expr>());

        byte ah = (byte)(c.Value & 0xFF);

        // Часто используемый far-указатель DS:DX
        Expr? dsDx = TryBuildDsDxFarPointer(regs);

        return ah switch
        {
            // === Character I/O ===
            0x02 => ("dos_char_output", [regs.Get8(2)]),           // DL
            0x09 => ("dos_print_string", dsDx != null ? [dsDx] : [regs.Get16(2), regs.GetSegment(3)]),

            // === Drive / Directory ===
            0x0E => ("dos_set_current_drive", [regs.Get8(0)]),
            0x19 => ("dos_get_current_drive", Array.Empty<Expr>()),
            0x39 => ("dos_make_directory", dsDx != null ? [dsDx] : FallbackDsDx(regs)),
            0x3A => ("dos_remove_directory", dsDx != null ? [dsDx] : FallbackDsDx(regs)),
            0x3B => ("dos_set_current_directory", dsDx != null ? [dsDx] : FallbackDsDx(regs)),
            0x47 => ("dos_get_current_directory", [regs.Get8(0), dsDx ?? FallbackDsDxSegmentOffset(regs)]),

            // === File operations (High level) ===
            0x3C => ("dos_creat", BuildCreatOpenArgs(regs, dsDx)),
            0x3D => ("dos_open", BuildCreatOpenArgs(regs, dsDx)),
            0x3E => ("dos_close", [regs.Get16(3)]),                // BX = handle
            0x3F => ("dos_read", BuildReadWriteArgs(regs, dsDx)),
            0x40 => ("dos_write", BuildReadWriteArgs(regs, dsDx)),
            0x41 => ("dos_unlink", dsDx != null ? [dsDx] : FallbackDsDx(regs)),
            0x42 => ("dos_lseek", [regs.Get16(3), regs.Get8(0), regs.Get16(1), regs.Get16(2)]), // BX, AL, CX:DX

            // === Find ===
            0x4E => ("dos_find_first", [dsDx ?? FallbackDsDxSegmentOffset(regs), regs.Get16(1)]),
            0x4F => ("dos_find_next", Array.Empty<Expr>()),

            // === Misc useful services ===
            0x30 => ("dos_get_dos_version", Array.Empty<Expr>()),
            0x36 => ("dos_get_free_disk_space", [regs.Get8(0)]),

            // Exit (обычно уже отфильтрован IsExit, но на всякий случай)
            0x4C => ("dos_exit", [regs.Get8(0)]),

            // Fallback — оставляем низкоуровневый intdos (как в оригинальном QuickC <dos.h>)
            _ => ("intdos", Array.Empty<Expr>())
        };
    }

    // ---------- Вспомогательные методы построения аргументов ----------

    private static Expr? TryBuildDsDxFarPointer(in RegisterExpressions regs)
    {
        // DX (offset) + DS (segment) — самый частый случай для имён файлов и буферов
        var dx = regs.Get16(2);
        var ds = regs.GetSegment(3);

        // Если оба осмысленные — создаём MemExpr как представление far-указателя
        if (dx is not ConstExpr { Value: 0 } || ds is not ConstExpr { Value: 0 })
            return new MemExpr(dx, ds);

        return null;
    }

    private static Expr FallbackDsDxSegmentOffset(in RegisterExpressions regs)
    {
        // Когда не получилось красиво собрать MemExpr — передаём два отдельных значения
        return new Math2Expr(Math2Operation.Or,
            new Math2Expr(Math2Operation.Shl, regs.GetSegment(3), new ConstExpr(16)),
            regs.Get16(2));
    }

    private static Expr[] FallbackDsDx(in RegisterExpressions regs)
    {
        return [regs.Get16(2), regs.GetSegment(3)];
    }

    private static Expr[] BuildCreatOpenArgs(in RegisterExpressions regs, Expr? dsDx)
    {
        // AH=3Ch/3Dh: DS:DX = имя, AL = access/attr
        var al = regs.Get8(0);
        if (dsDx != null)
            return [dsDx, al];

        return [regs.Get16(2), regs.GetSegment(3), al];
    }

    private static Expr[] BuildReadWriteArgs(in RegisterExpressions regs, Expr? dsDx)
    {
        // AH=3Fh/40h: BX=handle, DS:DX=буфер, CX=count
        var bx = regs.Get16(3);
        var cx = regs.Get16(1);

        if (dsDx != null)
            return [bx, dsDx, cx];

        return [bx, regs.Get16(2), regs.GetSegment(3), cx];
    }

    /// <summary>
    /// Обновляет (или дополняет) состояние регистров после выполнения
    /// смоделированного вызова прерывания.
    /// </summary>
    public static RegisterExpressions ApplyPostInterruptEffects(
        int vector,
        RegisterExpressions before,
        VariableStorage variables,
        CallExpr modeledCall)
    {
        // На данном этапе не выполняем дополнительных мутаций состояния.
        // Если вызов был value-returning, HandleInterrupt уже установил AX.
        // Если вызов был void — AX оставлен как есть (может быть испорчен сервисом).
        return before;
    }
}

