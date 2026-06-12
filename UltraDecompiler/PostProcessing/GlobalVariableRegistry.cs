using UltraDecompiler.Decompilation;
using UltraDecompiler.Parser;

namespace UltraDecompiler.PostProcessing;

/// <summary>
/// Реестр глобальных переменных DGROUP, восстановленных по near-смещениям в образе EXE.
/// </summary>
public sealed class GlobalVariableRegistry
{
    private readonly Dictionary<int, Variable> _byNearOffset = new();
    private int _nameCounter;

    /// <summary>Все глобалы в порядке возрастания near-смещения (для стабильного вывода C).</summary>
    public IReadOnlyList<Variable> All =>
        _byNearOffset.OrderBy(static kv => kv.Key).Select(static kv => kv.Value).ToList();

    /// <summary>
    /// Возвращает (или создаёт) глобальную переменную для near-смещения в DGROUP.
    /// </summary>
    public Variable GetOrCreate(int nearOffset, byte[] image, ExeImageLayout layout)
    {
        if (_byNearOffset.TryGetValue(nearOffset, out var existing))
        {
            return existing;
        }

        var variable = new Variable(Name: $"global{++_nameCounter}", IsGlobal: true)
        {
            Type = CType.Int,
            InitialValue = ReadInitialInt16(image, layout.ToImageOffset(nearOffset)),
        };
        _byNearOffset[nearOffset] = variable;
        return variable;
    }

    private static int ReadInitialInt16(byte[] image, int imageOffset)
    {
        if (imageOffset < 0 || imageOffset + 1 >= image.Length)
        {
            return 0;
        }

        return image[imageOffset] | (image[imageOffset + 1] << 8);
    }
}
