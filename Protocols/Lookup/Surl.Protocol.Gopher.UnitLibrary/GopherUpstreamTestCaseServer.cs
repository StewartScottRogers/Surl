using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Gopher;

/// <summary>
/// The Gopher server half of an upstream curl test case (ADR-0080 decision 2): Surl's own
/// selector reader, with its line limit and head timeout, reads the client's selector, and the
/// case's <see cref="GopherUpstreamTestCaseReplies"/> supplies the content sent back for it.
/// </summary>
/// <remarks>
/// <para>
/// A selector line read is answered with the case's <c>&lt;data&gt;</c>, then writes are
/// completed and the connection closed; a client that sends no whole line gets no bytes, as
/// <see cref="GopherProtocolServer"/> answers it. Every byte read is recorded.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c>: <c>Surl.Conformance.UnitTests</c> composes it on
/// the loopback listener for upstream's test cases.
/// </para>
/// </remarks>
public sealed class GopherUpstreamTestCaseServer : IConnectionProtocolServer
{
    private readonly GopherUpstreamTestCaseReplies replies;

    /// <summary>
    /// Creates the server half of one case.
    /// </summary>
    /// <param name="replies">The case's reply and record of received bytes.</param>
    public GopherUpstreamTestCaseServer(GopherUpstreamTestCaseReplies replies)
    {
        ArgumentNullException.ThrowIfNull(replies);

        this.replies = replies;
    }

    /// <summary>
    /// The schemes answered: <c>gopher</c>, and <c>gophers</c>, whose TLS handshake the engine
    /// completes before <see cref="ServeAsync"/>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["gopher", "gophers"]);

    /// <summary>
    /// Reads the client's selector and answers it with the case's <c>&lt;data&gt;</c>, then
    /// closes the connection.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var (outcome, _) = await GopherProtocolServer.ReadSelectorLineAsync(new ReceivedBytesRecordingConnection(connection, replies), context);
        if (outcome == GopherSelectorReadOutcome.LineRead)
        {
            context.Log.Note($"Selector answered with the case's {replies.Data.Length} bytes of <data>.");
            await connection.WriteAsync(replies.Data, context.CancellationToken);
        }

        await connection.CompleteWritesAsync(context.CancellationToken);
    }
}
