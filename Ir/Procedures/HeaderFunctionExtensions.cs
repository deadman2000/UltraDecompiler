namespace UltraDecompiler.Ir.Procedures;

/// <summary>Преобразование объявлений из заголовков в сигнатуры процедур IR.</summary>
public static class HeaderFunctionExtensions
{
    /// <summary>Строит <see cref="ProcedureSignature"/> с cdecl-смещениями на стеке QuickC.</summary>
    public static ProcedureSignature ToProcedureSignature(this HeaderFunction function)
    {
        var stackOffset = 4;
        var parameters = new List<ProcedureParameter>(function.Parameters.Count);
        foreach (var parameter in function.Parameters)
        {
            parameters.Add(new ProcedureParameter(parameter.Type, new StackParameter(stackOffset)));
            stackOffset += 2;
        }

        return new ProcedureSignature(function.ReturnType, parameters, function.IsVariadic);
    }

    /// <summary>Ищет функцию в каталоге и преобразует её в сигнатуру процедуры.</summary>
    public static bool TryGetProcedureSignature(
        this HeaderCatalog catalog,
        string cName,
        out ProcedureSignature? signature)
    {
        if (catalog.TryGetFunction(cName, out var function) && function is not null)
        {
            signature = function.ToProcedureSignature();
            return true;
        }

        signature = null;
        return false;
    }
}
