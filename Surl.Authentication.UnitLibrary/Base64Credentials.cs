namespace Surl.Authentication;

/// <summary>
/// Decodes the base64 token NTLM and Negotiate carry after their scheme.
/// </summary>
internal static class Base64Credentials
{
    /// <summary>
    /// The bytes <paramref name="credentials"/> encodes.
    /// </summary>
    /// <param name="credentials">What followed the scheme.</param>
    /// <returns>The bytes, or none when it is not base64, so the caller refuses it.</returns>
    public static byte[] Decode(string credentials)
    {
        var buffer = new byte[credentials.Length];

        return Convert.TryFromBase64String(credentials, buffer, out var written) ? buffer[..written] : [];
    }
}
