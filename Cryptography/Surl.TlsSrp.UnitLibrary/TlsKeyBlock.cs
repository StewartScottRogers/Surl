using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>
/// The keys RFC 5246 section 6.3 cuts from the key block for one of RFC 5054's suites: the client's
/// and the server's HMAC-SHA1 keys, then the client's and the server's encryption keys. A TLS 1.2
/// CBC record carries its own explicit IV, so no IV is cut (ADR-0122 decision 2).
/// </summary>
public sealed class TlsKeyBlock
{
    private TlsKeyBlock(TlsBulkCipher cipher, byte[] keyBlock)
    {
        const int macLength = TlsSrpCipherSuites.MacLength;
        var keyLength = TlsSrpCipherSuites.KeyLengthOf(cipher);
        Cipher = cipher;
        ClientMacKey = keyBlock[..macLength];
        ServerMacKey = keyBlock[macLength..(2 * macLength)];
        ClientKey = keyBlock[(2 * macLength)..((2 * macLength) + keyLength)];
        ServerKey = keyBlock[((2 * macLength) + keyLength)..];
    }

    /// <summary>The bulk cipher the keys are for.</summary>
    public TlsBulkCipher Cipher { get; }

    /// <summary><c>client_write_MAC_key</c>: 20 bytes.</summary>
    public byte[] ClientMacKey { get; }

    /// <summary><c>server_write_MAC_key</c>: 20 bytes.</summary>
    public byte[] ServerMacKey { get; }

    /// <summary><c>client_write_key</c>: the cipher's key length.</summary>
    public byte[] ClientKey { get; }

    /// <summary><c>server_write_key</c>: the cipher's key length.</summary>
    public byte[] ServerKey { get; }

    /// <summary>The length of the key block <paramref name="suite"/> needs: two MAC keys and two encryption keys.</summary>
    /// <param name="suite">One of RFC 5054's suites.</param>
    /// <returns>The key block's length in bytes.</returns>
    public static int LengthFor(TlsSrpCipherSuite suite) =>
        2 * (TlsSrpCipherSuites.MacLength + TlsSrpCipherSuites.KeyLengthOf(TlsSrpCipherSuites.BulkCipherOf(suite)));

    /// <summary>Derives the key block for <paramref name="suite"/> and cuts it into its keys.</summary>
    /// <param name="suite">The negotiated suite.</param>
    /// <param name="masterSecret">The 48-byte master secret.</param>
    /// <param name="clientRandom">The <c>ClientHello</c>'s random.</param>
    /// <param name="serverRandom">The <c>ServerHello</c>'s random.</param>
    /// <returns>The four keys.</returns>
    public static TlsKeyBlock Derive(
        TlsSrpCipherSuite suite, ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> serverRandom) =>
        new(
            TlsSrpCipherSuites.BulkCipherOf(suite),
            TlsPseudorandomFunction.KeyBlock(masterSecret, clientRandom, serverRandom, LengthFor(suite)));

    /// <summary>The protection for records the client writes, which the server reads.</summary>
    /// <param name="encryptThenMac">Whether RFC 7366's encrypt-then-MAC was negotiated.</param>
    /// <param name="ivSource">Where explicit IVs come from.</param>
    /// <returns>A new protection with its sequence number at zero.</returns>
    public TlsCbcRecordProtection ClientWriteProtection(bool encryptThenMac, RandomNumberGenerator ivSource) =>
        new(Cipher, ClientKey, ClientMacKey, encryptThenMac, ivSource);

    /// <summary>The protection for records the server writes.</summary>
    /// <param name="encryptThenMac">Whether RFC 7366's encrypt-then-MAC was negotiated.</param>
    /// <param name="ivSource">Where explicit IVs come from.</param>
    /// <returns>A new protection with its sequence number at zero.</returns>
    public TlsCbcRecordProtection ServerWriteProtection(bool encryptThenMac, RandomNumberGenerator ivSource) =>
        new(Cipher, ServerKey, ServerMacKey, encryptThenMac, ivSource);
}
