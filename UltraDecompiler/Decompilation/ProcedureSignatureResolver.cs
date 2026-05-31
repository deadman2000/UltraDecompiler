using UltraDecompiler.Decompilation.Headers;

namespace UltraDecompiler.Decompilation;

/// <summary>Назначает сигнатуры всем процедурам в хранилище (заголовки или анализ тела).</summary>
public static class ProcedureSignatureResolver
{
    /// <summary>
    /// Для библиотечных функций — из <paramref name="headers"/>,
    /// для пользовательских — эвристика по ассемблеру.
    /// </summary>
    public static void ResolveAll(ProcedureStorage storage, QuickCHeaderCatalog headers)
    {
        foreach (var procedure in storage.All.ToList())
        {
            procedure.Signature = Resolve(procedure, headers);
        }
    }

    public static ProcedureSignature Resolve(DisassembledProcedure procedure, QuickCHeaderCatalog headers)
    {
        if (procedure.IsLibrary && headers.TryGetSignature(procedure.Name, out var fromHeader) && fromHeader is not null)
        {
            return fromHeader;
        }

        if (!procedure.IsLibrary)
        {
            return ProcedureSignatureAnalyzer.Analyze(procedure);
        }

        return headers.TryGetSignature(procedure.Name, out var fallback) && fallback is not null
            ? fallback
            : ProcedureSignature.Unknown;
    }
}
