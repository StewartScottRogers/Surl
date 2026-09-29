namespace Surl.Authentication;

/// <summary>
/// Everything besides the <c>A1</c> hash that a <c>qop=auth</c> response covers (RFC 7616
/// section 3.4.1), as received.
/// </summary>
/// <param name="Method">The request method.</param>
/// <param name="Uri">The <c>uri</c> parameter.</param>
/// <param name="Nonce">The <c>nonce</c> parameter.</param>
/// <param name="NonceCount">The <c>nc</c> parameter: eight hex digits.</param>
/// <param name="Cnonce">The <c>cnonce</c> parameter.</param>
/// <param name="Qop">The <c>qop</c> parameter: <c>auth</c>.</param>
internal sealed record DigestResponseInputs(
    string Method, string Uri, string Nonce, string NonceCount, string Cnonce, string Qop);
