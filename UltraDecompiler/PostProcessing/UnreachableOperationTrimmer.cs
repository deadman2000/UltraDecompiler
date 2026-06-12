namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Удаляет недостижимые операции после первого явного <c>return</c> на верхнем уровне тела функции.
/// </summary>
public static class UnreachableOperationTrimmer
{
    /// <summary>Обрезает хвост списка операций после завершающего return.</summary>
    public static IReadOnlyList<Operation> Trim(IReadOnlyList<Operation> operations)
    {
        var result = new List<Operation>(operations.Count);

        foreach (var operation in operations)
        {
            result.Add(operation);

            if (operation is ReturnOperation)
            {
                break;
            }
        }

        return result;
    }
}
