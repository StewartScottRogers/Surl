using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// What <c>--gs-plaintext</c> does to the listen URLs (ADR-0110 decision 5): each <c>gs</c> one is
/// marked plain (<see cref="ListenUrl.IsImplicitTls"/> <see langword="false"/>), so the serving engine
/// answers it as plain HTTP/1.1, no certificate is needed for it;
/// without the option they are left as they are, TLS from the first byte.
/// </summary>
public static class GsPlaintextComposition
{
    private const string GsScheme = "gs";

    /// <summary>
    /// Marks every <c>gs</c> listen URL of <paramref name="commandLine"/> plain when
    /// <see cref="SurlCommandLine.GsPlaintext"/> is set.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The command line with its <c>gs</c> listen URLs marked, or itself without the option.</returns>
    public static SurlCommandLine MarkGsListenUrlsPlain(SurlCommandLine commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return commandLine.GsPlaintext
            ? commandLine with { ListenUrls = [.. commandLine.ListenUrls.Select(MarkPlainWhenGs)] }
            : commandLine;
    }

    private static ListenUrl MarkPlainWhenGs(ListenUrl listenUrl) =>
        listenUrl.Scheme == GsScheme
            ? listenUrl with { IsImplicitTls = false }
            : listenUrl;
}
