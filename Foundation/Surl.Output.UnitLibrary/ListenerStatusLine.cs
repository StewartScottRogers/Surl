using Surl.Protocol.Abstractions;

namespace Surl.Output;

/// <summary>
/// Writes the listener status line, <c>Listening on &lt;scheme&gt;://&lt;host&gt;:&lt;bound port&gt;/</c>,
/// one per bound listen URL (ADR-0007, section 7).
/// </summary>
/// <remarks>
/// Every line ends with <see cref="Environment.NewLine"/> (ADR-0007, section 5). Each line
/// is written with one call under a lock, so status lines from several threads never
/// interleave.
/// </remarks>
/// <param name="writer">Where the status lines go; <c>surl</c> passes stdout.</param>
public sealed class ListenerStatusLine(TextWriter writer)
{
    private readonly TextWriter writer = writer ?? throw new ArgumentNullException(nameof(writer));
    private readonly Lock writeLock = new();

    /// <summary>
    /// Formats a bound listen URL as the status line and the engine's notes write it:
    /// <c>&lt;scheme&gt;://&lt;host&gt;:&lt;bound port&gt;/</c>, the host bracketed when it
    /// is an IPv6 literal and a zone written as <c>%25&lt;zone&gt;</c>.
    /// </summary>
    /// <param name="listenUrl">
    /// The listen URL, with <see cref="ListenUrl.BoundPort"/> set; or one on a Unix domain socket,
    /// which binds no port, so its text carries the port asked for (ADR-0094, decision 6).
    /// </param>
    /// <returns>The listen URL's text, for example <c>http://[::1]:49731/</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="listenUrl"/> is on TCP and has no bound port yet.</exception>
    public static string FormatBoundListenUrl(ListenUrl listenUrl)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);
        var boundPort = listenUrl.UnixSocket is null ? listenUrl.BoundPort : listenUrl.Port;
        if (boundPort is null)
        {
            throw new ArgumentException("The listen URL has not been bound yet.", nameof(listenUrl));
        }

        var host = listenUrl.Host.Contains(':', StringComparison.Ordinal)
            ? "[" + listenUrl.Host.Replace("%", "%25", StringComparison.Ordinal) + "]"
            : listenUrl.Host;

        return $"{listenUrl.Scheme}://{host}:{boundPort}/";
    }

    /// <summary>
    /// Formats the status line for a bound listen URL, without its line ending.
    /// </summary>
    /// <param name="listenUrl">The listen URL, with <see cref="ListenUrl.BoundPort"/> set, or one on a Unix domain socket.</param>
    /// <returns>
    /// <c>Listening on </c> and <see cref="FormatBoundListenUrl"/>'s text, and for a listen URL on a
    /// Unix domain socket <c> over </c> and the socket in curl's own words
    /// (<c>Listening on http://localhost:80/ over unix:///tmp/surl.sock</c>, ADR-0094, decision 6).
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="listenUrl"/> is on TCP and has no bound port yet.</exception>
    public static string Format(ListenUrl listenUrl)
    {
        var line = "Listening on " + FormatBoundListenUrl(listenUrl);
        return listenUrl.UnixSocket is { } unixSocket ? line + " over " + unixSocket.UnixUrl : line;
    }

    /// <summary>
    /// Writes the status line for a bound listen URL, ending with <see cref="Environment.NewLine"/>.
    /// </summary>
    /// <param name="listenUrl">The listen URL, with <see cref="ListenUrl.BoundPort"/> set.</param>
    /// <exception cref="ArgumentException"><paramref name="listenUrl"/> has no bound port yet.</exception>
    public void Write(ListenUrl listenUrl)
    {
        var line = Format(listenUrl) + Environment.NewLine;
        lock (writeLock)
        {
            writer.Write(line);
        }
    }
}
