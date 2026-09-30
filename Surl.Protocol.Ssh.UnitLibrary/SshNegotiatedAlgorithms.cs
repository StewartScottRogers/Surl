namespace Surl.Protocol.Ssh;

/// <summary>
/// The algorithms both sides' <c>KEXINIT</c> messages agree on (RFC 4253, section 7.1).
/// </summary>
/// <param name="KeyExchange">The key exchange method.</param>
/// <param name="ServerHostKey">The host-key algorithm.</param>
/// <param name="CipherClientToServer">The cipher for client-to-server packets.</param>
/// <param name="CipherServerToClient">The cipher for server-to-client packets.</param>
/// <param name="MacClientToServer">The MAC for client-to-server packets; <see langword="null"/> beside an AEAD cipher.</param>
/// <param name="MacServerToClient">The MAC for server-to-client packets; <see langword="null"/> beside an AEAD cipher.</param>
/// <param name="CompressionClientToServer">The compression method for client-to-server packets.</param>
/// <param name="CompressionServerToClient">The compression method for server-to-client packets.</param>
/// <param name="StrictKeyExchange">Whether the client asked for strict key exchange (ADR-0051, decision 2.1).</param>
/// <param name="ClientGuessIsWrong">
/// Whether the client announced a guessed key exchange packet that must be discarded: its
/// first key exchange method or host-key algorithm is not the server's first (RFC 4253, section 7).
/// </param>
internal sealed record SshNegotiatedAlgorithms(
    string KeyExchange,
    string ServerHostKey,
    string CipherClientToServer,
    string CipherServerToClient,
    string? MacClientToServer,
    string? MacServerToClient,
    string CompressionClientToServer,
    string CompressionServerToClient,
    bool StrictKeyExchange,
    bool ClientGuessIsWrong)
{
    /// <summary>
    /// The verbose note ADR-0051 decision 10 writes once the first key exchange is agreed.
    /// </summary>
    /// <returns>The note.</returns>
    public string ToNote() =>
        $"SSH negotiated kex {KeyExchange}, host key {ServerHostKey}, "
        + $"cipher {CipherClientToServer}/{CipherServerToClient}, "
        + $"MAC {MacClientToServer ?? "implicit"}/{MacServerToClient ?? "implicit"}, "
        + $"compression {CompressionClientToServer}/{CompressionServerToClient}, "
        + $"strict kex {(StrictKeyExchange ? "on" : "off")}";
}
