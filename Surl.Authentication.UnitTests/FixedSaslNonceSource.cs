namespace Surl.Authentication;

/// <summary>
/// An <see cref="ISaslNonceSource"/> that always gives the bytes ADR-0049 measured upstream curl
/// against: <c>0123456789abcdef</c> as 8 bytes for a <c>CRAM-MD5</c> challenge, and the 16 ASCII
/// bytes of <c>0123456789abcdef</c> (base64 <c>MDEyMzQ1Njc4OWFiY2RlZg==</c>) for a
/// <c>DIGEST-MD5</c> nonce.
/// </summary>
internal sealed class FixedSaslNonceSource : ISaslNonceSource
{
    public static readonly FixedSaslNonceSource Instance = new();

    public byte[] CreateNonce(int length) =>
        length == 8 ? Convert.FromHexString("0123456789ABCDEF") : "0123456789abcdef"u8.ToArray();
}
