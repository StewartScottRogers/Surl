using System.Security.Cryptography;
using System.Text;

namespace Surl.Tls13;

/// <summary>
/// The TLS 1.3 key schedule of RFC 8446 section 7.1 without a pre-shared key: the early,
/// handshake and master secrets, the handshake and application traffic secrets, and the Finished
/// keys and verify data of section 4.4.4. The hash is the cipher suite's; HKDF and HMAC are the
/// base class library's.
/// </summary>
public sealed class TlsKeySchedule
{
    private const string LabelPrefix = "tls13 ";

    private byte[]? handshakeSecret;
    private byte[]? masterSecret;

    /// <summary>Starts the schedule for a cipher suite, computing the early secret with no pre-shared key.</summary>
    /// <param name="cipherSuite">The negotiated cipher suite, which names the hash.</param>
    /// <exception cref="ArgumentOutOfRangeException">The suite is not a TLS 1.3 cipher suite.</exception>
    public TlsKeySchedule(TlsCipherSuite cipherSuite)
    {
        HashAlgorithm = HashAlgorithmOf(cipherSuite);
        HashLength = HashAlgorithm == HashAlgorithmName.SHA384 ? 48 : 32;
        var zeros = new byte[HashLength];
        EarlySecret = HKDF.Extract(HashAlgorithm, zeros, zeros);
    }

    /// <summary>The hash the cipher suite names: SHA-256, or SHA-384 for TLS_AES_256_GCM_SHA384.</summary>
    public HashAlgorithmName HashAlgorithm { get; }

    /// <summary>The hash's output length in bytes: 32 or 48.</summary>
    public int HashLength { get; }

    /// <summary>The early secret: HKDF-Extract of zeros, with no pre-shared key.</summary>
    public byte[] EarlySecret { get; }

    /// <summary>The handshake secret, once <see cref="DeriveHandshakeTrafficSecrets"/> has run.</summary>
    /// <exception cref="InvalidOperationException">The handshake traffic secrets have not been derived yet.</exception>
    public byte[] HandshakeSecret => handshakeSecret ?? throw NotYetDerived();

    /// <summary>The master secret, once <see cref="DeriveHandshakeTrafficSecrets"/> has run.</summary>
    /// <exception cref="InvalidOperationException">The handshake traffic secrets have not been derived yet.</exception>
    public byte[] MasterSecret => masterSecret ?? throw NotYetDerived();

    /// <summary>The hash a cipher suite names.</summary>
    /// <param name="cipherSuite">A TLS 1.3 cipher suite.</param>
    /// <returns>SHA-384 for TLS_AES_256_GCM_SHA384, SHA-256 for the other two.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The suite is not a TLS 1.3 cipher suite.</exception>
    public static HashAlgorithmName HashAlgorithmOf(TlsCipherSuite cipherSuite) => cipherSuite switch
    {
        TlsCipherSuite.Aes128GcmSha256 or TlsCipherSuite.ChaCha20Poly1305Sha256 => HashAlgorithmName.SHA256,
        TlsCipherSuite.Aes256GcmSha384 => HashAlgorithmName.SHA384,
        _ => throw new ArgumentOutOfRangeException(nameof(cipherSuite), cipherSuite, "Not a TLS 1.3 cipher suite."),
    };

    /// <summary>
    /// HKDF-Expand-Label (RFC 8446 section 7.1): HKDF-Expand of <paramref name="secret"/> with the
    /// HkdfLabel of <paramref name="length"/>, "tls13 " + <paramref name="label"/> and
    /// <paramref name="context"/>. QUIC derives its packet protection keys with it (RFC 9001 section 5.1).
    /// </summary>
    /// <param name="hashAlgorithm">The hash.</param>
    /// <param name="secret">The secret to expand.</param>
    /// <param name="label">The label without its "tls13 " prefix.</param>
    /// <param name="context">The context: a transcript hash, or empty.</param>
    /// <param name="length">The output length in bytes.</param>
    /// <returns>The expanded bytes.</returns>
    public static byte[] ExpandLabel(
        HashAlgorithmName hashAlgorithm, ReadOnlySpan<byte> secret, string label, ReadOnlySpan<byte> context, int length)
    {
        var hkdfLabel = TlsWriter.Body(writer =>
        {
            writer.WriteUInt16(checked((ushort)length));
            writer.WriteVector(1, Encoding.ASCII.GetBytes(LabelPrefix + label));
        });
        var info = new byte[hkdfLabel.Length + 1 + context.Length];
        hkdfLabel.CopyTo(info, 0);
        info[hkdfLabel.Length] = checked((byte)context.Length);
        context.CopyTo(info.AsSpan(hkdfLabel.Length + 1));
        var output = new byte[length];
        HKDF.Expand(hashAlgorithm, secret, output, info);
        return output;
    }

    /// <summary>Derive-Secret (RFC 8446 section 7.1): HKDF-Expand-Label with a transcript hash as context, one hash long.</summary>
    /// <param name="secret">The secret to derive from.</param>
    /// <param name="label">The label without its "tls13 " prefix.</param>
    /// <param name="transcriptHash">Transcript-Hash of the messages the label covers.</param>
    /// <returns>The derived secret.</returns>
    public byte[] DeriveSecret(ReadOnlySpan<byte> secret, string label, ReadOnlySpan<byte> transcriptHash) =>
        ExpandLabel(HashAlgorithm, secret, label, transcriptHash, HashLength);

    /// <summary>
    /// Computes the handshake secret from the (EC)DHE shared secret, then the master secret, and
    /// returns the client and server handshake traffic secrets.
    /// </summary>
    /// <param name="sharedSecret">The key exchange's shared secret.</param>
    /// <param name="helloTranscriptHash">Transcript-Hash(ClientHello...ServerHello).</param>
    /// <returns>The handshake traffic secrets ("c hs traffic" and "s hs traffic").</returns>
    public TlsTrafficSecrets DeriveHandshakeTrafficSecrets(ReadOnlySpan<byte> sharedSecret, ReadOnlySpan<byte> helloTranscriptHash)
    {
        var emptyHash = CryptographicOperations.HashData(HashAlgorithm, []);
        var handshake = HKDF.Extract(HashAlgorithm, sharedSecret.ToArray(), DeriveSecret(EarlySecret, "derived", emptyHash));
        masterSecret = HKDF.Extract(HashAlgorithm, new byte[HashLength], DeriveSecret(handshake, "derived", emptyHash));
        handshakeSecret = handshake;
        return new TlsTrafficSecrets(
            DeriveSecret(handshake, "c hs traffic", helloTranscriptHash),
            DeriveSecret(handshake, "s hs traffic", helloTranscriptHash));
    }

    /// <summary>Returns the client and server application traffic secrets.</summary>
    /// <param name="serverFinishedTranscriptHash">Transcript-Hash(ClientHello...server Finished).</param>
    /// <returns>The application traffic secrets ("c ap traffic" and "s ap traffic").</returns>
    /// <exception cref="InvalidOperationException">The handshake traffic secrets have not been derived yet.</exception>
    public TlsTrafficSecrets DeriveApplicationTrafficSecrets(ReadOnlySpan<byte> serverFinishedTranscriptHash) =>
        new(
            DeriveSecret(MasterSecret, "c ap traffic", serverFinishedTranscriptHash),
            DeriveSecret(MasterSecret, "s ap traffic", serverFinishedTranscriptHash));

    /// <summary>The finished_key of RFC 8446 section 4.4.4: HKDF-Expand-Label(BaseKey, "finished", "", Hash.length).</summary>
    /// <param name="baseKey">The sender's handshake traffic secret.</param>
    /// <returns>The Finished key.</returns>
    public byte[] FinishedKey(ReadOnlySpan<byte> baseKey) =>
        ExpandLabel(HashAlgorithm, baseKey, "finished", [], HashLength);

    /// <summary>A Finished message's verify_data: HMAC(finished_key, Transcript-Hash) (RFC 8446 section 4.4.4).</summary>
    /// <param name="baseKey">The sender's handshake traffic secret.</param>
    /// <param name="transcriptHash">Transcript-Hash of the messages before the Finished.</param>
    /// <returns>The verify_data.</returns>
    public byte[] ComputeFinishedVerifyData(ReadOnlySpan<byte> baseKey, ReadOnlySpan<byte> transcriptHash) =>
        CryptographicOperations.HmacData(HashAlgorithm, FinishedKey(baseKey), transcriptHash.ToArray());

    private static InvalidOperationException NotYetDerived() =>
        new("The handshake traffic secrets have not been derived yet.");
}
