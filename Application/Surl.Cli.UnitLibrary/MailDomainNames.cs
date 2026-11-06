using System.Globalization;

namespace Surl.Cli;

/// <summary>
/// Reads mail domain names and the domain of a mail address into the one form they are compared
/// in (ADR-0102 decision 1.1): one trailing dot dropped, converted to ASCII by
/// <see cref="IdnMapping.GetAscii(string)"/> with <see cref="IdnMapping.UseStd3AsciiRules"/> on, then
/// lower-cased. A name <see cref="IdnMapping"/> refuses is refused with <see cref="NotADomainName"/>.
/// </summary>
public static class MailDomainNames
{
    /// <summary>A name that is not a domain name (ADR-0102 decision 1.1, ADR-0101 decision 5 row 10).</summary>
    public const string NotADomainName = "not a domain name";

    /// <summary>An address with no <c>@</c>, or nothing before or after its last <c>@</c>.</summary>
    public const string NotAMailAddress = "not a mail address";

    private static readonly IdnMapping Mapping = new() { UseStd3AsciiRules = true };

    /// <summary><see cref="ReadMailDomain"/> and its argument type (ADR-0046 decision 5), for <c>--mail-domain</c>.</summary>
    internal static readonly OptionArgumentReading<string> Reading =
        new(ReadMailDomain, new("domain", "a domain name in A-labels or U-labels, one trailing dot allowed; subdomains are not matched"));

    /// <summary>A mail domain an earlier <c>--mail-domain</c> already gave, compared as <see cref="ReadDomain"/> reads it (ADR-0101 decision 5 row 14).</summary>
    /// <param name="domain">The domain as written.</param>
    /// <returns><c>mail domain &lt;domain&gt; is given twice</c>.</returns>
    public static string MailDomainGivenTwice(string domain) => $"mail domain {domain} is given twice";

    /// <summary>Reads one <c>--mail-domain</c> argument: a name <see cref="ReadDomain"/> accepts, kept as written.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The domain as written.</param>
    /// <returns><c>blank argument where content is expected</c>, <see cref="NotADomainName"/>, or <see langword="null"/>.</returns>
    public static string? ReadMailDomain(string argument, out string value)
    {
        ArgumentNullException.ThrowIfNull(argument);
        value = argument;
        return argument.Length == 0 ? OptionArgumentReader.Blank : ReadDomain(argument, out _);
    }

    /// <summary>Reads one domain name.</summary>
    /// <param name="name">The name as given, e.g. <c>Bücher.Example.</c>.</param>
    /// <param name="domain">The ASCII lower-case form, e.g. <c>xn--bcher-kva.example</c>; empty when refused.</param>
    /// <returns><see cref="NotADomainName"/>, or <see langword="null"/> when accepted.</returns>
    public static string? ReadDomain(string name, out string domain)
    {
        ArgumentNullException.ThrowIfNull(name);
        domain = string.Empty;
        var withoutDot = name.EndsWith('.') ? name[..^1] : name;
        string ascii;
        try
        {
            ascii = Mapping.GetAscii(withoutDot);
        }
        catch (ArgumentException)
        {
            return NotADomainName;
        }

        if (ascii.EndsWith('.'))
        {
            return NotADomainName;
        }

        domain = ascii.ToLowerInvariant();
        return null;
    }

    /// <summary>
    /// Reads one mail address into its normalised form (ADR-0102 decision 1.2): the local part as
    /// written, <c>@</c>, and the domain after the last <c>@</c> as <see cref="ReadDomain"/> reads it.
    /// </summary>
    /// <param name="address">The address as given, e.g. <c>Ann@Example.COM</c>.</param>
    /// <param name="normalised">The normalised address, e.g. <c>Ann@example.com</c>; empty when refused.</param>
    /// <returns><see cref="NotAMailAddress"/> or <see cref="NotADomainName"/>, or <see langword="null"/>.</returns>
    public static string? ReadAddress(string address, out string normalised)
    {
        ArgumentNullException.ThrowIfNull(address);
        normalised = string.Empty;
        var at = address.LastIndexOf('@');
        if (at <= 0 || at == address.Length - 1)
        {
            return NotAMailAddress;
        }

        var failure = ReadDomain(address[(at + 1)..], out var domain);
        if (failure is null)
        {
            normalised = address[..(at + 1)] + domain;
        }

        return failure;
    }
}
