using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

/// <summary>
/// Answers the DICT commands of one connection, one line at a time (RFC 2229, section 3).
/// ADR-0011 records every reply below.
/// </summary>
/// <remarks>
/// Command names and <c>SHOW</c> and <c>OPTION</c> subjects are matched without regard to
/// case; database and strategy names, and words, ordinally. The reply text is Surl's own
/// fixed text, and a word the client sent comes back only inside a quoted string with
/// <c>"</c> and <c>\</c> escaped (ADR-0006, section 3). It is not safe for concurrent calls.
/// </remarks>
internal sealed class DictCommandResponder
{
    /// <summary>
    /// The name of the one database served.
    /// </summary>
    public const string DatabaseName = "surl";

    private const string DatabaseLine = DatabaseName + " \"Files served by surl\"";
    private const string Ok = "250 ok\r\n";
    private const string MimeHeader = "Content-type: text/plain; charset=utf-8\r\nContent-transfer-encoding: 8bit\r\n\r\n";

    private static readonly string[] HelpLines =
    [
        "CLIENT info                  -- identify the client",
        "DEFINE database word         -- look up word in database",
        "MATCH database strategy word -- match word in database using strategy",
        "SHOW DB                      -- list all accessible databases",
        "SHOW STRAT                   -- list available matching strategies",
        "SHOW INFO database           -- provide information about the database",
        "SHOW SERVER                  -- provide site-specific information",
        "OPTION MIME                  -- use MIME headers",
        "STATUS                       -- display timing information",
        "HELP                         -- display this help information",
        "QUIT                         -- terminate connection",
    ];

    private static readonly string[] DatabaseInformationLines =
    [
        "The files directly in the directory surl serves.",
        "Each file's name is a headword, and its contents are its definition.",
    ];

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly DictContentDictionary dictionary;
    private readonly Dictionary<string, Func<IReadOnlyList<string>, Task<bool>>> commands;
    private readonly Dictionary<string, (int WordCount, Func<IReadOnlyList<string>, Task<bool>> Answer)> showSubjects;
    private bool mimeHeadersRequested;

    /// <summary>
    /// Creates a responder that answers on <paramref name="connection"/> from <paramref name="dictionary"/>.
    /// </summary>
    /// <param name="connection">The connection every reply is written to.</param>
    /// <param name="context">The exchange, for its cancellation token.</param>
    /// <param name="dictionary">Where definitions and headwords come from.</param>
    public DictCommandResponder(IConnection connection, ExchangeContext context, DictContentDictionary dictionary)
    {
        this.connection = connection;
        this.context = context;
        this.dictionary = dictionary;
        commands = new(StringComparer.Ordinal)
        {
            ["CLIENT"] = AnswerClientAsync,
            ["DEFINE"] = AnswerDefineAsync,
            ["MATCH"] = AnswerMatchAsync,
            ["SHOW"] = AnswerShowAsync,
            ["OPTION"] = AnswerOptionAsync,
            ["STATUS"] = _ => ReplyAsync("210 status ok\r\n"),
            ["HELP"] = _ => ReplyWithTextAsync("113 help text follows\r\n", HelpLines),
            ["QUIT"] = AnswerQuitAsync,
            ["AUTH"] = _ => ReplyAsync("502 command not implemented\r\n"),
            ["SASLAUTH"] = _ => ReplyAsync("502 command not implemented\r\n"),
            ["SASLRESP"] = _ => ReplyAsync("502 command not implemented\r\n"),
        };
        showSubjects = new(StringComparer.Ordinal)
        {
            ["DB"] = (2, AnswerShowDatabasesAsync),
            ["DATABASES"] = (2, AnswerShowDatabasesAsync),
            ["STRAT"] = (2, AnswerShowStrategiesAsync),
            ["STRATEGIES"] = (2, AnswerShowStrategiesAsync),
            ["INFO"] = (3, AnswerShowInfoAsync),
            ["SERVER"] = (2, AnswerShowServerAsync),
        };
    }

    /// <summary>
    /// Answers one command line.
    /// </summary>
    /// <param name="line">The line, without its line ending.</param>
    /// <returns><see langword="true"/> when the connection stays open for the next command; <see langword="false"/> after <c>QUIT</c>.</returns>
    public Task<bool> AnswerAsync(string line)
    {
        if (DictCommandTokenizer.Split(line) is not { } words)
        {
            return ReplyAsync("501 syntax error, illegal parameters\r\n");
        }

        if (words.Count == 0 || !commands.TryGetValue(words[0].ToUpperInvariant(), out var answer))
        {
            return ReplyAsync("500 unknown command\r\n");
        }

        return answer(words);
    }

    private static bool IsDatabase(string name) => name is DatabaseName or "!" or "*";

    private static string Quote(string word) =>
        "\"" + word.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private Task<bool> AnswerClientAsync(IReadOnlyList<string> words) =>
        words.Count < 2 ? ReplyAsync("501 syntax error, illegal parameters\r\n") : ReplyAsync(Ok);

    private async Task<bool> AnswerDefineAsync(IReadOnlyList<string> words)
    {
        if (words.Count != 3)
        {
            return await ReplyAsync("501 syntax error, illegal parameters\r\n");
        }

        if (!IsDatabase(words[1]))
        {
            return await ReplyAsync("550 invalid database, use \"SHOW DB\" for list of databases\r\n");
        }

        if (dictionary.FindDefinition(words[2]) is not { } file)
        {
            return await ReplyAsync("552 no match\r\n");
        }

        await WriteAsync($"150 1 definitions retrieved\r\n151 {Quote(words[2])} {DatabaseLine}\r\n{MimeHeaderIfRequested()}");
        await using var text = new DictTextWriteStream(connection);
        try
        {
            await dictionary.CopyDefinitionAsync(file, text, context.CancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            context.Log.Note($"{file.Mapping.Location} could not be read while it was sent ({exception.Message}); the connection was aborted.");
            connection.Abort();

            return false;
        }

        await text.EndTextAsync(context.CancellationToken);

        return await ReplyAsync(Ok);
    }

    private Task<bool> AnswerMatchAsync(IReadOnlyList<string> words)
    {
        if (words.Count != 4)
        {
            return ReplyAsync("501 syntax error, illegal parameters\r\n");
        }

        if (!IsDatabase(words[1]))
        {
            return ReplyAsync("550 invalid database, use \"SHOW DB\" for list of databases\r\n");
        }

        if (DictMatchStrategy.Find(words[2]) is not { } strategy)
        {
            return ReplyAsync("551 invalid strategy, use \"SHOW STRAT\" for a list of strategies\r\n");
        }

        var headwords = dictionary.MatchHeadwords(strategy, words[3], context.CancellationToken);
        if (headwords.Count == 0)
        {
            return ReplyAsync("552 no match\r\n");
        }

        return ReplyWithTextAsync(
            $"152 {headwords.Count} matches found\r\n",
            headwords.Select(headword => $"{DatabaseName} {Quote(headword)}"));
    }

    private Task<bool> AnswerShowAsync(IReadOnlyList<string> words)
    {
        if (words.Count < 2
            || !showSubjects.TryGetValue(words[1].ToUpperInvariant(), out var subject)
            || words.Count != subject.WordCount)
        {
            return ReplyAsync("501 syntax error, illegal parameters\r\n");
        }

        return subject.Answer(words);
    }

    private Task<bool> AnswerShowDatabasesAsync(IReadOnlyList<string> words) =>
        ReplyWithTextAsync("110 1 databases present\r\n", [DatabaseLine]);

    private Task<bool> AnswerShowStrategiesAsync(IReadOnlyList<string> words) =>
        ReplyWithTextAsync(
            $"111 {DictMatchStrategy.All.Count} strategies available\r\n",
            DictMatchStrategy.All.Select(strategy => $"{strategy.Name} \"{strategy.Description}\""));

    private Task<bool> AnswerShowServerAsync(IReadOnlyList<string> words) =>
        ReplyWithTextAsync("114 server information follows\r\n", ["surl DICT server"]);

    private Task<bool> AnswerShowInfoAsync(IReadOnlyList<string> words) =>
        words[2] == DatabaseName
            ? ReplyWithTextAsync("112 database information follows\r\n", DatabaseInformationLines)
            : ReplyAsync("550 invalid database, use \"SHOW DB\" for list of databases\r\n");

    private Task<bool> AnswerOptionAsync(IReadOnlyList<string> words)
    {
        if (words.Count != 2 || !string.Equals(words[1], "MIME", StringComparison.OrdinalIgnoreCase))
        {
            return ReplyAsync("501 syntax error, illegal parameters\r\n");
        }

        mimeHeadersRequested = true;

        return ReplyAsync(Ok);
    }

    private async Task<bool> AnswerQuitAsync(IReadOnlyList<string> words)
    {
        await WriteAsync("221 bye\r\n");

        return false;
    }

    private string MimeHeaderIfRequested() => mimeHeadersRequested ? MimeHeader : string.Empty;

    private Task<bool> ReplyWithTextAsync(string statusLine, IEnumerable<string> lines)
    {
        var reply = new StringBuilder(statusLine).Append(MimeHeaderIfRequested());
        foreach (var line in lines)
        {
            reply.Append(line).Append("\r\n");
        }

        return ReplyAsync(reply.Append(".\r\n").Append(Ok).ToString());
    }

    private async Task<bool> ReplyAsync(string reply)
    {
        await WriteAsync(reply);

        return true;
    }

    private ValueTask WriteAsync(string text) =>
        connection.WriteAsync(Encoding.UTF8.GetBytes(text), context.CancellationToken);
}
