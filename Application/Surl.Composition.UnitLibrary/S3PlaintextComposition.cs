using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// What <c>--s3-plaintext</c> does to the listen URLs (ADR-0108 decision 5): each <c>s3</c> one is
/// marked plain (<see cref="ListenUrl.IsImplicitTls"/> <see langword="false"/>), so the serving engine
/// answers it as plain HTTP/1.1 and no certificate is needed for it; without the option they are
/// left as they are, TLS from the first byte.
/// </summary>
public static class S3PlaintextComposition
{
    private const string S3Scheme = "s3";

    /// <summary>
    /// Marks every <c>s3</c> listen URL of <paramref name="commandLine"/> plain when
    /// <see cref="SurlCommandLine.S3Plaintext"/> is set.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The command line with its <c>s3</c> listen URLs marked, or itself without the option.</returns>
    public static SurlCommandLine MarkS3ListenUrlsPlain(SurlCommandLine commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return commandLine.S3Plaintext
            ? commandLine with { ListenUrls = [.. commandLine.ListenUrls.Select(MarkPlainWhenS3)] }
            : commandLine;
    }

    private static ListenUrl MarkPlainWhenS3(ListenUrl listenUrl) =>
        listenUrl.Scheme == S3Scheme
            ? listenUrl with { IsImplicitTls = false }
            : listenUrl;
}
