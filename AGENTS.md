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

---

## Структура решения

| Проект | Путь | Назначение |
|--------|------|------------|
| `UltraDecompiler.Disassembly` | `Disassembly/` | `Parser/`, `Disassembler/`, `Graph/`, `Common/` (Ansi) |
| `UltraDecompiler.Ir` | `Ir/Decompilation/` | `Expressions/`, `Operations/`, `InstructionHandlers/`, `ExpressionBuilder/`, `Procedures/`, … |
| `UltraDecompiler.Headers` | `Headers/` | `HeaderCatalog`, `HeaderFunction`, `CType`, `StructDefinition` |
| `UltraDecompiler.LibMatching` | `LibMatching/` | сопоставление тел функций с `.LIB` |
| `UltraDecompiler.Compilation` | `Compilation/` | `CompilerOptions`, `MemoryModel`, `OptimizationLevel` |
| `UltraDecompiler.PostProcessing` | `PostProcessing/` | pass-ы IR по каталогам (`Epilogue/`, `Stack/`, `Types/`, `Loops/`, …), `Helpers/LongRuntimeHelpers`, профили (`Abstractions/`, `Profiles/QuickC/`) |
| `UltraDecompiler.CodeGeneration` | `CodeGeneration/` | `CCodeGenerator`, `MakefileGenerator`, `Rendering/` (`RenderExpr`, `ToCString`) |
| `UltraDecompiler.Decompilation` | `Decompilation/` | `Decompiler`, резолверы, `CallSiteResolver`, оркестрация |
| `Common` | `Common/` | `RelocationTable`, `RelocationEntry` |
| `LibParser` | `LibParser/` | разбор OMF `.LIB` Microsoft QuickC |
| `Tools` | `Tools/` | CLI (`udc`) |
| `TestSupport` | `TestSupport/` | DOSBox + QuickC round-trip |
| `DecompilerTests` | `DecompilerTests/` | тесты ядра |
| `LibParserTests` / `LibMatchingTests` | | тесты парсера и LibMatching |

Решение: `UltraDecompiler.slnx`.

### Граф зависимостей сборок

```
Common, LibParser, Compilation (leaf)
Disassembly → Common
Ir → Disassembly, Headers, LibMatching
LibMatching → Common, Compilation, Disassembly, LibParser
PostProcessing → Ir, Headers, Disassembly, CodeGeneration
CodeGeneration → Ir, Compilation
Decompilation → все выше + LibParser
Tools → Decompilation
TestSupport → Decompilation, Compilation
```

### Профили декомпиляции

Эвристики QuickC `/Od` инкапсулированы в `QuickCUnoptimizedProfile`; заглушка `QuickCOptimizedProfile` — точка расширения для `/Ot`/`/Ox`.

- `IDecompilationProfile`, `IPostProcessPass`, `PostProcessContext`, `IrConstructionContext` — `PostProcessing/Abstractions/`
- `DecompilationProfileRegistry.GetProfile(OptimizationLevel)` — выбор профиля
- `ApplyIrConstructionPasses` — после `ExpressionBuilder.BuildProc` (`TailReturnInserter`)
- `GetProcedurePasses()` — полная цепочка post-processing в `Decompiler`
- `GetDiagnosticPasses()` — урезанный набор для `Tools/DecompilePipeline`

---

## Архитектура и пайплайн декомпиляции

### Диагностический пайплайн (CLI `decompile`, `decompile-main`)

```
DosExeParser → X86Disassembler → ControlFlowGraph → ExpressionBuilder → PostProcessing (частично) → AppendToCString
```

`decompile-main` дополнительно находит `_main` через `LibraryProvider` / `LibraryCallResolver` перед запуском `DecompilePipeline`.

### Полный пайплайн (CLI `decompile-c`, класс `Decompiler`)

```
DosExeParser
  → LibraryProvider.TryResolveMain          # crt0/__astart, _main, конфигурация .LIB
  → CollectProcedures                       # рекурсивный CFG + IR, TryMatchProcedure для runtime
  → ProcedureSignatureResolver              # сигнатуры из HeaderCatalog / анализ тел
  → CallSiteResolver + материализация литералов
  → PostProcessing (см. ниже)
  → CCodeGenerator + MakefileGenerator      # *.c, *.h, Makefile
```

### 1. Parser (`Disassembly/Parser/`)
- `DosExeParser.cs` — загрузка и парсинг MZ .EXE и plain .COM файлов.
- Читает таблицу релокаций MZ и строит `RelocationTable`; образ **не патчится** — релокации помечаются символически при декодировании операндов.
- `ExeImageLayout.cs` — раскладка сегментов образа (DGROUP, строковые литералы).
- Определяет точку входа.
- Различает `IsCom` (разная инициализация регистров на входе).

### 2. Disassembler (`Disassembly/Disassembler/`)
- `X86Disassembler.cs` — рекурсивный дизассемблер с отслеживанием регистров (`RegisterState`). Статический `Disassemble()` — линейное извлечение тела функции для LibMatching.
- `Common/RelocationTable.cs` — помечает 16-битные слова образа как символические смещения (`TryGetOffsetName`); имена попадают в операнды инструкций и далее в IR.
- Использует BFS + `DisassembleBranch` для корректного разбора переходов.
- `DecodeOneInstruction()` — огромный свитч (высокая цикломатическая сложность ~257). Здесь вся логика декодирования опкодов 8086.
- `Instruction.cs` + `Instruction.Registers.cs` — модель инструкции + применение эффектов на регистры.
- Поддержка префиксов (сегментные, REP, LOCK).
- `IsExit` — умное определение выхода из программы через INT 20h/21h/27h (AH=00h,4Ch,31h).

**Важно:** дизассемблер передаёт `RegisterState` между блоками — это улучшает качество разбора косвенных переходов.

### 3. Graph (`Disassembly/Graph/`)
- `ControlFlowGraph.cs` + `BasicBlock.cs` — построение CFG.
- Умеет **разбивать блоки** при переходе в середину существующего блока (`GetBlock`).
- Строит `NextBlock` / `ConditionalBlock`.
- Есть экспорт в DOT (`SaveDot`) для визуализации (требует Graphviz `dot`).

### 4. IR (`Ir/Decompilation/`)

Каталоги (namespace: `UltraDecompiler.Ir.Decompilation` или `UltraDecompiler.Decompilation` для `ExpressionBuilder`):

| Каталог | Содержимое |
|---------|------------|
| `Expressions/` | `Expr`, `LongExpr`, `SyntheticLoadExpr`, `RegisterExpressions`, `ExprBlock`, `ExprSubstitution` |
| `Operations/` | `SetOperation`, `IfOperation`, `ForOperation`, `ReturnOperation`, … |
| `InstructionHandlers/` | обработчики инструкций x86 (`MovHandler`, `ArithmeticHandler`, …) |
| `ExpressionBuilder/` | `ExpressionBuilder` + partial (`Parameters`, `Dot`, `Flatten`, `Switch`) |
| `Procedures/` | `DisassembledProcedure`, `ProcedureStorage`, `ProcedureSignature`, `ProcedureParameter`, параметры |
| `Calls/` | `CallState`, `CallSiteArgumentResolver` |
| `Variables/` | `VariableStorage`, `VariableSignedness`, `AssignmentTarget` |
| `Switch/` | `QuickCSwitchDetector`, `QuickCSwitchPattern` |
| `Interrupts/` | `DosInterruptHelper` |
| `Helpers/` | `WordArithmeticHelper`, `StringLiteralMaterializer`, `Extensions` |

- `ExpressionBuilder.cs` — BFS по блокам CFG + symbolic execution. Обработка инструкций делегирована в `InstructionHandlers/` (словарь `Handlers.Get(mnemonic)`).
- `RegisterExpressions.cs` — ключевая структура. Моделирует состояние всех регистров (включая 8/16-битное алиасинг AX/AH/AL и т.д.) как **символические выражения**.
  - Поддерживает две формы хранения для групп регистров.
  - Автоматический split/merge при работе с 8-битными половинками.
- `Expr.cs` — алгебра выражений:
  - `Variable`, `ConstExpr`, `MemExpr`, `StringExpr`
  - `Math1Expr` / `Math2Expr` (Neg/Not, Add/Sub/And/Or/Xor/Shl/Shr)
  - `CmpExpr` (Eq, Ne, Ult/Ule/Ugt/Uge) — для моделирования флагов
  - `CallExpr`
- `CodeGeneration/Rendering/RenderingExtensions.cs` — extension `expr.RenderExpr()`, `op.ToCString()` / `op.AppendToCString()` (IR не знает синтаксис C).
- `ExprBlock.cs` — результат для одного BasicBlock (Operations + Condition + ссылки на следующие блоки).
- `VariableStorage.cs` + `PspKnownFields` — отслеживание переменных + распознавание обращений к PSP (Program Segment Prefix).
- `ExpressionBuilder.Parameters.cs` — восстановление входных параметров функции: пролог `push bp; mov bp, sp` / `ENTER`, смещения `[BP+4]`, `[BP+6]`, … → `arg0`, `arg1`.
- `DosInterruptHelper.cs` — преобразование INT 21h в вызовы (`dos_open`, `dos_print_string` и т.д.) на основе `msdos.h`.
- `Decompiler.cs` — оркестратор полной декомпиляции (`Decompilation/`).
- `DisassembledProcedure.cs` / `ProcedureStorage` — в `Ir/Decompilation/Procedures/` (используются и в PostProcessing).
- `CallSiteResolver.cs`, `ProcedureSignatureResolver.cs` — в `Decompilation/`.
- `ProcedureSignatureAnalyzer.cs` — в `Decompilation/`; `ProcedureSignature`, `ProcedureParameter` — в `Ir/Procedures/`; `CType.cs` — в `Headers/`.

**Символическое выполнение** — ядро IR:
> MOV/LEA просто обновляют символическое состояние регистров.  
> Настоящие `SetOperation`/`StoreOperation` создаются **только** для арифметики, логики, сдвигов, записи в память и вызовов.

Флаги (ZF, CF, SF, OF, DF) тоже хранятся как символические выражения (`CmpExpr` и булевы комбинации). `CLD`/`STD` обновляют DF; `CLI`/`STI` порождают `_disable()` / `_enable()`.

Обработчики инструкций (`InstructionHandlers/`): по одному классу на семейство (`MovHandler`, `ArithmeticHandler`, `MovsHandler`, `RotateHandler`, …). При добавлении новой инструкции — декодер + `Instruction.Registers` + новый/существующий handler + тесты.

### 5. PostProcessing (`PostProcessing/`)

Каталоги pass-ов (namespace везде `UltraDecompiler.PostProcessing`):

| Каталог | Содержимое |
|---------|------------|
| `Abstractions/` | `IPostProcessPass`, `IDecompilationProfile`, контексты |
| `Profiles/QuickC/` | `QuickCUnoptimizedProfile`, `QuickCOptimizedProfile`, `DecompilationProfileRegistry` |
| `Helpers/` | `LongRuntimeHelpers` (long-арифметика runtime QuickC) |
| `Epilogue/` | `TailReturnInserter`, `EpilogueAnalyzer`, return/branch normalizers |
| `Stack/` | `StackCheckDetector`, `StackLocalArrayInferrer`, `StackFrameAllocationHelper` |
| `Types/` | inferrer-ы типов, указателей, `MainParameterNormalizer` |
| `Loops/` | `WhileLoopRecognizer`, `Argv*`, `PointerLoopBodySimplifier` |
| `Literals/` | materializer-ы литералов, `GlobalVariableRegistry` |
| `Structs/` | `StructFieldRewriter`, `StructFieldLoadSimplifier` |
| `Normalization/` | `OperationOptimizer`, `VoidCallNormalizer`, … |
| `Infrastructure/` | `OperationTreeMapper` |

Проходы над IR перед кодогенерацией (вызываются из `Decompiler` через профиль и частично из `DecompilePipeline`):

- `OperationOptimizer`, `StackCheckDetector` — удаление `__chkstk`, оптимизация IR.
- `VariableTypeInferrer`, `PointerTypeInferrer`, `VariableSignedness` — типы переменных и указателей.
- `StructLocalInferrer`, `StructFieldRewriter`, `StructFieldLoadSimplifier` — поля структур.
- `StackLocalArrayInferrer`, `StackFrameAllocationHelper` — локальные массивы на стеке.
- `VoidCallNormalizer`, `CommutativeOperationNormalizer`, `IfElseReturnFlattener` — нормализация IR.
- `WhileLoopRecognizer`, `PointerLoopBodySimplifier`, `PointerCompareSimplifier` — циклы и указатели.
- `CharPtrLiteralMaterializer` — подстановка строковых литералов для `char*`.
- `EpilogueAnalyzer` — распознавание эпилога QuickC.

### 6. CodeGeneration (`CodeGeneration/`)
- `CCodeGenerator.cs` — форматирование `*.c` / `*.h`; принимает `ProcedureCodegenModel` (не `DisassembledProcedure`).
- `MakefileGenerator.cs` — `Makefile` с флагами QuickC (`/AS`, `/Gs`, модель памяти, набор `.LIB`).
- `Rendering/CExprRenderer.cs`, `Rendering/COperationRenderer.cs` — рендер IR в C.

### 7. Headers (`Headers/`)
- `HeaderCatalog.cs` — разбор заголовков QuickC (`QuickC/INCLUDE/`, `assets/QuickC/`) для pass-ов и резолверов.
- `HeaderFunction.cs` — сигнатура функции из `.H` (типы без привязки к стеку IR).
- `StructDefinition.cs`, `CType.cs` — описание struct/union и типы C.
- Преобразование `HeaderFunction` → `ProcedureSignature` — `Ir/Procedures/HeaderFunctionExtensions.cs`.

### 8. Compilation (`Compilation/`)
- `CompilerOptions.cs`, `MemoryModel.cs`, `MemoryModelDetector` — модель памяти и флаги QCL для Makefile.
- `OptimizationLevel.cs` — уровень оптимизации (зарезервировано).

### 9. LibParser (`LibParser/`)
- `Omf/OmfLibraryParser.cs` — чтение `.LIB` (записи F0h/F1h, TIS OMF Appendix 2).
- `Omf/OmfModuleParser.cs` — разбор объектного модуля: LEDATA/LIDATA, EXTDEF, FIXUPP.
- `Omf/OmfRelocationTableBuilder.cs` — `RelocationTable` для дизассемблирования кода модуля (Offset16, SegmentBase, Pointer32).
- `Omf/OmfFixupNameResolver.cs` — имена целей FIXUPP (EXTDEF, SEGDEF).
- Подробности формата: `LibParser/OMF.md`, API: `LibParser/Readme.md`.

### 10. LibMatching (`LibMatching/`)
Код сопоставления живёт в ядре (не в отдельной сборке).

- `LibraryProvider.cs` — единая точка входа: загрузка `.LIB`, `TryResolveMain`, `TryMatchProcedure`, сужение кандидатов.
- `LibraryCallResolver.cs` — поиск целевого символа по FIXUPP вызова (`FindMainFromAstart`, `FindCalledSymbol`).
- `LibraryFunctionMatcher.cs` — сопоставление тела функции в образе EXE с публичными символами `.LIB`.
- `FunctionBodyComparer.cs` — сравнение инструкций с маскированием rel16/seg16 и near CALL/JMP.
- `LibrarySymbolFinder.cs` — линейный перебор образа (fallback).

**Ограничение:** пока символ в модуле предполагается с offset 0 в CODE (нет разбора PUBDEF). См. [TODO.md](./TODO.md).

---

## Ключевые файлы и точки входа

| Путь | Назначение |
|------|------------|
| `Tools/Program.cs` | Точка входа CLI `udc` |
| `Tools/Commands/DecompileCommand.cs` | CLI `decompile`: парсинг → дизассемблирование → CFG → ExpressionBuilder |
| `Tools/Commands/DecompileMainCommand.cs` | CLI `decompile-main`: crt0 + `_main` через `.LIB`, затем `DecompilePipeline` |
| `Tools/Commands/DecompileCCommand.cs` | CLI `decompile-c`: полная декомпиляция через `Decompiler`, запись `*.c` + Makefile |
| `Tools/Commands/DisasmCommand.cs` | CLI `disasm`: простой дизассемблер с опцией `--main` |
| `Tools/Commands/LibCommand.cs` | CLI `lib`: разбор OMF `.LIB`, дизассемблирование символа |
| `Tools/DecompilePipeline.cs` | Общий диагностический пайплайн IR → консоль |
| `UltraDecompiler/Decompilation/Decompiler.cs` | Оркестратор полной декомпиляции |
| `UltraDecompiler/CodeGeneration/CCodeGenerator.cs` | Генерация C-исходников |
| `UltraDecompiler/LibMatching/LibraryProvider.cs` | Работа с OMF-библиотеками QuickC |
| `Common/RelocationTable.cs` | Таблица релокаций (EXE и OMF-модули) |
| `UltraDecompiler/Disassembler/X86Disassembler.cs` | Главный дизассемблер (самый сложный файл) |
| `UltraDecompiler/Decompilation/ExpressionBuilder.cs` | Основная логика декомпиляции (символическое выполнение) |
| `UltraDecompiler/Decompilation/RegisterExpressions.cs` | Моделирование регистров + флагов |
| `UltraDecompiler/assets/QuickC/` | Оригинальные заголовочные файлы Microsoft QuickC 1.0 |
| `QuickC/` (корень репозитория) | Эталонные `.LIB`, заголовки и `PROGRAMS/*.c` для тестов и round-trip |
| `QuickC/PROGRAMS/ROUNDTRIP_REPORT.md` | Отчёт по сквозным тестам QCL → декомпиляция → MAKE |
| `TODO.md` | Актуальный список ограничений, нереализованных инструкций и задач проекта |
| `DecompilerTests/BaseTests.cs` | Удобные хелперы для тестов ядра (hex DSL) |
| `DecompilerTests/Tools/HexConverter.cs` | Парсер hex-строк с комментариями `;` (дубликат в `Tools/HexConverter.cs`) |
| `TestSupport/DosBoxQuickCRunner.cs` | Запуск QCL/MAKE в DOSBox для round-trip тестов |
| `QuickC/INCLUDE/msdos.h` | Целевой стиль API для сгенерированного кода (QuickC-совместимый) |

### Заголовочные файлы QuickC

В папке `UltraDecompiler/assets/QuickC/` хранятся оригинальные заголовочные файлы компилятора **Microsoft QuickC 1.0** (включая `DOS.H`, `CONIO.H`, `BIOS.H`, `STDIO.H`, `STDLIB.H`, `PROCESS.H`, `GRAPH.H` и др., а также содержимое `SYS\`).

Эти файлы служат эталоном при:
- Проектировании обёрток в `msdos.h`
- Реализации `DosInterruptHelper`
- Генерации `#include` директив и сигнатур функций в выходном C-коде (`HeaderCatalog`)

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
- **Обязательная проверка форматирования**: перед завершением работы и перед коммитом необходимо запускать `dotnet format --verify-no-changes`. Любые изменения форматирования должны быть применены через `dotnet format`.

### Принятые паттерны в проекте
- Часто используются `record` и `record struct` (Expr, RegisterExpressions, Operation).
- `partial class` для разделения большой логики (`ExpressionBuilder` + `*.Parameters.cs`, `*.Dot.cs`, `*.Flatten.cs`).
- Обработчики инструкций — отдельные классы в `InstructionHandlers/`, регистрация в `Handlers.cs`.
- Тесты пишутся через многострочные raw-строки C# 11+ с hex + комментариями после `;`.
- Имена: `HandleXxx`, `BuildXxx`, `GetExpression`, `Infer`, `Resolve`, `Materialize`.
- Везде подробные XML-документация на русском языке.

### Тестирование
- xUnit + Coverlet.
- `DecompilerTests/` — ядро:
  - `Disassembler/*Tests.cs`
  - `Expressions/*Tests.cs` (самые важные)
  - `Registers/*Tests.cs` (тестируют `RegisterExpressions`)
  - `Decompilation/*Tests.cs` — кодогенерация, Makefile, round-trip QuickC
  - `GraphTests.cs`, `InstructionIsExitTests.cs` и др.
- `LibParserTests/` — OMF, FIXUPP, `OmfRelocationTableBuilder`.
- `LibMatchingTests/` — crt0, `_main`, `LibraryCallResolver`, сопоставление с эталонными `.LIB` и `.EXE`.
- `TestSupport/` — DOSBox, пути к `QuickC/`, `DecompileTestHelper`.
- Базовые хелперы ядра: `Disassemble(hex)`, `GetGraph(hex)`, `BuildExpressions(hex)`.

**Поясняющие комментарии в тестах (обязательно):**
- У каждого теста или тестового метода должны быть комментарии на русском, объясняющие **что именно проверяется** и **почему** (неочевидные `Assert`, граничные случаи, регрессии).
- В hex-тестах (`Expressions/*Tests.cs`, `BuildExpressions`, `Disassemble`) перед `[Fact]` указывай ожидаемый IR (тип операции, значение регистра, условие); комментарии после `;` в raw-строке дублируют ассемблер.
- В **интеграционных тестах декомпиляции** (`Decompilation/*DecompileTests.cs`, `QuickCProgramRoundTripTests`) перед `[Fact]` указывай:
  1. исходную программу QuickC (`QuickC/PROGRAMS/...`) или суть фрагмента;
  2. ожидаемый фрагмент сгенерированного C (пример `main.c` / `sub_*.c`), по которому читатель понимает критерий успеха.
- Неочевидные проверки в теле теста сопровождай однострочным комментарием у `Assert`.
- Round-trip тесты (`QuickCProgramRoundTripTests`) требуют DOSBox-X с QuickC; известные сбои перечислены в `QuickC/PROGRAMS/roundtrip_xfail.txt`.

Запуск тестов:
```powershell
dotnet test                              # все проекты
dotnet test DecompilerTests              # только ядро
dotnet test LibParserTests
dotnet test LibMatchingTests
```

Сборка и проверка форматирования:
```powershell
dotnet build -c Release
dotnet format --verify-no-changes   # проверка, что код отформатирован
dotnet format                     # применить форматирование (если нужно)
```

---

## Сборка программ через DOSBox-X и QCL

Эталонный toolchain **Microsoft QuickC 1.0** лежит в каталоге `QuickC/` репозитория (`QCL.EXE`, `LINK.EXE`, `make.exe`, `*.LIB`, `INCLUDE/`). Для автоматизации и round-trip тестов используется **DOSBox-X** (не классический DOSBox).

### Конфигурация эмулятора (`QuickC/dosbox.conf`)

При запуске из каталога `QuickC/` (это **обязательно** — `WorkingDirectory` для всех скриптов и `DosBoxQuickCRunner`):

| Секция | Назначение |
|--------|------------|
| `[sdl] output=dummy` | Без GUI (для CI и тестов) |
| `[dos] log console=quiet` | Минимум шума в консоли |
| `[autoexec]` | `MOUNT C ..` — в `C:` монтируется **родитель** `QuickC/`, т.е. корень репозитория |
| | `SET LIB=C:\QuickC`, `SET INCLUDE=C:\QuickC\INCLUDE`, `SET PATH=C:\QuickC;%PATH%` |
| | `CD \QuickC\PROGRAMS` — стартовый каталог по умолчанию |

Внутри DOS пути выглядят как `C:\QuickC\...` (регистр не важен).

### Шаблон вызова из PowerShell/cmd

Рабочий каталог — `QuickC/`. Каждая DOS-команда — отдельный аргумент `-c`; в конце обязательно `-c exit`:

```powershell
cd QuickC
dosbox-x -conf dosbox.conf -nopromptfolder -fastlaunch -silent `
  -c "CD C:\QuickC\PROGRAMS" `
  -c "QCL /nologo /AS /Gs /Od hello.c /FeHELLO.EXE SLIBCE.LIB" `
  -c exit
```

Флаги `-nopromptfolder -fastlaunch` убирают диалоги; `-silent` подавляет лишний вывод (для отладки убери `-silent` и перенаправь stdout, как в `build_examples.cmd`).

### Типичные флаги QCL (как в тестах и `MakefileGenerator`)

| Параметр | Флаг / значение |
|----------|-----------------|
| Модель small | `/AS` (также `/AC`, `/AM`, `/AL` для compact/medium/large) |
| Без проверки стека | `/Gs` (эталон для большинства `PROGRAMS/*.c`) |
| Без оптимизации | `/Od` |
| CRT small + эмулятор | `SLIBCE.LIB` |
| Доп. заголовки DOS | `LIBH.LIB` (см. `QuickC/PROGRAMS/Makefile`) |

Полная строка для простого примера: `QCL /nologo /AS /Gs /Od hello.c /FeHELLO.EXE SLIBCE.LIB`.

### Готовые скрипты в `QuickC/`

| Скрипт | Действие |
|--------|----------|
| `build_examples.cmd` | `make clean` + `make` в `PROGRAMS/` — собирает все `*.c` по `PROGRAMS/Makefile` |
| `build_decompiled.cmd` | То же для каталога декомпилированного вывода (`decomp~1` — 8.3-имя подкаталога) |
| `run_dosbox.cmd` | Интерактивная сессия DOSBox-X (без `-silent`) |

Вывод `build_examples.cmd` пишется в `build.log` (префикс `LOG:`) и выводится в консоль.

### Сборка одного файла в произвольный каталог

1. Создать каталог на хосте (например `QuickC/PROGRAMS/mytest/`).
2. Узнать **8.3-имя** подкаталога: `cmd /c "dir /x QuickC\PROGRAMS"` — DOS не всегда понимает длинные имена вроде `_hello_out`, нужно `_HELLO~1`.
3. Скомпилировать:

```powershell
dosbox-x -conf dosbox.conf -nopromptfolder -fastlaunch -silent `
  -c "CD C:\QuickC\PROGRAMS\_HELLO~1" `
  -c "QCL /nologo /AS /Gs /Od main.c /FeHELLO.EXE SLIBCE.LIB" `
  -c exit
```

Имя EXE (stem) — **не длиннее 8 символов** (ограничение DOS 8.3).

После работы удалить временные каталоги.

### Пересборка декомпилированного проекта (MAKE)

После `dotnet run --project Tools -- decompile-c path\to\file.exe -o outdir`:

1. В `outdir` появляются `*.c` и `Makefile` (генерирует `MakefileGenerator`).
2. В DOSBox: `CD` в 8.3-путь к `outdir`, затем `MAKE` (используется `QuickC/make.exe`).
3. Round-trip тесты (`QuickCProgramRoundTripTests`) делают то же в `QuickC/RT/{8символов}/OUT/` и сравнивают EXE побайтово.

Пример полного цикла вручную:

```powershell
# 1. Эталонный EXE
cd QuickC
dosbox-x -conf dosbox.conf -nopromptfolder -fastlaunch -silent `
  -c "CD C:\QuickC\PROGRAMS" `
  -c "QCL /nologo /AS /Gs hello.c /FeHELLO.EXE SLIBCE.LIB" -c exit

# 2. Декомпиляция
cd ..
dotnet run --project Tools -- decompile-c QuickC\PROGRAMS\HELLO.EXE -o QuickC\PROGRAMS\decompiled

# 3. Пересборка (8.3-имя decompiled — смотри dir /x)
cd QuickC
dosbox-x -conf dosbox.conf -nopromptfolder -fastlaunch -silent `
  -c "CD C:\QuickC\PROGRAMS\DECOMP~1" -c "MAKE" -c exit
```

### Из кода и тестов (предпочтительно для агента)

| API | Назначение |
|-----|------------|
| `DosBoxQuickCRunner.Run("cmd1", "cmd2", …)` | Выполнить DOS-команды; `WorkingDirectory` = `QuickC/`; stdout+stderr в `RunResult.Output` |
| `ExeProvider.Get("hello.c", …)` | Собрать EXE в кэш `QuickC/BUILT/` (изолированный `TMP/{id}/`, затем копия в кэш) |
| `QuickCTestAssets` | Пути к `PROGRAMS/`, `INCLUDE/`, `LIB/`, `RT/`, `BUILT/` |
| `DosBoxQuickCAssets` | Поиск `dosbox-x.exe` (`PATH` / `DOSBOX_X_PATH`) |

Пример из тестов:

```csharp
DosBoxQuickCRunner.Run(
    $@"CD C:\QuickC\RT\{workspaceId}",
    $"QCL /nologo /AS /Gs hello.c /FeHELLO.EXE SLIBCE.LIB");
// … декомпиляция …
DosBoxQuickCRunner.Run($@"CD C:\QuickC\RT\{workspaceId}\OUT", "MAKE");
```

Запуск round-trip тестов (нужен DOSBox-X):

```powershell
dotnet test DecompilerTests --filter "FullyQualifiedName~QuickCProgramRoundTripTests"
```

Известные сбои — `QuickC/PROGRAMS/roundtrip_xfail.txt`; отчёт — `QuickC/PROGRAMS/ROUNDTRIP_REPORT.md`.

### Частые ошибки

- **«DOSBox-X не найден»** — не в `PATH`, задай `DOSBOX_X_PATH`.
- **QCL не создал EXE** — смотри `RunResult.Output` / `build.log`; часто неверный `CD` или длинное имя каталога без 8.3.
- **Запуск не из `QuickC/`** — `MOUNT C ..` сломается, пути `C:\QuickC` не существует.
- **Линковка** — не забудь `.LIB` (минимум `SLIBCE.LIB` для small + эмулятор math).

---

## Текущее состояние и известные ограничения

Актуальный и подробный список нереализованных возможностей, ограничений и задач проекта находится в файле **[TODO.md](./TODO.md)**.

Этот файл (`AGENTS.md`) содержит только высокоуровневую архитектурную информацию. Все конкретные TODO и их статус отслеживаются отдельно.

**Кратко:** генерация C-кода и Makefile реализованы; round-trip проходит для простых программ (`hello.c`, `add.c`). Качество кодогенерации для сложных случаев (far-указатели, глобалы, long-арифметика) — в активной доработке. См. `ROUNDTRIP_REPORT.md`.

---

## Полезные практики при работе с проектом

- **При добавлении новой инструкции**:
  1. Добавь декодирование в `X86Disassembler.DecodeOneInstruction()`.
  2. Обнови `Instruction.Registers.cs` (эффекты на регистры/флаги).
  3. Добавь или расширь handler в `InstructionHandlers/` и зарегистрируй в `Handlers.cs`.
  4. При необходимости — форматирование в `Operations/Extensions.cs`.
  5. Напиши тесты в `Disassembler/...` и `Expressions/...`.

- **При работе с регистрами** — всегда используй `RegisterExpressions.Set16/Set8/Get16/Get8`. Не трогай поля напрямую.

- **Для тестов с символическими переменными** используй перегрузку:
  ```csharp
  BuildExpressions(hex, vars => RegisterExpressions.InitCom(vars) with { AX = vars.CreateVariable("input") });
  ```

- Визуализация CFG (требуется Graphviz в PATH):
  ```csharp
  cfg.SaveDot("cfg.dot");
  ```

- При отладке ExpressionBuilder полезно смотреть `block.InitRegisters` → `block.EndRegisters` и `block.Operations`.

- **При работе с `.LIB` и сопоставлением**:
  1. Проверь разбор модуля через `dotnet run --project Tools -- lib QuickC\CLIBC.LIB -s _printf`.
  2. FIXUPP → `OmfRelocationTableBuilder.Build` перед дизассемблированием кода модуля.
  3. Сравнение тел — `FunctionBodyComparer` (rel16 маскируются, near CALL/JMP игнорируют абсолютный target).
  4. Диагностика IR: `dotnet run --project Tools -- decompile-main path\to\hello.exe`.
  5. Полная декомпиляция: `dotnet run --project Tools -- decompile-c path\to\hello.exe`.

- **При доработке кодогенерации** — проверяй и unit-тесты (`CCodeGeneratorTests`, `*DecompileTests`), и round-trip (если доступен DOSBox-X; см. раздел «Сборка программ через DOSBox-X и QCL»).

---

## Что НЕ нужно делать

- Не добавляй поддержку 32-бит / protected mode — проект строго для 8086 real mode.
- Не ослабляй `WarningsAsErrors` и правила .editorconfig.
- Не ломай совместимость с QuickC 1.0 в генерируемом C (far/near, модели памяти, имена runtime из `.LIB`).
- Не дублируй логику LibMatching вне `UltraDecompiler/LibMatching/` — используй `LibraryProvider`.

---
