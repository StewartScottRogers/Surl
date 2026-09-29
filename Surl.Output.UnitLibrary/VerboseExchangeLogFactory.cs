using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Output;

/// <summary>
/// The <c>-v</c> verbose exchange log (ADR-0007, section 8): hands out one
/// <see cref="IExchangeLog"/> per exchange that writes <c>#&lt;exchange id&gt; &lt;marker&gt; &lt;text&gt;</c>
/// lines to one shared writer, or, with verbose off, logs that write nothing.
/// </summary>
/// <remarks>
/// A <see cref="LevelledExchangeLogFactory"/> at <see cref="LogLevel.Verbose"/> or
/// <see cref="LogLevel.None"/>, without timestamps, for <c>surl</c> until it passes the
/// parsed log level (BL-107).
/// </remarks>
public sealed class VerboseExchangeLogFactory : IExchangeLogFactory
{
    private readonly LevelledExchangeLogFactory levelledFactory;

    /// <summary>
    /// Creates the factory.
    /// </summary>
    /// <param name="writer">Where the log goes; <c>surl</c> passes stderr.</param>
    /// <param name="verbose">Whether <c>-v</c> was given. When <see langword="false"/>, the logs write nothing.</param>
    public VerboseExchangeLogFactory(TextWriter writer, bool verbose) =>
        levelledFactory = new LevelledExchangeLogFactory(
            writer,
            verbose ? LogLevel.Verbose : LogLevel.None,
            stampTimes: false,
            TimeProvider.System);

    /// <inheritdoc/>
    public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint) =>
        levelledFactory.Create(exchangeId, remoteEndPoint);

    /// <inheritdoc/>
    public void NoteOutsideExchange(string text) => levelledFactory.NoteOutsideExchange(text);
}
