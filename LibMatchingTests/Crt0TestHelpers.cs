using LibParser.Models;

namespace LibMatchingTests;

/// <summary>Вспомогательные методы для тестов модуля crt0 (C runtime startup).</summary>
internal static class Crt0TestHelpers
{
    /// <summary>Модуль crt0 из OMF-библиотеки QuickC.</summary>
    public static OmfModule GetCrt0Module(OmfLibrary library) =>
        library.Modules.First(static m =>
            m.DisplayName.Equals("crt0", StringComparison.OrdinalIgnoreCase)
            || m.HeaderName.Contains("CRT0", StringComparison.OrdinalIgnoreCase));

    /// <summary>Страница модуля crt0 в словаре .LIB.</summary>
    public static ushort GetCrt0ModulePage(OmfLibrary library) =>
        GetCrt0Module(library).PageNumber;
}
