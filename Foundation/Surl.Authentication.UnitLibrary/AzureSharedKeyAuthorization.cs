namespace Surl.Authentication;

/// <summary>
/// An Azure <c>Authorization: SharedKey &lt;account&gt;:&lt;signature&gt;</c> or
/// <c>SharedKeyLite &lt;account&gt;:&lt;signature&gt;</c> field ("Authorize with Shared Key").
/// </summary>
/// <param name="IsLite"><see langword="true"/> for <c>SharedKeyLite</c>, signed over the shorter string.</param>
/// <param name="AccountName">The storage account the signature names.</param>
/// <param name="Signature">The base64 signature as sent.</param>
internal sealed record AzureSharedKeyAuthorization(bool IsLite, string AccountName, string Signature)
{
    /// <summary>
    /// The <c>Authorization</c> scheme of Shared Key.
    /// </summary>
    public const string SharedKeyScheme = "SharedKey";

    /// <summary>
    /// The <c>Authorization</c> scheme of Shared Key Lite.
    /// </summary>
    public const string SharedKeyLiteScheme = "SharedKeyLite";

    /// <summary>
    /// The scheme as it is written in a login note.
    /// </summary>
    public string Scheme => IsLite ? SharedKeyLiteScheme : SharedKeyScheme;

    /// <summary>
    /// Reads <paramref name="credentials"/>, what followed the scheme: an account name and a
    /// signature, each non-empty and without spaces, joined by the first <c>:</c>.
    /// </summary>
    /// <param name="isLite">Whether the scheme was <c>SharedKeyLite</c>.</param>
    /// <param name="credentials">What followed the scheme and its space.</param>
    /// <returns>The authorization, or <see langword="null"/> when it does not read.</returns>
    public static AzureSharedKeyAuthorization? TryParse(bool isLite, string credentials)
    {
        var colon = credentials.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || colon == credentials.Length - 1 || credentials.Contains(' ', StringComparison.Ordinal))
        {
            return null;
        }

        return new AzureSharedKeyAuthorization(isLite, credentials[..colon], credentials[(colon + 1)..]);
    }
}
