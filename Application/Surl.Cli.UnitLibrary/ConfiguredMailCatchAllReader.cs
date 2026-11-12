namespace Surl.Cli;

/// <summary>
/// Reads one <c>--mail-catch-all &lt;domain&gt;=&lt;address&gt;</c> argument (ADR-0101 decisions 3 to 5, ADR-0102
/// decision 1.3) into a <see cref="ConfiguredMailCatchAll"/>: the item by <see cref="Shape"/>, the domain one
/// <see cref="MailDomainNames.ReadDomain"/> accepts and the target a mail address. The record keeps what was
/// written. A domain given twice or outside every <c>--mail-domain</c> is <see cref="ConfiguredMailConflicts"/>'
/// to refuse; a target outside every domain or naming no mailbox, the mail store's (BL-994).
/// </summary>
internal static class ConfiguredMailCatchAllReader
{
    /// <summary>The <c>--mail-catch-all</c> item: a domain, <c>=</c> and one address, with no keys or flags.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<domain>=<address>",
        '=',
        ConfigurationItemValueKind.Name,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal),
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredMailCatchAll> Reading = new(Read, Shape.Reading.Type);

    /// <summary>The catch-all a refused argument leaves behind.</summary>
    public static ConfiguredMailCatchAll Empty { get; } = new(string.Empty, string.Empty);

    /// <summary>A catch-all domain an earlier <c>--mail-catch-all</c> already gave (ADR-0101 decision 5 row 14).</summary>
    /// <param name="domain">The domain as written.</param>
    /// <returns><c>catch-all domain &lt;domain&gt; is given twice</c>.</returns>
    public static string CatchAllDomainGivenTwice(string domain) => $"catch-all domain {domain} is given twice";

    /// <summary>Reads one <c>--mail-catch-all</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The catch-all, when accepted; <see cref="Empty"/> otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredMailCatchAll value)
    {
        value = Empty;
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item)
            ?? MailDomainNames.ReadDomain(item.Name, out _)
            ?? MailDomainNames.ReadAddress(item.Value!, out _);
        if (failure is null)
        {
            value = new(item.Name, item.Value!);
        }

        return failure;
    }
}
