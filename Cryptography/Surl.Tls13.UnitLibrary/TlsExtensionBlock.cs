namespace Surl.Tls13;

/// <summary>Reads an extension block (RFC 8446 section 4.2): each extension's type and data, in the order they came.</summary>
internal static class TlsExtensionBlock
{
    /// <summary>
    /// Reads every extension of <paramref name="block"/>, the block without its own length. Two
    /// extensions of one type is <see cref="TlsAlert.IllegalParameter"/> (section 4.2: "There MUST
    /// NOT be more than one extension of the same type in a given extension block").
    /// </summary>
    public static List<TlsExtension> Read(ReadOnlySpan<byte> block)
    {
        var reader = new TlsReader(block);
        var extensions = new List<TlsExtension>();
        var seen = new HashSet<ushort>();
        while (!reader.IsAtEnd)
        {
            var type = reader.ReadUInt16();
            var data = reader.ReadVector16(0).ToArray();
            if (!seen.Add(type))
            {
                throw new TlsAlertException(
                    TlsAlert.IllegalParameter,
                    $"Extension 0x{type:X4} appears more than once.");
            }

            extensions.Add(new TlsExtension((TlsExtensionType)type, data));
        }

        return extensions;
    }

    /// <summary>The extension of <paramref name="type"/> in <paramref name="extensions"/>, or null when there is none.</summary>
    public static TlsExtension? Find(IReadOnlyList<TlsExtension> extensions, TlsExtensionType type) =>
        extensions.FirstOrDefault(extension => extension.Type == type);
}
