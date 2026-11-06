namespace Surl.TlsSrp;

/// <summary>
/// A ClientHello's fields as RFC 5246 section 7.4.1.2 lays them out: the version, the random, the
/// legacy session ID, the offered suites in the client's order, the compression methods and the
/// extensions by type.
/// </summary>
/// <param name="ClientVersion">The <c>client_version</c>, e.g. <c>0x0303</c>.</param>
/// <param name="Random">The 32-byte random.</param>
/// <param name="SessionId">The session ID, at most 32 bytes.</param>
/// <param name="CipherSuites">The offered suite codes in the client's order.</param>
/// <param name="CompressionMethods">The offered compression methods.</param>
/// <param name="Extensions">Each extension's data by its type; at most one of each.</param>
public sealed record TlsClientHello(
    ushort ClientVersion,
    byte[] Random,
    byte[] SessionId,
    IReadOnlyList<ushort> CipherSuites,
    byte[] CompressionMethods,
    IReadOnlyDictionary<ushort, byte[]> Extensions)
{
    /// <summary>The length of a hello's random: 32 bytes.</summary>
    public const int RandomLength = 32;

    private const int MaxSessionIdLength = 32;

    /// <summary>Reads a ClientHello's body (the handshake message without its four-byte header).</summary>
    /// <param name="body">The body.</param>
    /// <returns>The ClientHello.</returns>
    /// <exception cref="TlsAlertException">
    /// <see cref="TlsAlertDescription.DecodeError"/> for a short or overlong body, a session ID over
    /// 32 bytes, an odd or empty suite list, no compression method, or two extensions of one type.
    /// </exception>
    public static TlsClientHello Parse(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var reader = new TlsMessageReader(body, "the ClientHello");
        var clientVersion = reader.ReadUInt16();
        var random = reader.ReadBytes(RandomLength);
        var sessionId = reader.ReadVector8();
        Require(sessionId.Length <= MaxSessionIdLength, $"The ClientHello's session ID is {sessionId.Length} bytes, over 32.");
        var suites = ReadCipherSuites(reader.ReadVector16());
        var compressionMethods = reader.ReadVector8();
        Require(compressionMethods.Length > 0, "The ClientHello offers no compression method.");
        var extensions = reader.IsAtEnd ? new Dictionary<ushort, byte[]>() : ReadExtensions(reader.ReadVector16());
        reader.ExpectEnd();
        return new TlsClientHello(clientVersion, random, sessionId, suites, compressionMethods, extensions);
    }

    private static List<ushort> ReadCipherSuites(byte[] list)
    {
        Require(list.Length > 0 && list.Length % 2 == 0, $"The ClientHello's suite list is {list.Length} bytes, not a positive even number.");
        var reader = new TlsMessageReader(list, "the suite list");
        var suites = new List<ushort>(list.Length / 2);
        while (!reader.IsAtEnd)
        {
            suites.Add(reader.ReadUInt16());
        }

        return suites;
    }

    private static Dictionary<ushort, byte[]> ReadExtensions(byte[] block)
    {
        var reader = new TlsMessageReader(block, "the ClientHello's extensions");
        var extensions = new Dictionary<ushort, byte[]>();
        while (!reader.IsAtEnd)
        {
            var type = reader.ReadUInt16();
            Require(extensions.TryAdd(type, reader.ReadVector16()), $"The ClientHello carries extension {type} twice.");
        }

        return extensions;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new TlsAlertException(TlsAlertDescription.DecodeError, message);
        }
    }
}
