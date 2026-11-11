using System.Collections.Frozen;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// Checks <c>--unix-socket</c> and <c>--abstract-unix-socket</c> once the whole command line is
/// read (ADR-0094, decisions 1 and 2), and moves the socket onto the one listen URL they serve.
/// </summary>
internal static class UnixSocketCombination
{
    /// <summary>
    /// The schemes whose servers listen on UDP, which a Unix stream socket cannot carry: <c>tftp</c>,
    /// and the extended <c>coap</c>, <c>coaps</c> and <c>dns</c> (whose server answers on UDP
    /// beside TCP) (ADR-0094, decision 2).
    /// </summary>
    public static readonly FrozenSet<string> DatagramSchemes =
        FrozenSet.Create(StringComparer.Ordinal, "tftp", "coap", "coaps", "dns");


    /// <summary>
    /// Refuses a Unix socket given with more than one listen URL, with a scheme that listens on UDP,
    /// or with <c>--http3</c>, in that order, each
    /// <see cref="SurlExitCode.FailedInit"/> followed by the try-help line; null otherwise, and
    /// whenever no Unix socket is given.
    /// </summary>
    /// <param name="commandLine">The command line read so far.</param>
    /// <param name="listenUrls">The listen URLs given, at least one.</param>
    /// <returns>
    /// The refusal, naming the option that set the socket last (<c>--abstract-unix-socket</c> for an
    /// abstract name, else <c>--unix-socket</c>), or <see langword="null"/>.
    /// </returns>
    public static CommandLineFailure? Refuse(SurlCommandLine commandLine, IReadOnlyList<ListenUrl> listenUrls)
    {
        if (commandLine.UnixSocket is not { } unixSocket)
        {
            return null;
        }

        var optionName = unixSocket.IsAbstract ? "--abstract-unix-socket" : "--unix-socket";
        return listenUrls.Count > 1
            ? Refused(optionName, $"serves one listen URL, and {listenUrls.Count} were given")
            : RefuseScheme(commandLine, listenUrls[0].Scheme, optionName);
    }

    // Refuses the one listen URL's scheme, or --http3, where a Unix stream socket cannot serve it.
    private static CommandLineFailure? RefuseScheme(SurlCommandLine commandLine, string scheme, string optionName)
    {
        if (DatagramSchemes.Contains(scheme))
        {
            return Refused(optionName, $"{scheme} listens on UDP");
        }

        if (commandLine.Http3)
        {
            return Refused(optionName, "--http3 listens on UDP");
        }

        return null;
    }

    /// <summary>
    /// The listen URLs to serve: <paramref name="listenUrls"/> as given, or, with a Unix socket,
    /// the one listen URL with its <see cref="ListenUrl.UnixSocket"/> set.
    /// </summary>
    /// <param name="commandLine">The command line read, past <see cref="Refuse"/>.</param>
    /// <param name="listenUrls">The listen URLs given.</param>
    /// <returns>The listen URLs to serve.</returns>
    public static IReadOnlyList<ListenUrl> Apply(SurlCommandLine commandLine, IReadOnlyList<ListenUrl> listenUrls) =>
        commandLine.UnixSocket is { } unixSocket ? [listenUrls[0] with { UnixSocket = unixSocket }] : [.. listenUrls];

    private static CommandLineFailure Refused(string optionName, string reason) =>
        new(SurlExitCode.FailedInit, $"option {optionName}: {reason}", FollowedByTryHelpLine: true);
}
