using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// List Objects version 2's continuation token (ADR-0110, decision 6): the base64url, without padding, of
/// the UTF-8 bytes of the last key or common prefix a page returned, so a listing resumes after it
/// whatever was written in between.
/// </summary>
internal static class GcsContinuationToken
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The token that resumes after <paramref name="lastName"/>.
    /// </summary>
    /// <param name="lastName">The last key or common prefix returned.</param>
    /// <returns>The token.</returns>
    public static string Encode(string lastName) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(lastName)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Reads a token back into the name it resumes after.
    /// </summary>
    /// <param name="token">The token as the client sent it.</param>
    /// <returns>The name, or <see langword="null"/> when the token is not base64url of UTF-8.</returns>
    public static string? Decode(string token)
    {
        var base64 = token.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - (base64.Length % 4)) % 4);
        var bytes = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, bytes, out var written))
        {
            return null;
        }

        try
        {
            return StrictUtf8.GetString(bytes, 0, written);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
