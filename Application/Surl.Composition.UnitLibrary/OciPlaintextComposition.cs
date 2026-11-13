using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// What <c>--oci-plaintext</c> does to the listen URLs (ADR-0115 decision 6): each <c>oci</c> one is
/// marked plain (<see cref="ListenUrl.IsImplicitTls"/> <see langword="false"/>), so the serving engine
/// answers it as plain HTTP/1.1, no certificate is needed for it and its token realm is <c>http</c>;
/// without the option they are left as they are, TLS from the first byte.
/// </summary>
public static class OciPlaintextComposition
{
    private const string OciScheme = "oci";

    /// <summary>
    /// Marks every <c>oci</c> listen URL of <paramref name="commandLine"/> plain when
    /// <see cref="SurlCommandLine.OciPlaintext"/> is set.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The command line with its <c>oci</c> listen URLs marked, or itself without the option.</returns>
    public static SurlCommandLine MarkOciListenUrlsPlain(SurlCommandLine commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return commandLine.OciPlaintext
            ? commandLine with { ListenUrls = [.. commandLine.ListenUrls.Select(MarkPlainWhenOci)] }
            : commandLine;
    }

    private static ListenUrl MarkPlainWhenOci(ListenUrl listenUrl) =>
        listenUrl.Scheme == OciScheme
            ? listenUrl with { IsImplicitTls = false }
            : listenUrl;
}
