using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// What <c>--az-plaintext</c> does to the listen URLs (ADR-0109 decision 6): each <c>az</c> one is
/// marked plain (<see cref="ListenUrl.IsImplicitTls"/> <see langword="false"/>), so the serving engine
/// answers it as plain HTTP/1.1, no certificate is needed for it;
/// without the option they are left as they are, TLS from the first byte.
/// </summary>
public static class AzPlaintextComposition
{
    private const string AzScheme = "az";

    /// <summary>
    /// Marks every <c>az</c> listen URL of <paramref name="commandLine"/> plain when
    /// <see cref="SurlCommandLine.AzPlaintext"/> is set.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The command line with its <c>az</c> listen URLs marked, or itself without the option.</returns>
    public static SurlCommandLine MarkAzListenUrlsPlain(SurlCommandLine commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return commandLine.AzPlaintext
            ? commandLine with { ListenUrls = [.. commandLine.ListenUrls.Select(MarkPlainWhenAz)] }
            : commandLine;
    }

    private static ListenUrl MarkPlainWhenAz(ListenUrl listenUrl) =>
        listenUrl.Scheme == AzScheme
            ? listenUrl with { IsImplicitTls = false }
            : listenUrl;
}
