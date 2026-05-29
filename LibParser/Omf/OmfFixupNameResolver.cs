namespace LibParser.Omf;

using LibParser.Models;

/// <summary>Подстановка имён SEGDEF/EXTDEF в ссылки FIXUPP.</summary>
internal static class OmfFixupNameResolver
{
    public static OmfFixup WithResolvedNames(
        OmfFixup fixup,
        IReadOnlyList<string> segmentNames,
        IReadOnlyDictionary<int, string> externalNames)
    {
        return fixup with
        {
            Frame = ResolveReference(fixup.Frame, segmentNames, externalNames),
            Target = ResolveReference(fixup.Target, segmentNames, externalNames),
        };
    }

    private static OmfFixupReference ResolveReference(
        OmfFixupReference reference,
        IReadOnlyList<string> segmentNames,
        IReadOnlyDictionary<int, string> externalNames)
    {
        var name = reference.Kind switch
        {
            OmfFixupDatumKind.Segdef => GetSegmentName(segmentNames, reference.Index),
            OmfFixupDatumKind.Extdef => externalNames.GetValueOrDefault(reference.Index),
            _ => null,
        };

        if (name is null)
        {
            return reference;
        }

        return new OmfFixupReference
        {
            Kind = reference.Kind,
            Index = reference.Index,
            Name = name,
            Displacement = reference.Displacement,
            FromThread = reference.FromThread,
            ThreadNumber = reference.ThreadNumber,
        };
    }

    private static string? GetSegmentName(IReadOnlyList<string> segmentNames, int index)
    {
        if (index <= 0 || index > segmentNames.Count)
        {
            return null;
        }

        return segmentNames[index - 1];
    }
}
