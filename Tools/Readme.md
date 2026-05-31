# Tools — CLI UltraDecompiler

Единая точка входа для утилит. Используется [McMaster.Extensions.CommandLineUtils](https://github.com/natemcmaster/CommandLineUtils).

## Сборка и запуск

```powershell
dotnet run --project Tools -- <команда> [аргументы]
```

После сборки: `Tools\bin\Debug\net10.0\Tools.exe` (или `udc` при установке как tool).

## Команды

### `decompile` — декомпиляция DOS EXE/COM

```powershell
dotnet run --project Tools -- decompile game.exe
dotnet run --project Tools -- decompile game.exe -o 0x100
dotnet run --project Tools -- decompile game.exe --offset 200h
```

| Опция | Описание |
|-------|----------|
| `-o`, `--offset <OFFSET>` | Стартовое смещение (hex `0x100` / `100h` или decimal). По умолчанию — точка входа |

Парсинг MZ/COM, дизассемблирование, CFG (`asm.dot` / `asm.svg`), ExpressionBuilder (`expr.dot` / `expr.svg`), вывод операций в консоль. Для SVG нужен Graphviz `dot` в PATH.

### `decompile-main` — декомпиляция с сопоставлением crt0

```powershell
dotnet run --project Tools -- decompile-main game.exe
dotnet run --project Tools -- decompile-main game.exe -l C:\QuickC
```

| Опция | Описание |
|-------|----------|
| `-l`, `--lib-dir <DIR>` | Каталог с OMF `.LIB` (по умолчанию — `QuickC/` в корне репозитория) |

Сопоставляет точку входа EXE со всеми `.LIB` каталога, выводит таблицу совпадений crt0/`__astart`, находит адрес `__astart` в образе и прогоняет дизассемблирование + ExpressionBuilder (как `decompile`).

### `lib` — разбор OMF .LIB (QuickC)

```powershell
dotnet run --project Tools -- lib C:\QuickC\CLIBC.LIB
dotnet run --project Tools -- lib C:\QuickC\CLIBC.LIB -l
dotnet run --project Tools -- lib C:\QuickC\CLIBC.LIB -s _printf
```

| Опция | Описание |
|-------|----------|
| `-l`, `--list-modules` | Список всех модулей и публичных символов словаря по каждому модулю |
| `-s`, `--symbol <NAME>` | Поиск символа и сведения о модуле |

Справка: `dotnet run --project Tools -- --help` или `decompile --help`, `decompile-main --help`, `lib --help`.
