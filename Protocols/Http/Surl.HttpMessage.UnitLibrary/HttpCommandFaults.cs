using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.HttpMessage;

/// <summary>
/// How the HTTP, RTSP and WebSocket servers ask the exchange's fault plan about a request once it
/// was let in (ADR-0124 decision 4; ADR-0134): the <see cref="FaultStage.Command"/> point a request
/// makes, the chosen-error rule it finds, the status an HTTP or RTSP reply names, and the
/// <see cref="FaultAppliedEvent"/> an applied rule publishes.
/// </summary>
public static class HttpCommandFaults
{
    private static readonly SearchValues<char> ReasonPhraseCharacters =
        SearchValues.Create("\t !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");

    /// <summary>
    /// Asks <see cref="ExchangeContext.Faults"/> about a request at the <see cref="FaultStage.Command"/>
    /// stage and applies what it finds (ADR-0124 decision 4; ADR-0134; ADR-0141 amendment 1): a
    /// <see cref="FaultKind.Delay"/> holds the request on the exchange's clock with its idle clock
    /// paused, and the request then goes on as if no rule matched; a <see cref="FaultKind.Drop"/> is
    /// noted and published here and handed back, for the server to end the exchange without a byte of
    /// an answer; a <see cref="FaultKind.Error"/> is handed back for the server to answer. Any other
    /// kind is not applied by these servers: it is noted in the exchange log and the request goes on.
    /// </summary>
    /// <param name="context">The exchange: its listen URL, plan, log and cancellation.</param>
    /// <param name="method">The request's method, or <c>GET</c> for a WebSocket upgrade.</param>
    /// <param name="path">The request path without its query, or <see langword="null"/> when the request names none.</param>
    /// <param name="accountName">The account the request logged in as; <see langword="null"/> or empty when anonymous.</param>
    /// <returns>The chosen-error or drop rule to act on, or <see langword="null"/>.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async ValueTask<FaultRule?> FindAndApplyAsync(ExchangeContext context, string method, string? path, string? accountName)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rule = await CommandStageFaults.FindAndApplyAsync(context, CommandPoint(context, method, path, accountName), path ?? method);
        if (rule is null or { Kind: FaultKind.Error or FaultKind.Drop })
        {
            return rule;
        }

        if (rule.Kind == FaultKind.Delay)
        {
            return null;
        }

        context.Log.Note($"{method} {path}: fault rule {rule.Id} asks for {rule.Kind} at the command stage, which this server does not apply; answered as without it.");

        return null;
    }

    /// <summary>
    /// <see cref="FindAndApplyAsync"/> for a server that applies only the <see cref="FaultKind.Error"/>
    /// kind at the command stage (the S3, Azure Storage and Google Cloud Storage servers, ADR-0134
    /// decision 5 as it stood): any other kind a rule asks for is noted in the exchange log, not
    /// applied, and the request goes on as if no rule matched.
    /// </summary>
    /// <param name="context">The exchange: its listen URL, plan, log and cancellation.</param>
    /// <param name="method">The request's method.</param>
    /// <param name="path">The request path without its query, or <see langword="null"/> when the request names none.</param>
    /// <param name="accountName">The account the request logged in as; <see langword="null"/> or empty when anonymous.</param>
    /// <returns>The chosen-error rule to answer with, or <see langword="null"/>.</returns>
    public static async ValueTask<FaultRule?> FindChosenErrorAsync(ExchangeContext context, string method, string? path, string? accountName)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rule = await context.Faults.FindAsync(CommandPoint(context, method, path, accountName), context.CancellationToken);
        if (rule is null or { Kind: FaultKind.Error })
        {
            return rule;
        }

        context.Log.Note($"{method} {path}: fault rule {rule.Id} asks for {rule.Kind} at the command stage, which this server does not apply; answered as without it.");

        return null;
    }

    private static FaultPoint CommandPoint(ExchangeContext context, string method, string? path, string? accountName) =>
        new(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = string.IsNullOrEmpty(accountName) ? null : accountName,
            Path = path,
            Command = method,
        };

    /// <summary>
    /// Reads the status an HTTP or RTSP chosen-error reply names: a status code 400 to 599, then,
    /// after one space, an optional reason phrase of tabs, spaces and visible ASCII characters
    /// (ADR-0124 decision 4's reply table).
    /// </summary>
    /// <param name="reply">The rule's <see cref="FaultRule.Reply"/>.</param>
    /// <param name="code">The status code, when the reply fits.</param>
    /// <param name="reasonPhrase">The reason phrase, or <see langword="null"/> when the reply gives none.</param>
    /// <returns><see langword="true"/> when the reply fits the HTTP and RTSP error shape.</returns>
    public static bool TryReadChosenStatus(string? reply, out int code, out string? reasonPhrase)
    {
        var fits = TrySplitCode(reply, out code, out reasonPhrase) && code is >= 400 and <= 599;

        return fits && (reasonPhrase is null || IsReasonPhrase(reasonPhrase));
    }

    // One or more tabs, spaces and visible ASCII characters (RFC 9112 section 4, less obs-text).
    private static bool IsReasonPhrase(string text) =>
        text.Length > 0 && !text.AsSpan().ContainsAnyExcept(ReasonPhraseCharacters);

    /// <summary>
    /// Splits a reply at its first space into a decimal code and the text after it (null when it has
    /// no space). The code is digits only, at most nine of them.
    /// </summary>
    /// <param name="reply">The reply, or <see langword="null"/>.</param>
    /// <param name="code">The code, when the reply starts with one.</param>
    /// <param name="text">The text after the first space, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the reply starts with a decimal code.</returns>
    public static bool TrySplitCode(string? reply, out int code, out string? text)
    {
        code = 0;
        var space = reply?.IndexOf(' ', StringComparison.Ordinal) ?? -1;
        var digits = space < 0 ? reply : reply![..space];
        text = space < 0 ? null : reply![(space + 1)..];
        if (!IsCode(digits))
        {
            return false;
        }

        code = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

        return true;
    }

    // One to nine decimal digits, so the code always fits an int.
    private static bool IsCode([NotNullWhen(true)] string? digits) =>
        digits is { Length: > 0 and <= 9 } && !digits.AsSpan().ContainsAnyExceptInRange('0', '9');

    /// <summary>
    /// Publishes that <paramref name="rule"/> was applied to a request at the
    /// <see cref="FaultStage.Command"/> stage, on <see cref="ExchangeContext.Events"/>, and notes it
    /// in the exchange log.
    /// </summary>
    /// <param name="context">The exchange.</param>
    /// <param name="rule">The applied rule.</param>
    /// <param name="method">The request's method.</param>
    /// <param name="path">The request path, or <see langword="null"/>; the event's target when given, else the method.</param>
    /// <param name="answer">What the server answered, for the log.</param>
    public static void PublishApplied(ExchangeContext context, FaultRule rule, string method, string? path, string answer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rule);

        context.Log.Note($"{method} {path}: answered {answer} by fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, rule.Kind, FaultStage.Command, context.ExchangeId, path ?? method));
    }
}
