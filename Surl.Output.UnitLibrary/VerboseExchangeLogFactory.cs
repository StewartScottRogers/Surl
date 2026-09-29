using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Output;

/// <summary>
/// The <c>-v</c> verbose exchange log (ADR-0007, section 8): hands out one
/// <see cref="IExchangeLog"/> per exchange that writes <c>#&lt;exchange id&gt; &lt;marker&gt; &lt;text&gt;</c>
/// lines to one shared writer, or, with verbose off, logs that write nothing.
/// </summary>
/// <remarks>
/// Every line ends with <see cref="Environment.NewLine"/> (ADR-0007, section 5). The lines
/// of one event are written with one call under a lock the factory's logs share, so lines
/// from concurrent exchanges never interleave.
/// </remarks>
public sealed class VerboseExchangeLogFactory : IExchangeLogFactory
{
    private readonly TextWriter writer;
    private readonly bool verbose;
    private readonly Lock writeLock = new();

    /// <summary>
    /// Creates the factory.
    /// </summary>
    /// <param name="writer">Where the log goes; <c>surl</c> passes stderr.</param>
    /// <param name="verbose">Whether <c>-v</c> was given. When <see langword="false"/>, the logs write nothing.</param>
    public VerboseExchangeLogFactory(TextWriter writer, bool verbose)
    {
        ArgumentNullException.ThrowIfNull(writer);

        this.writer = writer;
        this.verbose = verbose;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The remote endpoint is not written here: the engine's <c>Connection from</c> note
    /// names it (ADR-0007, section 8).
    /// </remarks>
    public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint)
    {
        ArgumentNullException.ThrowIfNull(remoteEndPoint);

        return verbose ? new VerboseExchangeLog(exchangeId, writer, writeLock) : SilentExchangeLog.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// With verbose on, writes <c>#- * &lt;text&gt;</c>, escaped like any note: <c>-</c>
    /// stands where the exchange id goes, since exchange ids start at 1 (ADR-0028). With
    /// verbose off, writes nothing.
    /// </remarks>
    public void NoteOutsideExchange(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (verbose)
        {
            VerboseExchangeLog.OutsideAnyExchange(writer, writeLock).Note(text);
        }
    }
}
