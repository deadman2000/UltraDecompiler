using System.Text;
using UltraDecompiler.Ir.Calls;
using UltraDecompiler.Ir.Helpers;

namespace UltraDecompiler.Ir.Expressions;

/// <summary>
/// Базовый класс выражений
/// </summary>
public abstract partial record Expr
{
    /// <summary>
    /// Приоритет оператора выражения (по аналогии с C/C++).
    /// Чем выше — тем сильнее связывание, меньше нужда в скобках.
    /// </summary>
    public virtual int GetPrecedence() => Prec.Atom;

    public override string ToString() => ToString(0);

    /// <summary>
    /// Генерация строки выражения с учётом приоритета родительского контекста.
    /// При необходимости добавляются скобки.
    /// </summary>
    public virtual string ToString(int parentPrec) => ToString();
}

/// <summary>
/// Константы приоритетов операторов (как в языке C).
/// Используются для корректной расстановки скобок в ToString.
/// </summary>
static class Prec
{
    public const int Atom = 100;      // переменные, константы, вызовы, обращения к памяти
    public const int Postfix = 16;    // x++, x--
    public const int Unary = 15;      // -x, !x
    public const int MulDiv = 14;     // * / %
    public const int AddSub = 13;     // + -
    public const int Shift = 12;      // << >>
    public const int Compare = 11;    // < <= > >= == !=
    public const int BitAnd = 9;      // &
    public const int BitXor = 8;      // ^
    public const int BitOr = 7;       // |
}

/// <summary>
/// Переменная
/// </summary>
/// <param name="Number">Номер для <c>varN</c>/<c>tempN</c> (1-based; у <see cref="IsInternal"/> всегда 0).</param>
/// <param name="Name">Имя (параметры, поля PSP, метки строковых циклов).</param>
/// <param name="IsStack">Локаль стекового кадра [BP±disp].</param>
/// <param name="IsTemp">Временная переменная IR-анализа.</param>
/// <param name="IsInternal">Служебная переменная символического выполнения.</param>
/// <param name="IsGlobal">Глобальная переменная DGROUP (объявляется вне функций).</param>
public sealed record Variable(
    int Number = 0,
    string? Name = null,
    bool IsStack = false,
    bool IsTemp = false,
    bool IsInternal = false,
    bool IsGlobal = false,
    bool IsRegister = false) : Expr
{
    /// <summary>Старшее слово far-указателя на стеке (не объявляется отдельно в C).</summary>
    public bool IsMergedFarPointerSegment { get; set; }

    /// <summary>Старшее слово <c>long</c> на стеке (не объявляется отдельно в C).</summary>
    public bool IsMergedLongHigh { get; set; }

    /// <summary>Сегментная часть far-указателя, загруженная в ES/DS через LES/LDS.</summary>
    public Variable? FarPointerSegmentVariable { get; set; }

    /// <summary>Инициализатор far-указателя (<c>seg:off</c> в одном 32-битном литерале).</summary>
    public uint? FarPointerInitializer { get; set; }

    /// <summary>Нужно ли объявление переменной в сгенерированном C-коде (локально внутри функции).</summary>
    public bool RequiresCDeclaration =>
        !IsMergedFarPointerSegment
        && !IsMergedLongHigh
        && (IsStack || (!IsTemp && !IsInternal && !IsGlobal));

    /// <summary>
    /// Выведенный тип C (из сигнатуры вызова или копирования). <see langword="null"/> — <c>int</c>.
    /// </summary>
    public CType? Type { get; set; }

    /// <summary>
    /// Размер локального массива на стеке (<c>char buf[N]</c>), если известен из <c>_chkstk</c>.
    /// </summary>
    public int? ArraySize { get; set; }

    /// <summary>Начальное значение глобала (из инициализированного _DATA в образе EXE).</summary>
    public int? InitialValue { get; set; }

    /// <summary>Тип для объявления в C-коде (без имени; массивы оформляет <see cref="CCodeGenerator"/>).</summary>
    public string DeclaredType => (Type ?? CType.Int).ToString();

    public override string ToString()
    {
        if (Name is not null)
        {
            return Name;
        }

        if (IsStack)
        {
            return $"var{Number}";
        }

        if (IsTemp)
        {
            return $"temp{Number}";
        }

        return $"var{Number}";
    }
}

/// <summary>Доступ к полю структуры (<c>base.field</c>).</summary>
public record MemberExpr(Expr Base, string FieldName) : Expr
{
    public override string ToString() => $"{Base}.{FieldName}";
}

/// <summary>Префиксный или постфиксный инкремент/декремент (<c>ptr++</c>, <c>*ptr++</c> через <see cref="MemExpr"/>).</summary>
public record IncDecExpr(IncDecKind Kind, Expr Operand) : Expr
{
    public override int GetPrecedence() =>
        Kind is IncDecKind.PreInc or IncDecKind.PreDec ? Prec.Unary : Prec.Postfix;

    public override string ToString() => ToString(0);

    public override string ToString(int parentPrec)
    {
        var operandPrec = Kind is IncDecKind.PreInc or IncDecKind.PreDec ? Prec.Unary : Prec.Postfix;
        var operandStr = Operand.ToString(operandPrec);
        return Kind switch
        {
            IncDecKind.PreInc => $"++{operandStr}",
            IncDecKind.PostInc => $"{operandStr}++",
            IncDecKind.PreDec => $"--{operandStr}",
            IncDecKind.PostDec => $"{operandStr}--",
            _ => throw new InvalidOperationException(),
        };
    }
}

/// <summary>Вид инкремента/декремента в <see cref="IncDecExpr"/>.</summary>
public enum IncDecKind
{
    PreInc,
    PostInc,
    PreDec,
    PostDec,
}

/// <summary>Унарный &amp; для передачи структуры по указателю (<c>&amp;var</c>).</summary>
public record AddressOfExpr(Expr Operand) : Expr
{
    public override int GetPrecedence() => Prec.Unary;

    public override string ToString() => ToString(0);

    public override string ToString(int parentPrec)
    {
        var operandStr = Operand.ToString(GetPrecedence());
        return parentPrec >= GetPrecedence() ? $"(&{operandStr})" : $"&{operandStr}";
    }
}

/// <summary>
/// Константное значение
/// </summary>
/// <param name="Value">Значение</param>
public record ConstExpr(int Value) : Expr
{
    public static readonly ConstExpr Zero = new(0);
    public static readonly ConstExpr One = new(1);

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Смещение внутри загруженного образа программы (слова из таблицы релокаций MZ):
/// <c>baseName + constant</c>, где <paramref name="BaseName"/> — символическая база (image, data, …).
/// </summary>
public record ImageOffsetExpr(string BaseName, int Value) : Expr
{
    public override string ToString() => $"{BaseName} + 0x{Value:X4}";
}

/// <summary>Символьный литерал QuickC (<c>'a'</c>).</summary>
public record CharConstExpr(char Value) : Expr
{
    public override string ToString() => Value switch
    {
        '\\' => "'\\\\'",
        '\'' => "'\\''",
        '\n' => "'\\n'",
        '\r' => "'\\r'",
        '\t' => "'\\t'",
        _ => $"'{Value}'",
    };
}

/// <summary>
/// Строковый литерал. Появляется, когда аргумент функции по сигнатуре из заголовка имеет тип char*
/// (например, форматная строка printf). Создаётся в CallSiteResolver из ConstExpr/ImageOffsetExpr
/// путём чтения содержимого по адресу в образе программы.
/// </summary>
public record StringExpr(string Value) : Expr
{
    public override string ToString() => ToCStringLiteral(Value);

    private static string ToCStringLiteral(string s)
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
}

/// <summary>
/// Доступ к памяти по вычисленному адресу (dereference / load).
/// Представляет значение по адресу, например ES:[BX+SI+5] или [BP-2].
/// Segment — символическое выражение сегментного регистра (если известно).
/// </summary>
public record MemExpr(Expr Address, Expr? Segment = null) : Expr
{
    public override int GetPrecedence() => Prec.Atom;

    public override string ToString() => ToString(0);

    public override string ToString(int parentPrec)
    {
        string addrStr = Address.ToString(0);
        string segPrefix = Segment is null ? "" : $"{Segment.ToString(Prec.Atom)}:";
        return $"{segPrefix}[{addrStr}]";
    }
}

/// <summary>
/// Типы математических операций с одним аргументом
/// </summary>
public enum Math1Operation
{
    /// <summary>
    /// Смена знака числа
    /// </summary>
    Neg,

    /// <summary>
    /// Отрицание
    /// </summary>
    Not,
}

/// <summary>
/// Выражение математической операции с одним аргументом
/// </summary>
/// <param name="Operation">Тип операции</param>
/// <param name="Op">Операнд</param>
public record Math1Expr(Math1Operation Operation, Expr Op) : Expr
{
    public override int GetPrecedence() => Prec.Unary;

    public override string ToString() => ToString(0);

    public override string ToString(int parentPrec)
    {
        // x86 NOT → побитовое ~; логическое ! для условий — через BoolNot() и CmpExpr.
        string opSym = Operation switch
        {
            Math1Operation.Neg => "-",
            Math1Operation.Not => Op is CmpExpr ? "!" : "~",
            _ => throw new NotImplementedException(),
        };

        // Унарные операторы — правоассоциативны. Для одинакового приоритета скобки не нужны (цепочки !!x, --x).
        string operandStr = Op.ToString(GetPrecedence());

        string result = $"{opSym}{operandStr}";

        int myPrec = GetPrecedence();
        if (parentPrec > 0 && (myPrec < parentPrec || (myPrec > parentPrec && myPrec < Prec.Atom)))
            result = $"({result})";

        return result;
    }
}

/// <summary>
/// Типы математических операций с двумя аргументами
/// </summary>
public enum Math2Operation
{
    /// <summary>
    /// Сложение
    /// </summary>
    Add,

    /// <summary>
    /// Вычитание
    /// </summary>
    Sub,

    /// <summary>
    /// Побитовый сдвиг влево
    /// </summary>
    Shl,

    /// <summary>
    /// Побитовый сдвиг вправо
    /// </summary>
    Shr,

    /// <summary>
    /// Побитовое И
    /// </summary>
    And,

    /// <summary>
    /// Побитовое ИЛИ
    /// </summary>
    Or,

    /// <summary>
    /// Побитовое исключающее ИЛИ
    /// </summary>
    Xor,

    /// <summary>
    /// Умножение (знаковое/беззнаковое определяется контекстом инструкции MUL/IMUL)
    /// </summary>
    Mul,

    /// <summary>
    /// Целочисленное деление
    /// </summary>
    Div,

    /// <summary>
    /// Остаток от деления (mod)
    /// </summary>
    Mod,
}

/// <summary>
/// Выражение математической операции с двумя аргументами
/// </summary>
/// <param name="Operation">Тип операции</param>
/// <param name="First">Первый операнд</param>
/// <param name="Second">Второй операнд</param>
public record Math2Expr(Math2Operation Operation, Expr First, Expr Second) : Expr
{
    public override int GetPrecedence() => Operation switch
    {
        Math2Operation.Add or Math2Operation.Sub => Prec.AddSub,
        Math2Operation.Shl or Math2Operation.Shr => Prec.Shift,
        Math2Operation.And => Prec.BitAnd,
        Math2Operation.Xor => Prec.BitXor,
        Math2Operation.Or => Prec.BitOr,
        Math2Operation.Mul or Math2Operation.Div or Math2Operation.Mod => Prec.MulDiv,
        _ => 0
    };

    public override string ToString() => ToString(0);

    public override string ToString(int parentPrec)
    {
        int myPrec = GetPrecedence();

        string opSym = Operation switch
        {
            Math2Operation.Add => "+",
            Math2Operation.Sub => "-",
            Math2Operation.Shl => "<<",
            Math2Operation.Shr => ">>",
            Math2Operation.And when LooksLikeBooleanAnd(First, Second) => "&&",
            Math2Operation.And => "&",
            Math2Operation.Or => "|",
            Math2Operation.Xor => "^",
            Math2Operation.Mul => "*",
            Math2Operation.Div => "/",
            Math2Operation.Mod => "%",
            _ => throw new NotImplementedException(),
        };

        // Все наши бинарные операторы — левоассоциативны.
        // Левому потомку передаём myPrec (одинаковый приоритет без скобок),
        // правому — myPrec + 1 (чтобы при одинаковом приоритете справа появились скобки: a - (b - c)).
        string leftStr = First.ToString(myPrec);
        string rightStr = Second.ToString(myPrec + 1);

        string result = $"{leftStr} {opSym} {rightStr}";

        if (parentPrec > 0 && (myPrec < parentPrec || (myPrec > parentPrec && myPrec < Prec.Atom)))
            result = $"({result})";

        return result;
    }

    private static bool LooksLikeBooleanAnd(Expr left, Expr right) =>
        IsBooleanLike(left) && IsBooleanLike(right);

    private static bool IsBooleanLike(Expr expr) =>
        expr is CmpExpr or Math2Expr { Operation: Math2Operation.And };
}

/// <summary>
/// Вызов функции
/// </summary>
public record CallExpr(string Name, IReadOnlyList<Expr> Args) : Expr
{
    /// <summary>
    /// Состояние на момент вызова
    /// </summary>
    public CallState? CallState { get; init; }

    public override string ToString()
    {
        var args = string.Join(", ", Args);
        return $"{Name}({args})";
    }
}

/// <summary>
/// Операции сравнения, используемые для представления флагов (ZF и т.д.) и условий.
/// </summary>
public enum CmpOperation
{
    /// <summary>Равно</summary>
    Eq,
    /// <summary>Не равно</summary>
    Ne,

    /// <summary>Беззнаковое меньше (unsigned &lt;). Соответствует CF=1 после CMP/SUB.</summary>
    Ult,
    /// <summary>Беззнаковое меньше или равно (unsigned &lt;=)</summary>
    Ule,
    /// <summary>Беззнаковое больше (unsigned &gt;)</summary>
    Ugt,
    /// <summary>Беззнаковое больше или равно (unsigned &gt;=)</summary>
    Uge,
}

/// <summary>
/// Выражение сравнения двух операндов.
/// Используется чтобы символически представить значение флагов после CMP/TEST/арифметики.
/// Пример: после "CMP AX, 5" флаг ZF может быть CmpExpr(Eq, AX, Const(5))
/// </summary>
public record CmpExpr(CmpOperation Operation, Expr Left, Expr Right) : Expr
{
    public override int GetPrecedence() => Prec.Compare;

    public override string ToString() => ToString(0);

    public override string ToString(int parentPrec)
    {
        int myPrec = GetPrecedence();

        string opSym = Operation switch
        {
            CmpOperation.Eq => "==",
            CmpOperation.Ne => "!=",
            CmpOperation.Ult => "<",
            CmpOperation.Ule => "<=",
            CmpOperation.Ugt => ">",
            CmpOperation.Uge => ">=",
            _ => throw new NotImplementedException(),
        };

        // Операции сравнения не ассоциативны. Для одинакового уровня используем правило левоассоциативности
        // (на практике Cmp редко вкладываются друг в друга напрямую).
        string leftStr = Left.ToString(myPrec);
        string rightStr = Right.ToString(myPrec + 1);

        string result = $"{leftStr} {opSym} {rightStr}";

        if (parentPrec > 0 && (myPrec < parentPrec || (myPrec > parentPrec && myPrec < Prec.Atom)))
            result = $"({result})";

        return result;
    }
}

// =============================================================================
// Перегрузка операторов для булевой логики условий.
// Используется при построении условий Jcc, LOOP* и т.д.
//
// Даёт очень читаемый код:
//     !CF & !ZF
//     ZF | (SF ^ OF)
//     !(SF ^ OF)
//     cxNotZero & !ZF
//
// Операторы делегируют на BoolAnd/BoolOr/BoolNot/BoolXor (extension-методы),
// которые выполняют constant folding и специальные упрощения CmpExpr.
// =============================================================================

partial record Expr
{
    /// <summary>Булево И (с constant folding и упрощениями).</summary>
    public static Expr operator &(Expr left, Expr right) => left.BoolAnd(right);

    /// <summary>Булево ИЛИ (с constant folding и упрощениями).</summary>
    public static Expr operator |(Expr left, Expr right) => left.BoolOr(right);

    /// <summary>Булево НЕ (с инверсией CmpExpr и constant folding).</summary>
    public static Expr operator !(Expr expr) => expr.BoolNot();

    /// <summary>Булево XOR (полезно для моделирования SF ^ OF).</summary>
    public static Expr operator ^(Expr left, Expr right) => left.BoolXor(right);
}
