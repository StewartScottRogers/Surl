using System.Globalization;

namespace Surl.MailStore;

/// <summary>
/// Puts a mail domain in the one form configured domains and recipients' domains are compared
/// in (ADR-0102, decision 1.1): one trailing dot dropped, converted to its ASCII form by
/// <see cref="IdnMapping.GetAscii(string)"/> with <see cref="IdnMapping.UseStd3AsciiRules"/>
/// on, then lower-cased.
/// </summary>
public static class MailDomainName
{
    private static readonly IdnMapping Std3IdnMapping = new() { UseStd3AsciiRules = true };

    /// <summary>
    /// Normalises <paramref name="domain"/>.
    /// </summary>
    /// <param name="domain">The domain as written or sent, in A-labels or U-labels.</param>
    /// <param name="asciiDomain">The normalised domain, or the empty string when the result is
    /// <see langword="false"/>.</param>
    /// <returns><see langword="false"/> when <paramref name="domain"/> is not a domain name:
    /// <see cref="IdnMapping.GetAscii(string)"/> refuses it, as it refuses an address literal
    /// such as <c>[127.0.0.1]</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="domain"/> is <see langword="null"/>.</exception>
    public static bool TryNormalise(string domain, out string asciiDomain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        asciiDomain = string.Empty;
        var withoutTrailingDot = domain.EndsWith('.') ? domain[..^1] : domain;
        try
        {
            asciiDomain = Std3IdnMapping.GetAscii(withoutTrailingDot).ToLowerInvariant();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
