namespace Surl.Protocol.Ssh;

/// <summary>
/// Agrees each algorithm from the two <c>KEXINIT</c> messages as RFC 4253 section 7.1 says:
/// the first name on the client's list that is also on the server's.
/// </summary>
internal static class SshAlgorithmNegotiator
{
    private static readonly string[] AuthenticatedEncryptionCiphers =
    [
        "chacha20-poly1305@openssh.com",
        "aes256-gcm@openssh.com",
        "aes128-gcm@openssh.com",
    ];

    /// <summary>
    /// Agrees every algorithm the session needs. The server's strict key exchange marker is
    /// never agreed as a method, and the MAC lists are not consulted beside an AEAD cipher.
    /// </summary>
    /// <param name="client">The client's <c>KEXINIT</c>.</param>
    /// <param name="server">The server's <c>KEXINIT</c>.</param>
    /// <returns>The agreed algorithms.</returns>
    /// <exception cref="SshDisconnectRequiredException">
    /// A list shares no name with the server's: <c>DISCONNECT</c> 3, noted with the list's
    /// kind and what the client offered (ADR-0051, decisions 9 and 10).
    /// </exception>
    public static SshNegotiatedAlgorithms Negotiate(SshKexInit client, SshKexInit server)
    {
        var keyExchange = Agree("kex", client.KeyExchange, [.. server.KeyExchange.Where(IsKeyExchangeMethod)]);
        var hostKey = Agree("host key", client.ServerHostKey, server.ServerHostKey);
        var cipherClientToServer = Agree("cipher", client.CipherClientToServer, server.CipherClientToServer);
        var cipherServerToClient = Agree("cipher", client.CipherServerToClient, server.CipherServerToClient);
        var macClientToServer = AgreeMac(cipherClientToServer, client.MacClientToServer, server.MacClientToServer);
        var macServerToClient = AgreeMac(cipherServerToClient, client.MacServerToClient, server.MacServerToClient);

        return new SshNegotiatedAlgorithms(
            keyExchange,
            hostKey,
            cipherClientToServer,
            cipherServerToClient,
            macClientToServer,
            macServerToClient,
            Agree("compression", client.CompressionClientToServer, server.CompressionClientToServer),
            Agree("compression", client.CompressionServerToClient, server.CompressionServerToClient),
            IsStrict(client.KeyExchange, server.KeyExchange),
            client.FirstKexPacketFollows && !IsRightGuess(client, server));
    }

    /// <summary>
    /// Whether the connection is held to strict key exchange: both sides list their marker
    /// (OpenSSH <c>PROTOCOL</c>; ADR-0051, decision 2.1), so both reset their sequence numbers.
    /// </summary>
    /// <param name="clientKeyExchange">The client's key exchange list.</param>
    /// <param name="serverKeyExchange">The server's key exchange list.</param>
    /// <returns>Whether the key exchange is strict.</returns>
    public static bool IsStrict(IReadOnlyList<string> clientKeyExchange, IReadOnlyList<string> serverKeyExchange) =>
        clientKeyExchange.Contains(SshAlgorithmOffer.StrictKeyExchangeClientMarker)
        && serverKeyExchange.Contains(SshAlgorithmOffer.StrictKeyExchangeServerMarker);

    private static bool IsKeyExchangeMethod(string name) => name != SshAlgorithmOffer.StrictKeyExchangeServerMarker;

    // The guess is right when both sides put the same key exchange method and the same
    // host-key algorithm first (RFC 4253 section 7, as OpenSSH reads it).
    private static bool IsRightGuess(SshKexInit client, SshKexInit server) =>
        client.KeyExchange.FirstOrDefault() == server.KeyExchange.FirstOrDefault()
        && client.ServerHostKey.FirstOrDefault() == server.ServerHostKey.FirstOrDefault();

    private static string? AgreeMac(string cipher, IReadOnlyList<string> client, IReadOnlyList<string> server) =>
        AuthenticatedEncryptionCiphers.Contains(cipher) ? null : Agree("MAC", client, server);

    private static string Agree(string kind, IReadOnlyList<string> client, IReadOnlyList<string> server) =>
        client.FirstOrDefault(server.Contains)
            ?? throw new SshDisconnectRequiredException(
                SshDisconnectReason.KeyExchangeFailed,
                "No common algorithm",
                $"SSH no common {kind} algorithm; client offered {SshLogText.RenderNameList(client)}");
}
