namespace Surl.Dtls;

/// <summary>
/// One hello extension (RFC 5246 section 7.4.1.4): its type and its opaque data, read and written
/// as the list that ends a <c>ClientHello</c> or <c>ServerHello</c>.
/// </summary>
/// <param name="Type">The extension's number; one ADR-0087 does not name is carried as it came.</param>
/// <param name="Data">The extension's data, at most 2^16 - 1 bytes.</param>
public sealed record DtlsExtension(DtlsExtensionType Type, byte[] Data)
{
    private const int MaximumListLength = ushort.MaxValue;

    /// <summary>
    /// Reads the optional extension list at the end of a hello: none when no byte is left, else a
    /// two-byte-length list in which no type may repeat (RFC 5246 section 7.4.1.4).
    /// </summary>
    internal static IReadOnlyList<DtlsExtension> ReadList(ref DtlsHandshakeBodyReader reader)
    {
        if (reader.IsAtEnd)
        {
            return [];
        }

        var listReader = new DtlsHandshakeBodyReader(reader.ReadVector16(0, MaximumListLength));
        var extensions = new List<DtlsExtension>();
        var seen = new HashSet<DtlsExtensionType>();
        while (!listReader.IsAtEnd)
        {
            var extension = new DtlsExtension(
                (DtlsExtensionType)listReader.ReadUInt16(), listReader.ReadVector16(0, MaximumListLength));
            if (!seen.Add(extension.Type))
            {
                throw new DtlsDecodeErrorException($"Extension {(ushort)extension.Type} appears twice.");
            }

            extensions.Add(extension);
        }

        return extensions;
    }

    /// <summary>
    /// Writes the extension list, or nothing at all when it is empty, as RFC 5246 section 7.4.1.2
    /// allows a hello without extensions to end at its last fixed field.
    /// </summary>
    internal static void WriteList(DtlsHandshakeBodyWriter writer, IReadOnlyList<DtlsExtension> extensions)
    {
        if (extensions.Count == 0)
        {
            return;
        }

        var listWriter = new DtlsHandshakeBodyWriter();
        foreach (var extension in extensions)
        {
            listWriter.WriteUInt16((ushort)extension.Type);
            listWriter.WriteVector16(extension.Data, 0, MaximumListLength);
        }

        writer.WriteVector16(listWriter.ToArray(), 0, MaximumListLength);
    }
}
