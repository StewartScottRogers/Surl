namespace Surl.MailStore;

/// <summary>
/// A mail address whose domain is normalised by <see cref="MailDomainName.TryNormalise"/>: the
/// local part as written, <c>@</c>, the domain in ASCII lower case (ADR-0102, decision 1.2).
/// </summary>
/// <param name="LocalPart">The local part, as written.</param>
/// <param name="Domain">The normalised domain.</param>
internal readonly record struct MailDomainAddress(string LocalPart, string Domain)
{
    /// <summary>
    /// Reads a configured address, <c>local@domain</c>, split at its last <c>@</c>.
    /// </summary>
    /// <returns><see langword="false"/> when the address has no <c>@</c>, an empty local part,
    /// or a domain that is no domain name.</returns>
    public static bool TryParse(string address, out MailDomainAddress parsed)
    {
        parsed = default;
        var at = address.LastIndexOf('@');
        if (at < 1 || !MailDomainName.TryNormalise(address[(at + 1)..], out var domain))
        {
            return false;
        }

        parsed = new MailDomainAddress(address[..at], domain);
        return true;
    }

    /// <summary>
    /// The normalised address, <c>local@domain</c>.
    /// </summary>
    public override string ToString() => LocalPart + "@" + Domain;
}
