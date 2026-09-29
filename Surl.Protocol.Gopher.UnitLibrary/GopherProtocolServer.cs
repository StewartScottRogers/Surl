using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Gopher;

/// <summary>
/// The Gopher server (RFC 1436): answers the one selector a client sends with a file from the
/// content store, a menu of a directory, or an error menu, then closes the connection, whose
/// close marks the end of the reply. ADR-0012 records the answers below.
/// </summary>
/// <remarks>
/// <para>
/// The client's line ends at LF, with or without a CR before it. Anything after the first TAB
/// (a type 7 search string, or a Gopher+ <c>+</c>) is ignored, and the selector before it is
/// answered. The selector is read as a percent-encoded path: bytes outside printable ASCII are
/// percent-encoded, a <c>/</c> is put in front when it does not start with one (the empty
/// selector is the root), and the content store maps it, so a <c>..</c> segment, raw or
/// percent-encoded (<c>%2e%2e</c>), is refused there.
/// </para>
/// <para>
/// A file is sent as its bytes exactly, with no <c>.</c> line after them and no line-ending
/// conversion, whatever its item type: upstream curl writes every byte it receives until the
/// close. If the file shrinks while it is sent, the connection is aborted, so the client sees
/// a reset and not a short file that looks whole.
/// </para>
/// <para>
/// A directory is answered with a menu: one line per entry of the content store's listing,
/// in its order, <c>type display TAB selector TAB host TAB port CRLF</c>, then <c>.</c> CRLF.
/// The type is <c>1</c> for a directory; for a file, <c>0</c> for <c>.txt</c>, <c>.text</c>,
/// <c>.md</c>, <c>.csv</c> and <c>.log</c>, <c>g</c> for <c>.gif</c>, <c>I</c> for
/// <c>.png</c>, <c>.jpg</c>, <c>.jpeg</c> and <c>.bmp</c>, <c>h</c> for <c>.html</c> and
/// <c>.htm</c>, and <c>9</c> (binary) for any other. The display string is the entry's name;
/// the selector is the directory's path, <c>/</c> and the name, with <c>%</c> and every byte
/// outside printable ASCII percent-encoded. The host is the listen URL's host, or, when that
/// is the wildcard <c>0.0.0.0</c> or <c>::</c>, the connection's local address (without an IPv6 zone); the port is
/// the listen URL's bound port.
/// </para>
/// <para>
/// A selector the content store refuses, one where nothing exists, and one that vanishes
/// before it is read, are all answered with the same error menu, so a client cannot tell them
/// apart (ADR-0006 section 2): <c>3Nothing is served at this selector.</c>, an empty selector,
/// host <c>error.host</c>, port <c>1</c>, then the <c>.</c> line.
/// </para>
/// <para>
/// A client that closes before a whole line, a line longer than
/// <see cref="ExchangeLimits.MaxLineBytes"/> (line ending included), and a line that takes
/// longer than <see cref="ExchangeLimits.HeadTimeout"/>, get no bytes: the connection is
/// closed (ADR-0006 section 5).
/// </para>
/// </remarks>
public sealed class GopherProtocolServer : IConnectionProtocolServer
{
    private readonly ContentStore contentStore;

    /// <summary>
    /// Creates a Gopher server that serves <paramref name="contentStore"/>.
    /// </summary>
    /// <param name="contentStore">The content store every selector is looked up in.</param>
    public GopherProtocolServer(ContentStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(contentStore);

        this.contentStore = contentStore;
    }

    /// <summary>
    /// The one scheme answered: <c>gopher</c>. <c>gophers</c> joins it with the TLS contract.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["gopher"]);

    /// <summary>
    /// Reads the client's selector and answers it, then closes the connection.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var (outcome, line) = await ReadSelectorLineAsync(connection, context);
        if (outcome != GopherSelectorReadOutcome.LineRead)
        {
            context.Log.Note($"No selector was read ({outcome}); the connection was closed with no reply.");
            return;
        }

        var hasSearch = GopherSelector.SplitAtTab(line, out var selector);
        var requestPath = GopherSelector.ToRequestPath(selector);
        var answer = new GopherSelectorResponder(connection, context, contentStore, $"Selector \"{GopherLogText.Render(selector)}\"");
        if (hasSearch)
        {
            context.Log.Note($"Selector \"{GopherLogText.Render(selector)}\": the text after its TAB was ignored.");
        }

        await answer.AnswerAsync(requestPath);
    }

    private static async Task<(GopherSelectorReadOutcome Outcome, byte[] Line)> ReadSelectorLineAsync(IConnection connection, ExchangeContext context)
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            return await GopherSelectorReader.ReadAsync(connection, context.Limits.MaxLineBytes, linked.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return (GopherSelectorReadOutcome.HeadTimedOut, []);
        }
    }
}
