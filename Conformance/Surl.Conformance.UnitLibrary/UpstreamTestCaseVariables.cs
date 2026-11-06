namespace Surl.Conformance;

/// <summary>
/// Substitutes a test case's <c>%NAME</c> variables, in the order upstream's runtests does.
/// </summary>
/// <remarks>
/// Upstream's <c>subvariables</c> (<c>tests/servers.pm</c> at tag <c>curl-8_21_0</c>) runs one
/// <c>s/%NAME/value/g</c> per variable it knows, in a fixed order, and nothing else: a
/// <c>%WORD</c> that names no variable it knows matches none of those substitutions and so is
/// left exactly as written. (<c>tests/testutil.pm</c> at the tag holds the other preprocessing
/// steps - <c>subchars</c>, <c>subbase64</c>, <c>subnewlines</c> - and no variable handling of
/// its own, so nothing there replaces an unknown <c>%WORD</c> either.) The FILEFORMAT.md
/// "Variables" list is a subset of these names.
/// Here a variable the caller gives no value for is left as written too.
/// </remarks>
public static class UpstreamTestCaseVariables
{
    /// <summary>
    /// Every variable name upstream's <c>subvariables</c> substitutes, without the <c>%</c>, in
    /// the order it substitutes them.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "DICTPORT", "DNSPORT",
        "FTPPORT", "FTP6PORT", "FTPSPORT",
        "GOPHERPORT", "GOPHER6PORT", "GOPHERSPORT",
        "HTTPPORT", "HTTP6PORT", "HTTPSPORT", "HTTPS-MTLSPORT",
        "HTTPSPROXYPORT", "HTTPTLSPORT", "HTTPTLS6PORT",
        "HTTP2PORT", "HTTP2TLSPORT",
        "HTTP3PORT",
        "IMAPPORT", "IMAP6PORT", "IMAPSPORT",
        "MQTTPORT", "MQTTSPORT",
        "NOLISTENPORT",
        "POP3PORT", "POP36PORT", "POP3SPORT",
        "RTSPPORT", "RTSP6PORT",
        "SMBPORT", "SMBSPORT",
        "SMTPPORT", "SMTP6PORT", "SMTPSPORT",
        "SOCKSPORT",
        "SSHPORT",
        "TELNETPORT",
        "TFTPPORT", "TFTP6PORT",
        "PROXYPORT",
        "HTTPUNIXPATH", "SOCKSUNIXPATH",
        "CLIENT6IP-NB", "CLIENT6IP", "CLIENTIP",
        "HOST6IP", "HOSTIP",
        "PERL", "CURL", "LOGDIR", "PWD", "VERSION", "VERNUM", "DATE", "TESTNUMBER", "RESOLVE",
        "FILE_PWD", "SCP_PWD", "SFTP_PWD", "SRCDIR", "CERTDIR", "USER", "DEV_NULL", "LIBTESTS",
        "SSHSRVMD5", "SSHSRVSHA256", "SSHKEYALGO",
        "FTPTIME2",
        "H2CVER",
    ];

    private static readonly HashSet<string> KnownNames = new(Names, StringComparer.Ordinal);

    /// <summary>
    /// Checks that every name in <paramref name="variables"/> is one upstream substitutes.
    /// </summary>
    /// <param name="variables">The values by name.</param>
    /// <exception cref="ArgumentException">A name is not in <see cref="Names"/>.</exception>
    public static void RequireKnownNames(IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var unknown = variables.Keys.Where(name => !KnownNames.Contains(name)).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException(
                $"Upstream's runtests substitutes no variable named {string.Join(", ", unknown)}.",
                nameof(variables));
        }
    }

    /// <summary>
    /// Replaces each <c>%NAME</c> in <paramref name="text"/> that <paramref name="variables"/>
    /// gives a value for, name by name in <see cref="Names"/> order.
    /// </summary>
    /// <param name="text">The text, one character per byte.</param>
    /// <param name="variables">The values by name.</param>
    /// <returns>The text with the given variables substituted.</returns>
    public static string Substitute(string text, IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(variables);

        foreach (var name in Names)
        {
            if (variables.TryGetValue(name, out var value))
            {
                text = text.Replace("%" + name, value, StringComparison.Ordinal);
            }
        }

        return text;
    }
}
