using System.Security.Cryptography;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The random source the server uses unless given another: the operating system's
/// cryptographic generator, so stream IDs are unpredictable (RFC 6120 section 4.7.3).
/// </summary>
public sealed class CryptographicXmppRandomSource : IXmppRandomSource
{
    private CryptographicXmppRandomSource()
    {
    }

    /// <summary>The one instance; it holds no state.</summary>
    public static CryptographicXmppRandomSource Instance { get; } = new();

    /// <inheritdoc/>
    public string NextHexDigits(int count) => RandomNumberGenerator.GetHexString(count, lowercase: true);
}
