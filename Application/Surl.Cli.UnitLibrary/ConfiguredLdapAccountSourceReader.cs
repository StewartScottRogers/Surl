using System.Globalization;

namespace Surl.Cli;

/// <summary>
/// Reads one <c>--user-ldap</c> argument (ADR-0149 decisions 3 and 7, ADR-0101 decisions 3 to 5) into a
/// <see cref="ConfiguredLdapAccountSource"/>: the item by <see cref="Shape"/>, then the checks that need
/// no directory library - the scheme, the bind pair, the timeout, <c>plaintext</c> and the schemes. No
/// refusal holds the URL, a DN or a file name.
/// </summary>
internal static class ConfiguredLdapAccountSourceReader
{
    /// <summary>The key naming the DN <c>surl</c> binds as.</summary>
    public const string BindDnKey = "bind-dn";

    /// <summary>The key naming the file holding the bind password.</summary>
    public const string BindPasswordFileKey = "bind-password-file";

    /// <summary>The key naming the PEM certificates trusted beside the system's roots.</summary>
    public const string CaFileKey = "ca-file";

    /// <summary>The key bounding one login's directory check, in seconds.</summary>
    public const string TimeoutKey = "timeout";

    /// <summary>The key naming the schemes a directory account may log in on.</summary>
    public const string ProtocolsKey = "protocols";

    /// <summary>The flag that allows <c>ldap://</c> without StartTLS.</summary>
    public const string PlaintextFlag = "plaintext";

    /// <summary>The seconds one login's directory check may take when <c>timeout</c> is not given.</summary>
    public const int DefaultTimeoutSeconds = 5;

    /// <summary>A head that is not <c>ldap://</c> or <c>ldaps://</c> (ADR-0149 decision 7).</summary>
    public const string ExpectedAnLdapUrl = "expected an ldap:// or ldaps:// URL";

    /// <summary><c>bind-dn</c> without <c>bind-password-file</c>, or the other way round.</summary>
    public const string BindPairIsGivenTogether = "bind-dn and bind-password-file are given together";

    /// <summary>A <c>timeout</c> that is not a whole number from 1 to 60.</summary>
    public const string TimeoutIsOutOfRange = "timeout is not 1 to 60 seconds";

    /// <summary><c>plaintext</c> on an <c>ldaps://</c> URL.</summary>
    public const string PlaintextNeedsLdap = "plaintext cannot be used with ldaps://";

    /// <summary>A second <c>--user-ldap</c>: one directory is the account source.</summary>
    public const string GivenTwice = "is given twice";

    /// <summary>The <c>--user-ldap</c> item: the URL as the head, with no separator.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<ldap-url>[;bind-dn=<dn>][;bind-password-file=<file>][;ca-file=<file>][;timeout=<seconds>][;protocols=<scheme>,...][;plaintext]",
        null,
        ConfigurationItemValueKind.None,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal)
        {
            [BindDnKey] = ConfigurationItemAttributeKind.Value,
            [BindPasswordFileKey] = ConfigurationItemAttributeKind.Value,
            [CaFileKey] = ConfigurationItemAttributeKind.Value,
            [TimeoutKey] = ConfigurationItemAttributeKind.Value,
            [ProtocolsKey] = ConfigurationItemAttributeKind.List,
            [PlaintextFlag] = ConfigurationItemAttributeKind.Flag,
        },
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredLdapAccountSource> Reading = new(Read, Shape.Reading.Type);

    private static readonly Func<string, bool> AnySurlScheme = ConfiguredMailboxReader.IsAnySurlScheme;

    /// <summary>The source a refused argument leaves behind.</summary>
    public static ConfiguredLdapAccountSource Empty { get; } = new(string.Empty, null, null, null, DefaultTimeoutSeconds, null, IsPlaintext: false);

    /// <summary>Reads one <c>--user-ldap</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The source, when accepted; <see cref="Empty"/> otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredLdapAccountSource value)
    {
        value = Empty;
        if (ConfigurationItemReader.Read(Shape, argument, out var item) is string itemFailure)
        {
            return itemFailure;
        }

        var schemes = new List<string>();
        var timeoutSeconds = DefaultTimeoutSeconds;
        var failure = CheckScheme(item) ?? CheckBindPair(item) ?? ReadTimeout(item, ref timeoutSeconds) ?? CheckPlaintext(item)
            ?? ConfiguredMailboxReader.CheckProtocols(item.Lists.GetValueOrDefault(ProtocolsKey) ?? [], AnySurlScheme, schemes);
        if (failure is null)
        {
            value = new(
                item.Name,
                item.Values.GetValueOrDefault(BindDnKey),
                item.Values.GetValueOrDefault(BindPasswordFileKey),
                item.Values.GetValueOrDefault(CaFileKey),
                timeoutSeconds,
                item.Lists.ContainsKey(ProtocolsKey) ? schemes : null,
                item.Flags.Contains(PlaintextFlag));
        }

        return failure;
    }

    private static bool IsLdaps(ConfigurationItem item) => item.Name.StartsWith("ldaps://", StringComparison.OrdinalIgnoreCase);

    private static string? CheckScheme(ConfigurationItem item) =>
        IsLdaps(item) || item.Name.StartsWith("ldap://", StringComparison.OrdinalIgnoreCase) ? null : ExpectedAnLdapUrl;

    private static string? CheckBindPair(ConfigurationItem item) =>
        item.Values.ContainsKey(BindDnKey) != item.Values.ContainsKey(BindPasswordFileKey) ? BindPairIsGivenTogether : null;

    private static string? CheckPlaintext(ConfigurationItem item) =>
        IsLdaps(item) && item.Flags.Contains(PlaintextFlag) ? PlaintextNeedsLdap : null;

    private static string? ReadTimeout(ConfigurationItem item, ref int timeoutSeconds)
    {
        if (!item.Values.TryGetValue(TimeoutKey, out var timeout))
        {
            return null;
        }

        var isWholeNumber = int.TryParse(timeout, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds);
        timeoutSeconds = seconds;
        return isWholeNumber && seconds is >= 1 and <= 60 ? null : TimeoutIsOutOfRange;
    }
}
