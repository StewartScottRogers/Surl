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
    /// <param name="listenUrl">The listen URL, with <see cref="ListenUrl.BoundPort"/> set.</param>
    /// <returns>The listen URL's text, for example <c>http://[::1]:49731/</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="listenUrl"/> has no bound port yet.</exception>
    public static string FormatBoundListenUrl(ListenUrl listenUrl)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);
        if (listenUrl.BoundPort is not int boundPort)
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
    /// <param name="listenUrl">The listen URL, with <see cref="ListenUrl.BoundPort"/> set.</param>
    /// <returns><c>Listening on </c> and <see cref="FormatBoundListenUrl"/>'s text.</returns>
    /// <exception cref="ArgumentException"><paramref name="listenUrl"/> has no bound port yet.</exception>
    public static string Format(ListenUrl listenUrl) => "Listening on " + FormatBoundListenUrl(listenUrl);

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
