using System.Globalization;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// One fault rule: a way to make the server misbehave on purpose (ADR-0124, decision 4), as
/// <see cref="SurlServerOptions.Faults"/> holds it before start and <see cref="SurlServerFaults.Add"/> takes it while
/// serving. It is a spelling of one <c>surl --fault</c> item, read by <c>surl</c>'s own reader, so every refusal is
/// <c>surl</c>'s. Make one with a factory, then narrow its scope with <see langword="with"/>; a scope left
/// <see langword="null"/> matches anything.
/// </summary>
/// <example>
/// <code>
/// // Drop the next two connections to the SMTP listener before any byte.
/// var drop = SurlFault.DropConnection() with { Listener = "smtp://127.0.0.1:0", Times = 2 };
/// </code>
/// </example>
public sealed record SurlFault
{
    private SurlFault(FaultKind kind) => Kind = kind;

    /// <summary>What the rule makes the server do: the item's head.</summary>
    public FaultKind Kind { get; }

    /// <summary>The listen URL the rule is scoped to (<c>listener</c>), as <c>surl</c> takes one; a port of 0 matches any bound port.</summary>
    public string? Listener { get; init; }

    /// <summary>The scheme the rule is scoped to (<c>scheme</c>); not given together with <see cref="Listener"/>.</summary>
    public string? Scheme { get; init; }

    /// <summary>The account the rule is scoped to (<c>account</c>); not at the accept stage.</summary>
    public string? Account { get; init; }

    /// <summary>The path, mailbox, topic, queue, selector or DN the rule is scoped to (<c>path</c>); not at the accept stage.</summary>
    public string? Path { get; init; }

    /// <summary>The verb a delay, drop or error is scoped to (<c>command</c>); not at the accept stage.</summary>
    public string? Command { get; init; }

    /// <summary>
    /// The stage a delay or drop acts at (<c>stage</c>): <see cref="FaultStage.Accept"/>, the default when
    /// <see langword="null"/>, or <see cref="FaultStage.Command"/>.
    /// </summary>
    public FaultStage? Stage { get; init; }

    /// <summary>How long a <see cref="Delay"/> waits (<c>ms</c>), in whole milliseconds from 0 to 600000.</summary>
    public TimeSpan? DelayDuration { get; init; }

    /// <summary>How many bytes a <see cref="DropConnection"/> at accept lets the server send before it drops (<c>after-bytes</c>).</summary>
    public long? AfterBytes { get; init; }

    /// <summary>The chosen error a <see cref="ProtocolError"/> answers (<c>reply</c>), in its scheme's error shape.</summary>
    public string? Reply { get; init; }

    /// <summary>How many times the rule applies before it is spent (<c>times</c>), 1 to 1000000; <see langword="null"/> for always.</summary>
    public int? Times { get; init; }

    /// <summary>Holds each matching exchange for <paramref name="duration"/> (<c>--fault delay;ms=&lt;n&gt;</c>), at accept unless <see cref="Stage"/> says command.</summary>
    /// <param name="duration">How long, in whole milliseconds from 0 to 600000.</param>
    /// <returns>The rule.</returns>
    public static SurlFault Delay(TimeSpan duration) => new(FaultKind.Delay) { DelayDuration = duration };

    /// <summary>Drops each matching connection (<c>--fault drop</c>): before any byte at accept, unless <see cref="AfterBytes"/> or <see cref="Stage"/> says otherwise.</summary>
    /// <returns>The rule.</returns>
    public static SurlFault DropConnection() => new(FaultKind.Drop);

    /// <summary>Refuses each matching login, whatever its password (<c>--fault refuse-login</c>).</summary>
    /// <returns>The rule.</returns>
    public static SurlFault RefuseLogin() => new(FaultKind.RefuseLogin);

    /// <summary>
    /// Answers each matching command with <paramref name="reply"/> (<c>--fault error;reply=&lt;reply&gt;</c>); needs
    /// <see cref="Scheme"/> or <see cref="Listener"/>, and the reply must fit that scheme's error shape (<c>503 Busy</c> for HTTP).
    /// </summary>
    /// <param name="reply">The chosen error.</param>
    /// <returns>The rule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reply"/> is null.</exception>
    public static SurlFault ProtocolError(string reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        return new(FaultKind.Error) { Reply = reply };
    }

    /// <summary>Makes each matching store write fail as a full disk would (<c>--fault out-of-space</c>).</summary>
    /// <returns>The rule.</returns>
    public static SurlFault OutOfSpace() => new(FaultKind.OutOfSpace);

    /// <summary>
    /// The rule a fault plan holds, spelt as a <see cref="SurlFault"/> without its count: every key but <c>times</c>,
    /// so <see cref="ToItem"/> spells what the rule does and where, whatever is left of its count.
    /// </summary>
    /// <param name="rule">The plan's rule.</param>
    /// <returns>The rule, <see cref="Times"/> left <see langword="null"/>.</returns>
    internal static SurlFault WithoutCountFrom(FaultRule rule) => new(rule.Kind)
    {
        Listener = rule.Listener,
        Scheme = rule.Scheme,
        Account = rule.Account,
        Path = rule.Path,
        Command = rule.Command,
        Stage = rule.Stage,
        DelayDuration = rule.DelayMilliseconds is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null,
        AfterBytes = rule.AfterBytes,
        Reply = rule.Reply,
    };

    /// <summary>
    /// The <c>--fault</c> item this rule spells: the kind's word, then each key given, in the order the ADR lists them,
    /// each value percent-encoded so the reader decodes it back unchanged.
    /// </summary>
    /// <returns>The item, e.g. <c>delay;listener=http%3A%2F%2F127.0.0.1%3A0;ms=250</c>.</returns>
    internal string ToItem()
    {
        var item = new StringBuilder(KindWord(Kind));
        AddKey(item, "listener", Listener);
        AddKey(item, "scheme", Scheme);
        AddKey(item, "account", Account);
        AddKey(item, "path", Path);
        AddKey(item, "command", Command);
        AddKey(item, "stage", Stage?.ToString().ToLowerInvariant());
        AddKey(item, "ms", DelayDuration is { } duration ? FormatMilliseconds(duration) : null);
        AddKey(item, "after-bytes", AfterBytes?.ToString(CultureInfo.InvariantCulture));
        AddKey(item, "reply", Reply);
        AddKey(item, "times", Times?.ToString(CultureInfo.InvariantCulture));
        return item.ToString();
    }

    private static string KindWord(FaultKind kind) => kind switch
    {
        FaultKind.Delay => "delay",
        FaultKind.Drop => "drop",
        FaultKind.RefuseLogin => "refuse-login",
        FaultKind.Error => "error",
        _ => "out-of-space",
    };

    // Milliseconds as ms reads them: whole ones as digits; a fraction or a sign is left for the reader to refuse.
    private static string FormatMilliseconds(TimeSpan duration) =>
        (duration.Ticks / (decimal)TimeSpan.TicksPerMillisecond).ToString(CultureInfo.InvariantCulture);

    private static void AddKey(StringBuilder item, string key, string? value)
    {
        if (value is not null)
        {
            item.Append(';').Append(key).Append('=').Append(Uri.EscapeDataString(value));
        }
    }
}
