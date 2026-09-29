using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

/// <summary>
/// The DICT server (RFC 2229): greets the client, then answers its command lines one after
/// another until it sends <c>QUIT</c> or closes the connection. ADR-0011 records the answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where definitions come from.</b> The one database, <c>surl</c> ("Files served by
/// surl"), is the files directly in the content store's served root: a file's name is a
/// headword and its bytes are the definition, sent as UTF-8 text with every line ending as
/// CRLF and every line that starts with <c>.</c> dot-stuffed. Names are compared ordinally.
/// A word that starts with <c>.</c>, names a directory, or is a name the content store
/// refuses has no definition. In <c>DEFINE</c> and <c>MATCH</c>, <c>!</c> and <c>*</c> name
/// the database too; <c>SHOW INFO</c> takes only <c>surl</c>. Strategies are
/// <c>exact</c> and <c>prefix</c>, and <c>.</c>, the server's default, is <c>prefix</c>.
/// </para>
/// <para>
/// <b>The banner</b> is <c>220 surl DICT server &lt;mime&gt; &lt;</c><i>n</i><c>@surl&gt;</c>,
/// with <i>n</i> the exchange's <see cref="ExchangeContext.ExchangeId"/> as the message
/// id. It names no version (ADR-0006, section 3). It is sent as soon as the connection is
/// served, before anything is read; upstream curl 8.21.0 sends its commands without waiting
/// for it.
/// </para>
/// <para>
/// <b>Replies.</b> A word with no definition, and a <c>MATCH</c> that matches nothing, is
/// answered <c>552 no match</c>. A command Surl does not know, and a blank line, <c>500
/// unknown command</c>; <c>AUTH</c>, <c>SASLAUTH</c> and <c>SASLRESP</c> <c>502 command not
/// implemented</c>; the wrong number of parameters or an unclosed quote <c>501 syntax
/// error, illegal parameters</c>; an unknown database <c>550</c> and an unknown strategy
/// <c>551</c>. <c>CLIENT</c> and <c>OPTION MIME</c> are answered <c>250 ok</c>; after
/// <c>OPTION MIME</c> every text begins with MIME headers and a blank line.
/// <c>QUIT</c> is answered <c>221 bye</c> and the connection is closed.
/// </para>
/// <para>
/// <b>Limits.</b> A command line longer than <see cref="ExchangeLimits.MaxLineBytes"/>, its
/// line ending included, is answered <c>500 line too long</c> and the connection is closed
/// without reading the rest (ADR-0006, sections 1 and 5). A client that closes the
/// connection part way through a line gets no reply. If a definition's file cannot be read
/// once its <c>151</c> line is sent, the connection is aborted, because the reply can no
/// longer be completed truthfully.
/// </para>
/// </remarks>
public sealed class DictProtocolServer : IConnectionProtocolServer
{
    private readonly DictContentDictionary dictionary;

    /// <summary>
    /// Creates a DICT server whose database is the files in <paramref name="contentStore"/>'s served root.
    /// </summary>
    /// <param name="contentStore">The content store whose root holds the definitions.</param>
    public DictProtocolServer(ContentStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(contentStore);

        dictionary = new DictContentDictionary(contentStore);
    }

    /// <summary>
    /// The one scheme answered: <c>dict</c>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["dict"]);

    /// <summary>
    /// Sends the banner, then answers every command line on <paramref name="connection"/>
    /// until the client quits or closes it, or a line is too long.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var cancellationToken = context.CancellationToken;
        await WriteAsync(connection, $"220 surl DICT server <mime> <{context.ExchangeId}@surl>\r\n", cancellationToken);

        var reader = new DictLineReader(connection, context.Limits.MaxLineBytes);
        var responder = new DictCommandResponder(connection, context, dictionary);
        var keepsConnectionOpen = true;

        while (keepsConnectionOpen)
        {
            var result = await reader.ReadLineAsync(cancellationToken);
            keepsConnectionOpen = result.Line is { } line
                ? await responder.AnswerAsync(line)
                : await AnswerNoLineAsync(connection, context, result.Outcome);
        }
    }

    private static async Task<bool> AnswerNoLineAsync(IConnection connection, ExchangeContext context, DictLineReadOutcome outcome)
    {
        switch (outcome)
        {
            case DictLineReadOutcome.LineTooLong:
                context.Log.Note($"A command line was longer than {context.Limits.MaxLineBytes} bytes; answered 500 and closed.");
                await WriteAsync(connection, "500 line too long\r\n", context.CancellationToken);
                break;
            case DictLineReadOutcome.ConnectionClosedMidLine:
                context.Log.Note("The client closed the connection part way through a command line.");
                break;
        }

        return false;
    }

    private static ValueTask WriteAsync(IConnection connection, string text, CancellationToken cancellationToken) =>
        connection.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
}
