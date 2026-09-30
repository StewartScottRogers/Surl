namespace Surl.Protocol.Ssh;

/// <summary>
/// One side's <c>SSH_MSG_KEXINIT</c> (RFC 4253, section 7.1): its cookie and the ten
/// algorithm name-lists, in that side's order of preference.
/// </summary>
/// <param name="Cookie">The 16 random bytes that start the message.</param>
/// <param name="KeyExchange">The key exchange methods, with any pseudo-algorithms such as <c>ext-info-c</c>.</param>
/// <param name="ServerHostKey">The host-key algorithms.</param>
/// <param name="CipherClientToServer">The ciphers for client-to-server packets.</param>
/// <param name="CipherServerToClient">The ciphers for server-to-client packets.</param>
/// <param name="MacClientToServer">The MACs for client-to-server packets.</param>
/// <param name="MacServerToClient">The MACs for server-to-client packets.</param>
/// <param name="CompressionClientToServer">The compression methods for client-to-server packets.</param>
/// <param name="CompressionServerToClient">The compression methods for server-to-client packets.</param>
/// <param name="LanguagesClientToServer">The language tags for client-to-server text.</param>
/// <param name="LanguagesServerToClient">The language tags for server-to-client text.</param>
/// <param name="FirstKexPacketFollows">Whether a guessed key exchange packet follows at once.</param>
internal sealed record SshKexInit(
    byte[] Cookie,
    IReadOnlyList<string> KeyExchange,
    IReadOnlyList<string> ServerHostKey,
    IReadOnlyList<string> CipherClientToServer,
    IReadOnlyList<string> CipherServerToClient,
    IReadOnlyList<string> MacClientToServer,
    IReadOnlyList<string> MacServerToClient,
    IReadOnlyList<string> CompressionClientToServer,
    IReadOnlyList<string> CompressionServerToClient,
    IReadOnlyList<string> LanguagesClientToServer,
    IReadOnlyList<string> LanguagesServerToClient,
    bool FirstKexPacketFollows)
{
    /// <summary>The length of <see cref="Cookie"/> in bytes.</summary>
    public const int CookieLength = 16;

    /// <summary>
    /// The server's <c>KEXINIT</c>: each list of <paramref name="offer"/> in both directions,
    /// no languages and no guessed packet.
    /// </summary>
    /// <param name="offer">The algorithms offered.</param>
    /// <param name="randomSource">Where the cookie comes from.</param>
    /// <returns>The message.</returns>
    public static SshKexInit ForServer(SshAlgorithmOffer offer, ISshRandomSource randomSource)
    {
        var cookie = new byte[CookieLength];
        randomSource.Fill(cookie);

        return new SshKexInit(
            cookie,
            offer.KeyExchange,
            offer.ServerHostKey,
            offer.Cipher,
            offer.Cipher,
            offer.Mac,
            offer.Mac,
            offer.Compression,
            offer.Compression,
            [],
            [],
            FirstKexPacketFollows: false);
    }

    /// <summary>
    /// Reads a <c>KEXINIT</c> payload, message number first. The reserved <c>uint32</c> that
    /// ends it is read and ignored; bytes after it are ignored too, as RFC 4253 section 7.1
    /// leaves room for extension.
    /// </summary>
    /// <param name="payload">The packet payload.</param>
    /// <returns>The message.</returns>
    /// <exception cref="SshDisconnectRequiredException">The payload ends early: <c>DISCONNECT</c> 2.</exception>
    public static SshKexInit Parse(ReadOnlyMemory<byte> payload)
    {
        var reader = new SshWireReader(payload);
        reader.ReadByte();
        var kexInit = new SshKexInit(
            reader.ReadBytes(CookieLength).ToArray(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            FirstKexPacketFollows: reader.ReadBoolean());
        reader.ReadUInt32();

        return kexInit;
    }

    /// <summary>
    /// Writes the message as a packet payload, message number first, reserved field 0.
    /// </summary>
    /// <returns>The payload.</returns>
    public byte[] ToPayload()
    {
        var writer = new SshWireWriter();
        writer.WriteByte(SshMessageNumber.KeyExchangeInit);
        writer.WriteBytes(Cookie);
        foreach (var names in new[]
        {
            KeyExchange, ServerHostKey, CipherClientToServer, CipherServerToClient, MacClientToServer,
            MacServerToClient, CompressionClientToServer, CompressionServerToClient, LanguagesClientToServer,
            LanguagesServerToClient,
        })
        {
            writer.WriteNameList(names);
        }

        writer.WriteBoolean(FirstKexPacketFollows);
        writer.WriteUInt32(0);

        return writer.ToArray();
    }
}
