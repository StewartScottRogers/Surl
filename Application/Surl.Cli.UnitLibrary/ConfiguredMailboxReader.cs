namespace Surl.Cli;

/// <summary>
/// Reads one <c>--mailbox &lt;address&gt;:&lt;password&gt;[;protocols=&lt;scheme&gt;,...]</c> argument (ADR-0101
/// decisions 3 to 5, ADR-0102 decisions 1.2 and 1.4) into a <see cref="ConfiguredMailbox"/>: the item
/// by <see cref="Shape"/>, split at the first <c>:</c>, then the checks <see cref="Build"/> shares with
/// <see cref="MailboxFileReader"/>. No refusal holds the password.
/// </summary>
internal static class ConfiguredMailboxReader
{
    /// <summary>The <c>--mailbox</c> item: an address, <c>:</c> and a password, with the list key <c>protocols</c>.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<address>:<password>[;protocols=<scheme>,...]",
        ':',
        ConfigurationItemValueKind.Text,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal)
        {
            [ProtocolsKey] = ConfigurationItemAttributeKind.List,
        },
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredMailbox> Reading = new(Read, Shape.Reading.Type);

    /// <summary>An address holding <c>:</c>, possible only percent-encoded (ADR-0102 decision 1.2).</summary>
    public const string AddressHoldsColon = "the address holds a colon";

    /// <summary>The key, and the <c>--mailbox-file</c> column, naming the schemes a mailbox logs in on.</summary>
    public const string ProtocolsKey = "protocols";

    /// <summary>A mailbox address given twice, compared case-insensitively (ADR-0101 decision 5 row 14).</summary>
    /// <param name="address">The address as written.</param>
    /// <returns><c>mailbox &lt;address&gt; is given twice</c>.</returns>
    public static string MailboxGivenTwice(string address) => $"mailbox {address} is given twice";

    /// <summary>A mailbox whose address is also a <c>--user</c> name (ADR-0101 decision 5 row 15).</summary>
    /// <param name="address">The address as written.</param>
    /// <returns><c>mailbox &lt;address&gt; is also a --user account</c>.</returns>
    public static string AlsoAUserAccount(string address) => $"mailbox {address} is also a --user account";

    /// <summary>A mailbox whose domain no <c>--mail-domain</c> names (ADR-0101 decision 5 row 15).</summary>
    /// <param name="address">The address as written.</param>
    /// <returns><c>mailbox &lt;address&gt; is not in any --mail-domain</c>.</returns>
    public static string NotInAnyMailDomain(string address) => $"mailbox {address} is not in any --mail-domain";

    /// <summary>A mailbox given with no <c>--mail-domain</c> at all (ADR-0101 decision 5 row 15).</summary>
    /// <param name="address">The address as written.</param>
    /// <returns><c>mailbox &lt;address&gt; needs --mail-domain</c>.</returns>
    public static string NeedsMailDomain(string address) => $"mailbox {address} needs --mail-domain";

    /// <summary>
    /// The conflict of an accepted mailbox with the rest of the configuration (ADR-0101 decision 5
    /// row 15): its domain in none of <paramref name="mailDomains"/>, or none given, or its normalised
    /// address one of <paramref name="userNames"/>.
    /// </summary>
    /// <param name="written">The address as written, for the refusal.</param>
    /// <param name="mailbox">The mailbox <see cref="Build"/> accepted.</param>
    /// <param name="mailDomains">The mail domains, each in <see cref="MailDomainNames"/>' form.</param>
    /// <param name="userNames">The <c>--user</c> names.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? FindConflict(string written, ConfiguredMailbox mailbox, IReadOnlySet<string> mailDomains, IReadOnlySet<string> userNames)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        ArgumentNullException.ThrowIfNull(mailDomains);
        ArgumentNullException.ThrowIfNull(userNames);
        if (mailDomains.Count == 0)
        {
            return NeedsMailDomain(written);
        }

        if (!mailDomains.Contains(mailbox.Address[(mailbox.Address.LastIndexOf('@') + 1)..]))
        {
            return NotInAnyMailDomain(written);
        }

        return userNames.Contains(mailbox.Address) ? AlsoAUserAccount(written) : null;
    }

    /// <summary>A scheme no server surl has speaks (ADR-0101 decision 4, row 10).</summary>
    /// <param name="scheme">The scheme, lower-cased.</param>
    /// <returns><c>unknown scheme &lt;scheme&gt;</c>.</returns>
    public static string UnknownScheme(string scheme) => $"unknown scheme {scheme}";

    /// <summary>Reads one <c>--mailbox</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The mailbox, when accepted; an empty one otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredMailbox value)
    {
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item);
        value = Empty;
        return failure ?? Build(item.Name, item.Value!, item.Lists.GetValueOrDefault(ProtocolsKey), IsAnySurlScheme, out value);
    }

    /// <summary>
    /// Whether <paramref name="scheme"/> is upstream curl's or one any extended server claims: what
    /// the command line can check before the registered servers are known. <c>Surl.Composition</c>
    /// checks against the registered ones (<see cref="IsServedScheme"/>).
    /// </summary>
    /// <param name="scheme">The scheme, lower-cased.</param>
    /// <returns><see langword="true"/> when some surl server speaks it.</returns>
    public static bool IsAnySurlScheme(string scheme) =>
        SchemeDefaultPorts.TryGetDefaultPort(scheme, out _) || ExtendedSchemeDefaultPorts.TryGetDefaultPort(scheme, out _);

    /// <summary>
    /// Whether <paramref name="scheme"/> is upstream curl's or one of <paramref name="extendedSchemes"/>,
    /// so an extended scheme under <c>SURL_EXTENSIONS=0</c> is refused (ADR-0101 decision 4).
    /// </summary>
    /// <param name="scheme">The scheme, lower-cased.</param>
    /// <param name="extendedSchemes">The schemes the registered extended servers claim.</param>
    /// <returns><see langword="true"/> when a server this start runs speaks it.</returns>
    public static bool IsServedScheme(string scheme, IReadOnlyCollection<string> extendedSchemes) =>
        SchemeDefaultPorts.TryGetDefaultPort(scheme, out _) || extendedSchemes.Contains(scheme, StringComparer.Ordinal);

    /// <summary>The mailbox a refused argument or record leaves behind.</summary>
    public static ConfiguredMailbox Empty { get; } = new(string.Empty, string.Empty, null);

    /// <summary>
    /// Checks one mailbox's decoded fields and builds it: the address non-empty, free of control
    /// characters and <c>:</c>, and normalised by <see cref="MailDomainNames.ReadAddress"/>; the
    /// password non-empty and free of U+0000; each scheme non-empty, free of control characters,
    /// lower-cased, served by surl and given once.
    /// </summary>
    /// <param name="address">The address, decoded.</param>
    /// <param name="password">The password, decoded.</param>
    /// <param name="protocols">The schemes as given, or <see langword="null"/> when <c>protocols</c> was not given.</param>
    /// <param name="isServedScheme">Whether a lower-case scheme is one a server surl runs speaks.</param>
    /// <param name="mailbox">The mailbox, when accepted; <see cref="Empty"/> otherwise.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? Build(
        string address, string password, IReadOnlyList<string>? protocols, Func<string, bool> isServedScheme, out ConfiguredMailbox mailbox)
    {
        mailbox = Empty;
        var schemes = new List<string>();
        var failure = CheckAddress(address, out var normalised)
            ?? CheckPassword(password)
            ?? CheckProtocols(protocols ?? [], isServedScheme, schemes);
        if (failure is null)
        {
            mailbox = new(normalised, password, protocols is null ? null : schemes);
        }

        return failure;
    }

    private static string? CheckAddress(string address, out string normalised)
    {
        normalised = string.Empty;
        if (CheckName(address) is string failure)
        {
            return failure;
        }

        return address.Contains(':', StringComparison.Ordinal)
            ? AddressHoldsColon
            : MailDomainNames.ReadAddress(address, out normalised);
    }

    private static string? CheckPassword(string password)
    {
        if (password.Length == 0)
        {
            return ConfigurationItemReader.EmptyField;
        }

        return password.Contains('\0', StringComparison.Ordinal) ? ConfigurationItemReader.ControlCharacter : null;
    }

    private static string? CheckProtocols(IReadOnlyList<string> protocols, Func<string, bool> isServedScheme, List<string> schemes)
    {
        foreach (var given in protocols)
        {
            var failure = CheckName(given) ?? CheckScheme(given.ToLowerInvariant(), isServedScheme, schemes);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private static string? CheckScheme(string scheme, Func<string, bool> isServedScheme, List<string> schemes)
    {
        if (!isServedScheme(scheme))
        {
            return UnknownScheme(scheme);
        }

        if (schemes.Contains(scheme, StringComparer.Ordinal))
        {
            return ConfiguredGroupReader.MemberGivenTwice(scheme);
        }

        schemes.Add(scheme);
        return null;
    }

    /// <summary>A name or list member: non-empty, no control character (ADR-0101 decision 3).</summary>
    private static string? CheckName(string name)
    {
        if (name.Length == 0)
        {
            return ConfigurationItemReader.EmptyField;
        }

        return name.Any(character => character < ' ' || character == '\u007F') ? ConfigurationItemReader.ControlCharacter : null;
    }
}
