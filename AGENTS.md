# AGENTS.md — Руководство для AI-агентов по UltraDecompiler

## Цель

**UltraDecompiler** — декомпилятор MS-DOS .EXE/.COM (8086, real mode) в C, максимально близкий к выводу **Microsoft QuickC 1.0**. Не универсальный декомпилятор — только QuickC/MSC 5.x–6.x того периода.

---

## Язык

- Общение с пользователем, комментарии в коде и XML-документация — **только на русском**.

---

## Структура решения

| Сборка | Путь | Роль |
|--------|------|------|
| Disassembly | `Disassembly/` | парсер EXE/COM, дизассемблер, CFG |
| Ir | `Ir/` | `ExpressionBuilder`, IR, handlers, переменные |
| PostProcessing | `PostProcessing/` | pass-ы и профили QuickC (`/Od`, `/Ox`) |
| CodeGeneration | `CodeGeneration/` | C-код, Makefile, рендер IR |
| Decompilation | `Decompilation/` | `Decompiler`, резолверы, эвристики |
| LibMatching + LibParser | `LibMatching/`, `LibParser/` | OMF `.LIB`, сопоставление символов |
| Headers, Compilation, Common | соответствующие каталоги | заголовки QCL, флаги, релокации |
| Tools | `Tools/` | CLI `udc` |
| DecompilerTests, TestSupport | | unit-тесты, DOSBox round-trip |

Решение: `UltraDecompiler.slnx`. Ограничения и задачи — **[TODO.md](./TODO.md)**.

---

## Пайплайн

**Полная декомпиляция** (`decompile-c`, `Decompiler`):

```
DosExeParser → TryResolveMain → CollectInstructions → OptimizationLevelHeuristics (/Od vs /Ox)
→ BuildIrForProcedures → резолверы сигнатур/вызовов → PostProcessing → CCodeGenerator + Makefile
```

**Уровни оптимизации:** `ExpressionBuilder.Create(level)` → unopt (`/Od`) или opt (`/Ox`); профиль — `DecompilationProfileRegistry.GetProfile`. `TailReturnInserter` — только `/Od`, на этапе IR construction.

---

## Где живёт логика

| Слой | Ключевое |
|------|----------|
| Дизассемблер | `X86Disassembler`, `RegisterState` (конкретные/`null` значения для разбора CFG; **не** IR) |
| IR | `ExpressionBuilder` + `InstructionHandlers/`; BFS по CFG, symbolic execution |
| Регистры в IR | `VariableStorage` (`regAX`…, флаги) + `SetOperation` через `ExprBlock.Set()`; между блоками — стек `InitStack`→`EndStack`; чтение — `Operand.GetExpression()` |
| Структура управления | CFG / flatten / loop analyzer — **не** post-hoc pass-ы по именам символов |
| PostProcessing | только семантически нейтральная читаемость (типы, DCE, литералы по типу, `void`-вызовы) |
| LibMatching | только через `LibraryProvider` |

### Политика PostProcessing (обязательно)

**Цель — точный декомпилятор, а не подгонка под эталонный `.c`.**

| Где | Что |
|-----|-----|
| CFG / IR | if/else, циклы, switch, параметры, эпилоги, типы, вызовы |
| PostProcessing | DCE, нормализация `void`, литералы по **типу**, `n & 255` → `n`, удаление `__chkstk` |

**Запрещено в PostProcessing:** pass-ы под конкретные программы; whitelist имён (`sub_*`, `printf`, `argc`); if-хирургия вместо structurer; pass-ы, маскирующие баги flatten. Round-trip сломался — **чинить IR/CFG**.

---

## CLI (`dotnet run --project Tools -- …`)

| Команда | Назначение |
|---------|------------|
| `decompile-c` | полный вывод `*.c` + Makefile |
| `disasm` | линейный дизассемблер |
| `lib` | разбор OMF `.LIB` |
| `ir-graph` | DOT-граф IR одной функции по смещению |
| `ir-tree` | текстовые IR-деревья всех пользовательских процедур |

**Примеры QuickC** (`QuickC/PROGRAMS/*.c`): вместо пути к EXE можно передать имя исходника (`hello`, `hello.c`, `switch.c`). EXE собирается через `ExeProvider` (DOSBox-X + QCL, кэш в `QuickC/BUILT/`). Параметры сборки:

| Опция | Значение |
|-------|----------|
| `--model s\|c\|m\|l` | модель памяти (`/AS`…`/AL`; по умолчанию small) |
| `--gs` / `--chkstk` | без проверки стека (`/Gs`, по умолчанию) или с ней |
| `--od` / `--ox` / `--ot` / `--ol` или `--opt od` | уровень оптимизации |
| `--lib SLIBCE.LIB` | OMF-библиотека при линковке (повторяемая) |

```powershell
# дизассемблирование _main в /Ox-примере
dotnet run --project Tools -- disasm switch.c --main --ox

# декомпиляция hello с large-моделью
dotnet run --project Tools -- decompile-c hello --model l --lib LLIBCE.LIB -o out

# IR-деревья всех пользовательских процедур (метки label_XXXX, goto)
dotnet run --project Tools -- ir-tree switch.c --ox -o switch.ir.txt

# только main
dotnet run --project Tools -- ir-tree hello --proc main
```

### Анализ IR (`ir-tree`)

Команда `ir-tree` проходит тот же этап, что и `decompile-c` до post-processing: `Decompiler.BuildIR()` — сопоставление с `.LIB`, рекурсивный сбор процедур, `ExpressionBuilder`. Вывод — по базовым блокам IR (`ExprBlock`): разделитель `──────── 0xSTART..0xEND ────────`, метка `label_XXXX:`, операции блока, переходы `goto label_XXXX`.

| Опция | Назначение |
|-------|------------|
| `input` | EXE/COM или имя примера QuickC (`hello`, `switch.c`) |
| `--proc <NAME>` | одна процедура (`main`, `sub_0123`) |
| `-o\|--output <PATH>` | файл вместо stdout |
| `-l\|--lib-dir` | каталог `.LIB` |
| `--model`, `--od`/`--ox`, `--lib` | параметры сборки примера (как у `decompile-c`) |

Типичный workflow при отладке structurer/flatten:

1. `ir-tree program.c -o program.ir.txt` — снимок IR всех пользовательских функций.
2. `ir-graph -s program.c -o 0xNNN --out proc.dot --png` — DOT одной функции по смещению (узлы = базовые блоки CFG).
3. Сравнить с `disasm program.c --main` и с эталонным `.c` в `QuickC/PROGRAMS/`.

Рендерер: `Ir/Rendering/IrTreeTextRenderer.cs`. Для unit-тестов IR без полного Decompiler — `BaseTests.BuildExpressions` + `ExpressionBuilder.Blocks`.

Точки входа: `Decompilation/Decompiler.cs`, `Ir/Builder/ExpressionBuilder.cs`, `DecompilerTests/BaseTests.cs` (`Disassemble`, `GetGraph`, `BuildExpressions`).

---

## Соглашения

- **WarningsAsErrors**; перед коммитом — `dotnet format --verify-no-changes`.
- File-scoped namespaces, 4 пробела, CRLF.
- `partial class ExpressionBuilder`; handlers — отдельные классы в `InstructionHandlers/`.
- Тесты: raw hex-строки с `;`-комментариями; у `[Fact]` — краткий комментарий **что** и **зачем** проверяется.
- Новая инструкция: `DecodeOneInstruction` → `Instruction.Registers.cs` → handler → тесты в `Disassembler/` и `Expressions/`.

```powershell
dotnet build -c Release
dotnet test DecompilerTests
dotnet format --verify-no-changes
```

---

## QuickC и round-trip

- Эталон: `QuickC/` (QCL, `INCLUDE/`, `PROGRAMS/*.c`, `.LIB`).
- Round-trip: DOSBox-X + QCL; известные сбои — `QuickC/PROGRAMS/roundtrip_xfail.txt`.
- Из тестов/агента: `DosBoxQuickCRunner.Run(...)` (рабочий каталог **`QuickC/`**), `ExeProvider.Get(...)`, `QuickCTestAssets`.

---

## Практики

- IR-регистры: `VariableStorage.Get`, `ExprBlock.Set`, `Operand.GetExpression` — не поля storage напрямую.
- Дизассемблер: `RegisterState`, `Instruction.ApplyRegisters`.
- `.LIB`: `dotnet run --project Tools -- lib QuickC\CLIBC.LIB -s _printf`.
- Полная декомпиляция: `decompile-c`.
- Кодоген: unit-тесты + round-trip.

---

## Не делать

- 32-bit / protected mode.
- Ослаблять warnings/format rules.
- Ломать совместимость с QuickC 1.0 (far/near, модели памяти, runtime из `.LIB`).
- Дублировать LibMatching вне `LibMatching/`.
