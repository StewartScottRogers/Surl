using System.Net;
using System.Security.Cryptography;

namespace Surl.Dtls;

/// <summary>
/// The secret behind the stateless cookie exchange (RFC 6347 section 4.2.1, ADR-0087 decision 4):
/// 32 random bytes, replaced every <see cref="Lifetime"/> by the <see cref="TimeProvider"/>, with
/// the previous secret still accepted, so a cookie lives between 5 and 10 minutes. One instance
/// serves every association of a listener; it is never written anywhere.
/// </summary>
public sealed class DtlsCookieSecrets
{
    /// <summary>How long one secret makes cookies: 5 minutes.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>The length of a secret: 32 bytes.</summary>
    public const int SecretLength = 32;

    /// <summary>The length of a cookie: 32 bytes, the first 32 of an HMAC-SHA256.</summary>
    public const int CookieLength = 32;

    private readonly TimeProvider timeProvider;
    private readonly Func<byte[]> makeSecret;
    private readonly Lock gate = new();
    private byte[] current;
    private byte[]? previous;
    private DateTimeOffset currentMadeAt;

    /// <summary>
    /// Creates the secrets, drawing each from <see cref="RandomNumberGenerator"/>.
    /// </summary>
    /// <param name="timeProvider">The clock that ages the secrets.</param>
    public DtlsCookieSecrets(TimeProvider timeProvider)
        : this(timeProvider, () => RandomNumberGenerator.GetBytes(SecretLength))
    {
    }

    /// <summary>
    /// Creates the secrets, drawing each from <paramref name="makeSecret"/>, so tests can fix them.
    /// </summary>
    /// <param name="timeProvider">The clock that ages the secrets.</param>
    /// <param name="makeSecret">Makes one fresh secret.</param>
    public DtlsCookieSecrets(TimeProvider timeProvider, Func<byte[]> makeSecret)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(makeSecret);
        this.timeProvider = timeProvider;
        this.makeSecret = makeSecret;
        current = makeSecret();
        currentMadeAt = timeProvider.GetUtcNow();
    }

    /// <summary>
    /// The cookie for <paramref name="hello"/> from <paramref name="remoteEndPoint"/>, under the
    /// current secret.
    /// </summary>
    /// <param name="remoteEndPoint">The address and port the hello came from.</param>
    /// <param name="hello">The hello whose fields the cookie covers.</param>
    /// <returns>The 32-byte cookie.</returns>
    public byte[] MakeCookie(EndPoint remoteEndPoint, DtlsClientHello hello)
    {
        lock (gate)
        {
            ReplaceAgedSecret();
            return Compute(current, remoteEndPoint, hello);
        }
    }

    /// <summary>
    /// Whether <paramref name="hello"/>'s cookie is one the current or the previous secret made for
    /// this endpoint and these hello fields.
    /// </summary>
    /// <param name="remoteEndPoint">The address and port the hello came from.</param>
    /// <param name="hello">The hello carrying the cookie.</param>
    /// <returns><see langword="true"/> when the cookie is valid.</returns>
    public bool IsValid(EndPoint remoteEndPoint, DtlsClientHello hello)
    {
        lock (gate)
        {
            ReplaceAgedSecret();
            return Matches(current, remoteEndPoint, hello)
                || (previous is not null && Matches(previous, remoteEndPoint, hello));
        }
    }

    private static bool Matches(byte[] secret, EndPoint remoteEndPoint, DtlsClientHello hello) =>
        CryptographicOperations.FixedTimeEquals(Compute(secret, remoteEndPoint, hello), hello.Cookie);

    /// <summary>
    /// <c>HMAC-SHA256(secret, remote address and port || client_version || random || session_id ||
    /// cipher_suites || compression_methods)</c>, with each field as on the wire.
    /// </summary>
    private static byte[] Compute(byte[] secret, EndPoint remoteEndPoint, DtlsClientHello hello)
    {
        var address = remoteEndPoint.Serialize();
        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteFixed(address.Buffer.Span[..address.Size], address.Size);
        writer.WriteUInt16(hello.ClientVersion);
        writer.WriteFixed(hello.Random, hello.Random.Length);
        writer.WriteVector8(hello.SessionId, 0, byte.MaxValue);
        writer.WriteUInt16Vector16(hello.CipherSuites, 0, ushort.MaxValue);
        writer.WriteVector8(hello.CompressionMethods, 0, byte.MaxValue);
        return HMACSHA256.HashData(secret, writer.ToArray())[..CookieLength];
    }

    private void ReplaceAgedSecret()
    {
        var now = timeProvider.GetUtcNow();
        var age = now - currentMadeAt;
        if (age < Lifetime)
        {
            return;
        }

        if (age < 2 * Lifetime)
        {
            previous = current;
            currentMadeAt += Lifetime;
        }
        else
        {
            previous = null;
            currentMadeAt = now;
        }

        current = makeSecret();
    }
}
