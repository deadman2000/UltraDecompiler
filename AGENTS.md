# AGENTS.md — Руководство для AI-агентов по UltraDecompiler

## Общее описание проекта

**UltraDecompiler** — специализированный декомпилятор для 16-битных программ MS-DOS (8086/8088, реальный режим).

**Главная цель проекта:**
> Декомпилировать DOS .EXE / .COM файлы в исходный код на C, максимально близкий по стилю и структуре к тому, что генерировал **Microsoft QuickC Compiler 1.0** (и близкие MSC 5.x/6.x).

Это не универсальный декомпилятор. Проект фокусируется на высокой точности восстановления логики именно для кода, скомпилированного QuickC той эпохи.

---

## Язык общения и комментариев в коде

**Строгое правило проекта (обязательно к соблюдению):**

- **Все ответы, объяснения, размышления и коммуникация** с пользователем должны быть **исключительно на русском языке**.
- Все комментарии в исходном коде (`//`, многострочные комментарии) пишутся **на русском языке**.
- Вся XML-документация (комментарии `///`) тоже должна быть на русском языке.
- Это требование распространяется на все файлы: основной код, тесты, вспомогательные скрипты и документацию.

Правило унаследовано из корневых инструкций репозитория (`.grok/Agents.md`) и имеет высший приоритет.

---

## Архитектура и пайплайн декомпиляции

Процесс декомпиляции строго разделён на этапы:

```
DosExeParser → X86Disassembler → ControlFlowGraph → ExpressionBuilder → (будущий CodeGen)
```

### 1. Parser (`UltraDecompiler/Parser/`)
- `DosExeParser.cs` — загрузка и парсинг MZ .EXE и plain .COM файлов.
- Применяет релокации, определяет точку входа.
- Различает `IsCom` (разная инициализация регистров на входе).

### 2. Disassembler (`UltraDecompiler/Disassembler/`)
- `X86Disassembler.cs` — рекурсивный дизассемблер с отслеживанием регистров (`RegisterState`).
- Использует BFS + `DisassembleBranch` для корректного разбора переходов.
- `DecodeOneInstruction()` — огромный свитч (высокая цикломатическая сложность ~257). Здесь вся логика декодирования опкодов 8086.
- `Instruction.cs` + `Instruction.Registers.cs` — модель инструкции + применение эффектов на регистры.
- Поддержка префиксов (сегментные, REP, LOCK).
- `IsExit` — умное определение выхода из программы через INT 20h/21h/27h (AH=00h,4Ch,31h).

**Важно:** дизассемблер передаёт `RegisterState` между блоками — это улучшает качество разбора косвенных переходов.

### 3. Graph (`UltraDecompiler/Graph/`)
- `ControlFlowGraph.cs` + `BasicBlock.cs` — построение CFG.
- Умеет **разбивать блоки** при переходе в середину существующего блока (`GetBlock`).
- Строит `NextBlock` / `ConditionalBlock`.
- Есть экспорт в DOT (`SaveDot`) для визуализации (требует Graphviz `dot`).

### 4. Decompilation (`UltraDecompiler/Decompilation/`)
Это сердце проекта — **символическое выполнение**.

- `ExpressionBuilder.cs` (основной класс, раньше назывался Decompiler) — BFS по блокам CFG + symbolic execution.
- `RegisterExpressions.cs` — ключевая структура. Моделирует состояние всех регистров (включая 8/16-битное алиасинг AX/AH/AL и т.д.) как **символические выражения**.
  - Поддерживает две формы хранения для групп регистров.
  - Автоматический split/merge при работе с 8-битными половинками.
- `Expr.cs` — алгебра выражений:
  - `Variable`, `ConstExpr`, `MemExpr`
  - `Math1Expr` / `Math2Expr` (Neg/Not, Add/Sub/And/Or/Xor/Shl/Shr)
  - `CmpExpr` (Eq, Ne, Ult/Ule/Ugt/Uge) — для моделирования флагов
  - `CallExpr`
- `Operation.cs` — side-effect операции: `SetOperation`, `CallOperation`, `StoreOperation`.
- `ExprBlock.cs` — результат для одного BasicBlock (Operations + Condition + ссылки на следующие блоки).
- `VariableStorage.cs` + `PspKnownFields` — отслеживание переменных + распознавание обращений к PSP (Program Segment Prefix).
- `DosInterruptHelper.cs` — преобразование INT 21h в красивые вызовы (`dos_open`, `dos_print_string` и т.д.) на основе `assets/msdos.h`.

**Ключевой принцип ExpressionBuilder:**
> MOV/LEA просто обновляют символическое состояние регистров.  
> Настоящие `SetOperation`/`StoreOperation` создаются **только** для арифметики, логики, сдвигов, записи в память и вызовов.

Флаги (ZF, CF, SF, OF) тоже хранятся как символические выражения (`CmpExpr` и булевы комбинации).

---

## Ключевые файлы и точки входа

| Путь | Назначение |
|------|------------|
| `UltraDecompiler/Program.cs` | CLI: парсинг → дизассемблирование → CFG → ExpressionBuilder |
| `UltraDecompiler/Disassembler/X86Disassembler.cs` | Главный дизассемблер (самый сложный файл) |
| `UltraDecompiler/Decompilation/ExpressionBuilder.cs` | Основная логика декомпиляции (символическое выполнение) |
| `UltraDecompiler/Decompilation/RegisterExpressions.cs` | Моделирование регистров + флагов |
| `UltraDecompiler/assets/msdos.h` | Целевой стиль API для сгенерированного кода (QuickC-совместимый) |
| `Tests/BaseTests.cs` | Удобные хелперы для тестов (hex DSL) |
| `Tests/Tools/HexConverter.cs` | Парсер hex-строк с комментариями `;` |

---

## Соглашения по коду и стиль

### Строгие правила (WarningsAsErrors + .editorconfig)
- **Все предупреждения — ошибки**.
- `dotnet_diagnostic.CS0168/0169/0219/IDE0051/IDE0052/IDE0059` — error.
- File-scoped namespaces (`namespace Foo.Bar;`).
- 4 пробела, CRLF, `csharp_style_namespace_declarations = file_scoped:error`.
- Предпочтительно `var` **только** когда тип очевиден из инициализатора.
- Expression-bodied члены разрешены выборочно (аксессоры — да, методы — обычно нет).

### Принятые паттерны в проекте
- Часто используются `record` и `record struct` (Expr, RegisterExpressions).
- `partial class` для разделения большой логики (`ExpressionBuilder` + `*.Helpers.cs`).
- Тесты пишутся через многострочные raw-строки C# 11+ с hex + комментариями после `;`.
- Имена: `HandleXxx`, `BuildXxx`, `GetExpression`.
- Везде подробные XML-документация на русском языке.

### Тестирование
- xUnit + Coverlet.
- Тесты разделены по уровням:
  - `Disassembler/*Tests.cs`
  - `Expressions/*Tests.cs` (самые важные)
  - `Registers/*Tests.cs` (тестируют `RegisterExpressions`)
  - `GraphTests.cs`, `InstructionIsExitTests.cs` и др.
- Базовые хелперы: `Disassemble(hex)`, `GetGraph(hex)`, `BuildExpressions(hex)`.

Запуск тестов:
```powershell
dotnet test
```

Сборка:
```powershell
dotnet build -c Release
```

---

## Текущее состояние и известные ограничения (TODO)

Из кода и комментариев:

1. **Нет поддержки многих инструкций**:
   - `MUL`, `IMUL`, `DIV`, `IDIV`
   - Строковые операции (`MOVS`, `LODS`, `STOS`, `CMPS`, `SCAS` + REP)
   - `LEA` в некоторых контекстах, `POP` в регистр (частично), `DAA/DAS/AAA` и т.д.
   - `LOOP*`, `JCXZ`

2. **Анализ процедур почти отсутствует**:
   - `Procedure` — пока просто имя.
   - Нет анализа пролога/эпилога, аргументов, возвращаемых значений.
   - Вызовы обрабатываются поверхностно.

3. **Моделирование памяти**:
   - Базовое (через `MemExpr` + `StoreOperation`).
   - Есть распознавание PSP, но полноценного анализа стека/кучи/глобальных данных нет.
   - TODO: объединить Address + Segment в `StoreOperation`.

4. **Флаги**:
   - PF (чётность) не отслеживается вообще.
   - Часть флаговой арифметики — приближённая.

5. **Code Generation**:
   - На данный момент ExpressionBuilder только строит IR (`ExprBlock` + `Operations`).
   - Генерация финального C-кода **ещё не реализована**.

6. **Другие**:
   - Обработка `CALL` в CFG помечена TODO (нужен анализ "есть ли возврат").
   - Переходы в середину инструкции — исключение.

**Важно при работе с TODO:**
При закрытии, частичном решении или значительном прогрессе по любому из пунктов выше — **обязательно обновляй этот файл** (AGENTS.md). Нужно либо удалить выполненный пункт, либо добавить актуальный статус (например, "частично реализовано в ...").

---

## Полезные практики при работе с проектом

- **При добавлении новой инструкции**:
  1. Добавь декодирование в `X86Disassembler.DecodeOneInstruction()`.
  2. Обнови `Instruction.Registers.cs` (эффекты на регистры/флаги).
  3. Добавь обработку в `ExpressionBuilder.GenerateCode()`.
  4. Напиши тесты в `Disassembler/...` и `Expressions/...`.

- **При работе с регистрами** — всегда используй `RegisterExpressions.Set16/Set8/Get16/Get8`. Не трогай поля напрямую.

- **Для тестов с символическими переменными** используй перегрузку:
  ```csharp
  BuildExpressions(hex, vars => RegisterExpressions.InitCom(vars) with { AX = vars.CreateVariable("input") });
  ```

- Визуализация CFG (требуется Graphviz в PATH):
  ```csharp
  cfg.SaveDot("cfg.dot");
  ```

- При отладке ExpressionBuilder очень полезно смотреть `block.InitRegisters` → `block.EndRegisters` и `block.Operations`.

---

## Что НЕ нужно делать

- Не пытайся сразу писать генератор C-кода, пока не стабилизирован IR (ExpressionBuilder).
- Не добавляй поддержку 32-бит / protected mode — проект строго для 8086 real mode.
- Не ослабляй `WarningsAsErrors` и правила .editorconfig.

---

**Дата последнего обновления этого файла:** (обновляй при значимых архитектурных изменениях)

Проект активно развивается. Основной фокус — точность symbolic execution и соответствие стилю QuickC.
