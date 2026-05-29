## Парсер библиотек Quick C

Читает библиотеки формата **OMF .LIB** (записи F0h/F1h, TIS OMF Appendix 2) компилятора Microsoft QuickC.
Извлекает объектные модули, сегменты (LEDATA/LIDATA) и публичный словарь символов — для сопоставления с кодом в декомпилируемом EXE.

**Описание формата OMF и структуры .LIB:** [OMF.md](./OMF.md)

### API

```csharp
using LibParser.Omf;

OmfLibrary lib = OmfLibraryParser.ParseFile(@"C:\QuickC\CLIBC.LIB");
OmfModule? mod = lib.FindModuleBySymbol("_printf"); // ModulePage в словаре = mod.PageNumber
byte[] code = mod?.CodeSegments.FirstOrDefault()?.Data ?? [];

// Релокации FIXUPP (внешние вызовы, ссылки на данные)
foreach (var fix in mod?.Fixups ?? [])
{
    // fix.SegmentOffset — смещение в кодовом сегменте
    // fix.Target.Name — имя из EXTDEF, если цель — внешний символ
}
```

### CLI

Команда `lib` в проекте **Tools** (см. `Tools/Commands/LibCommand.cs`):

```powershell
dotnet run --project Tools -- lib C:\QuickC\CLIBC.LIB -s _printf
dotnet run --project Tools -- lib C:\QuickC\CLIBC.LIB -l
```

### Тесты

```powershell
dotnet test LibParserTests
```

Используются эталонные `.LIB` из каталога `QuickC/` в корне репозитория.
