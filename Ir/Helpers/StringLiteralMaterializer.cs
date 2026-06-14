using UltraDecompiler.Disassembly.Parser;

namespace UltraDecompiler.Ir.Helpers;

/// <summary>
/// Восстановление строковых литералов (char*) из near-указателей в плоском образе EXE/COM.
/// </summary>
public static class StringLiteralMaterializer
{
    /// <summary>
    /// Читает C-строку по вычисленному адресу: <c>layout.DataSegmentOffset + nearOffset</c>.
    /// </summary>
    public static StringExpr? TryMaterialize(byte[] image, Expr addressExpr, ExeImageLayout layout)
    {
        if (addressExpr is StringExpr existing)
            return existing;

        int nearOffset = GetLogicalAddress(addressExpr);
        if (nearOffset < 0 || image.Length == 0)
            return null;

        int phys = layout.ToImageOffset(nearOffset);
        return TryReadCString(image, phys);
    }

    private static int GetLogicalAddress(Expr expr)
    {
        switch (expr)
        {
            case ConstExpr c:
                return c.Value;
            case ImageOffsetExpr off:
                return off.Value;
            case Math2Expr { Operation: Math2Operation.Add, First: var baseExpr, Second: ConstExpr offset }:
                {
                    int b = GetLogicalAddress(baseExpr);
                    return b >= 0 ? b + offset.Value : -1;
                }
            default:
                return -1;
        }
    }

    private static StringExpr? TryReadCString(byte[] image, int phys)
    {
        if (phys < 0 || phys >= image.Length)
            return null;

        var bytes = new List<byte>(64);
        for (int i = phys; i < image.Length; i++)
        {
            byte b = image[i];
            if (b == 0)
                break;
            bytes.Add(b);
            if (bytes.Count > 128)
                break;
        }

        if (bytes.Count == 0)
            return null;

        byte first = bytes[0];
        if (first < 32 || first >= 127)
            return null;

        if (bytes.Count < 2 && first != (byte)'%')
            return null;

        int good = bytes.Count(bb =>
            (bb >= 32 && bb < 127) || bb == '\n' || bb == '\r' || bb == '\t');
        if (good < bytes.Count * 0.6)
            return null;

        return new StringExpr(System.Text.Encoding.ASCII.GetString(bytes.ToArray()));
    }
}
