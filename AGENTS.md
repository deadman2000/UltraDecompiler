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

## Структура решения

| Проект | Назначение |
|--------|------------|
| `UltraDecompiler` | Ядро: парсер EXE/COM, дизассемблер, CFG, ExpressionBuilder |
| `Common` | Общие типы: `RelocationTable`, `RelocationEntry` (используются ядром, LibParser и LibMatching) |
| `LibParser` | Разбор OMF `.LIB` Microsoft QuickC (модули, сегменты, FIXUPP, словарь символов) |
| `LibMatching` | Сопоставление кода EXE/COM с функциями из `.LIB` (crt0, `_main`, runtime) |
| `Tools` | CLI: `decompile`, `decompile-match`, `lib` |
| `DecompilerTests` | Тесты ядра (дизассемблер, IR, регистры, CFG) |
| `LibParserTests` / `LibMatchingTests` | Тесты парсера библиотек и сопоставления (эталонные `.LIB` из `QuickC/`) |

---

## Архитектура и пайплайн декомпиляции

Базовый пайплайн:

```
DosExeParser → X86Disassembler → ControlFlowGraph → ExpressionBuilder → (будущий CodeGen)
```

Расширенный пайплайн (CLI `decompile-match`):

```
DosExeParser → Crt0EntryPointMatcher → MainOffsetFinder → DecompilePipeline (от _main)
                     ↑ LibParser (.LIB)        ↑ LibMatching
```

### 1. Parser (`UltraDecompiler/Parser/`)
- `DosExeParser.cs` — загрузка и парсинг MZ .EXE и plain .COM файлов.
- Читает таблицу релокаций MZ и строит `RelocationTable`; образ **не патчится** — релокации помечаются символически при декодировании операндов.
- Определяет точку входа.
- Различает `IsCom` (разная инициализация регистров на входе).

### 2. Disassembler (`UltraDecompiler/Disassembler/`)
- `X86Disassembler.cs` — рекурсивный дизассемблер с отслеживанием регистров (`RegisterState`). Статический `Disassemble()` — линейное извлечение тела функции для LibMatching.
- `Common/RelocationTable.cs` — помечает 16-битные слова образа как символические смещения (`TryGetOffsetName`); имена попадают в операнды инструкций и далее в IR.
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
- `ExpressionBuilder.Parameters.cs` — восстановление входных параметров функции: пролог `push bp; mov bp, sp` / `ENTER`, смещения `[BP+4]`, `[BP+6]`, … → `arg0`, `arg1`; чтения подменяются на `Variable` в IR.
- `DosInterruptHelper.cs` — преобразование INT 21h в красивые вызовы (`dos_open`, `dos_print_string` и т.д.) на основе `msdos.h`. При генерации кода ориентируется на оригинальные заголовки из `assets/QuickC/`.

**Ключевой принцип ExpressionBuilder:**
> MOV/LEA просто обновляют символическое состояние регистров.  
> Настоящие `SetOperation`/`StoreOperation` создаются **только** для арифметики, логики, сдвигов, записи в память и вызовов.

Флаги (ZF, CF, SF, OF, DF) тоже хранятся как символические выражения (`CmpExpr` и булевы комбинации). `CLD`/`STD` обновляют DF; `CLI`/`STI` порождают `_disable()` / `_enable()`.

### 5. LibParser (`LibParser/`)
- `Omf/OmfLibraryParser.cs` — чтение `.LIB` (записи F0h/F1h, TIS OMF Appendix 2).
- `Omf/OmfModuleParser.cs` — разбор объектного модуля: LEDATA/LIDATA, EXTDEF, FIXUPP.
- `Omf/OmfRelocationTableBuilder.cs` — `RelocationTable` для дизассемблирования кода модуля (Offset16, SegmentBase, Pointer32).
- `Omf/OmfFixupNameResolver.cs` — имена целей FIXUPP (EXTDEF, SEGDEF).
- Подробности формата: `LibParser/OMF.md`, API: `LibParser/Readme.md`.

### 6. LibMatching (`LibMatching/`)
- `LibraryFunctionMatcher.cs` — сопоставление тела функции в образе EXE с публичными символами `.LIB`.
- `FunctionBodyComparer.cs` — сравнение инструкций с маскированием rel16/seg16 и near CALL/JMP (до/после линковки).
- `Crt0EntryPointMatcher.cs` — определение библиотеки по совпадению crt0/`__astart` на точке входа.
- `MainOffsetFinder.cs` — поиск `_main` по FIXUPP вызова из crt0.
- `LibrarySymbolFinder.cs` — линейный перебор образа для поиска символа (fallback, если не на точке входа).

**Ограничение:** пока символ в модуле предполагается с offset 0 в CODE (нет разбора PUBDEF). См. [TODO.md](./TODO.md).

---

## Ключевые файлы и точки входа

| Путь | Назначение |
|------|------------|
| `Tools/Commands/DecompileCommand.cs` | CLI `decompile`: парсинг → дизассемблирование → CFG → ExpressionBuilder |
| `Tools/Commands/DecompileMatchCommand.cs` | CLI `decompile-match`: crt0 + `_main` через `.LIB`, затем `DecompilePipeline` |
| `Tools/DecompilePipeline.cs` | Общий пайплайн декомпиляции (используется обеими командами) |
| `Tools/Commands/LibCommand.cs` | CLI `lib`: разбор OMF `.LIB`, дизассемблирование символа |
| `LibParser/Omf/OmfLibraryParser.cs` | Парсер QuickC `.LIB` |
| `LibMatching/LibraryFunctionMatcher.cs` | Сопоставление EXE с функциями библиотеки |
| `Common/RelocationTable.cs` | Таблица релокаций (EXE и OMF-модули) |
| `UltraDecompiler/Disassembler/X86Disassembler.cs` | Главный дизассемблер (самый сложный файл) |
| `UltraDecompiler/Decompilation/ExpressionBuilder.cs` | Основная логика декомпиляции (символическое выполнение) |
| `UltraDecompiler/Decompilation/RegisterExpressions.cs` | Моделирование регистров + флагов |
| `UltraDecompiler/assets/QuickC/` | Оригинальные заголовочные файлы Microsoft QuickC 1.0 (DOS.H, CONIO.H, STDIO.H, BIOS.H и др.). Используются как эталон для генерации совместимого кода |
| `QuickC/` (корень репозитория) | Эталонные `.LIB` и заголовки QuickC для тестов и `decompile-match` |
| `TODO.md` | Актуальный список ограничений, нереализованных инструкций и задач проекта |
| `DecompilerTests/BaseTests.cs` | Удобные хелперы для тестов ядра (hex DSL) |
| `DecompilerTests/Tools/HexConverter.cs` | Парсер hex-строк с комментариями `;` |
| `QuickC/INCLUDE/msdos.h` | Целевой стиль API для сгенерированного кода (QuickC-совместимый) |

### Заголовочные файлы QuickC

В папке `UltraDecompiler/assets/QuickC/` хранятся оригинальные заголовочные файлы компилятора **Microsoft QuickC 1.0** (включая `DOS.H`, `CONIO.H`, `BIOS.H`, `STDIO.H`, `STDLIB.H`, `PROCESS.H`, `GRAPH.H` и др., а также содержимое `SYS\`).

Эти файлы служат эталоном при:
- Проектировании обёрток в `msdos.h`
- Реализации `DosInterruptHelper`
- Будущей генерации `#include` директив и сигнатур функций в выходном C-коде

При добавлении новых распознаваемых INT 21h функций рекомендуется сверяться именно с этими заголовками.

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
- `DecompilerTests/` — ядро:
  - `Disassembler/*Tests.cs`
  - `Expressions/*Tests.cs` (самые важные)
  - `Registers/*Tests.cs` (тестируют `RegisterExpressions`)
  - `GraphTests.cs`, `InstructionIsExitTests.cs` и др.
- `LibParserTests/` — OMF, FIXUPP, `OmfRelocationTableBuilder`.
- `LibMatchingTests/` — crt0, `_main`, сопоставление с эталонными `.LIB` и `.EXE`.
- Базовые хелперы ядра: `Disassemble(hex)`, `GetGraph(hex)`, `BuildExpressions(hex)`.

Запуск тестов:
```powershell
dotnet test                              # все проекты
dotnet test DecompilerTests              # только ядро
dotnet test LibParserTests
dotnet test LibMatchingTests
```

Сборка:
```powershell
dotnet build -c Release
```

---

## Текущее состояние и известные ограничения

Актуальный и подробный список нереализованных возможностей, ограничений и задач проекта находится в файле **[TODO.md](./TODO.md)**.

Этот файл (`AGENTS.md`) содержит только высокоуровневую архитектурную информацию. Все конкретные TODO и их статус отслеживаются отдельно.

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

- **При работе с `.LIB` и сопоставлением**:
  1. Проверь разбор модуля через `dotnet run --project Tools -- lib QuickC\CLIBC.LIB -s _printf`.
  2. FIXUPP → `OmfRelocationTableBuilder.Build` перед дизассемблированием кода модуля.
  3. Сравнение тел — `FunctionBodyComparer` (rel16 маскируются, near CALL/JMP игнорируют абсолютный target).
  4. Полный сценарий: `dotnet run --project Tools -- decompile-match path\to\hello.exe`.

---

## Что НЕ нужно делать

- Не пытайся сразу писать генератор C-кода, пока не стабилизирован IR (ExpressionBuilder).
- Не добавляй поддержку 32-бит / protected mode — проект строго для 8086 real mode.
- Не ослабляй `WarningsAsErrors` и правила .editorconfig.

---
