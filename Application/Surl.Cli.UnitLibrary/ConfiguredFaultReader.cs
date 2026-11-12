using System.Collections.Frozen;
using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// Reads one <c>--fault &lt;kind&gt;[;&lt;key&gt;=&lt;value&gt;]...</c> argument (ADR-0124 decision 4) into a
/// <see cref="ConfiguredFault"/>: the item by <see cref="Shape"/> (ADR-0101 decision 3, with its
/// bounds and refusals), then the kind, the keys the kind allows, its required key, each number's
/// range, the stage, the scope and an <c>error</c> rule's reply checked against its scheme's error
/// shape. Every refusal follows <c>option &lt;name&gt;: </c> and never holds a value but a kind or scheme.
/// </summary>
internal static class ConfiguredFaultReader
{
    /// <summary>The most fault rules one instance holds (ADR-0124 decision 4).</summary>
    public const int MaxRules = 256;

    /// <summary>A <c>--fault</c> past <see cref="MaxRules"/>.</summary>
    public const string TooManyRules = "more than 256 fault rules";

    /// <summary><c>stage</c> given a word other than <c>accept</c> or <c>command</c>.</summary>
    public const string StageIsAcceptOrCommand = "key stage must be accept or command";

    /// <summary>A <c>listener</c> that is not a listen URL surl can serve.</summary>
    public const string ListenerIsNotAListenUrl = "key listener is not a listen URL";

    /// <summary><c>listener</c> and <c>scheme</c> together: a listener already names its scheme.</summary>
    public const string ListenerWithScheme = "key listener cannot be given with key scheme";

    /// <summary><c>after-bytes</c> with <c>stage=command</c>.</summary>
    public const string AfterBytesAtCommand = "key after-bytes cannot be given with stage=command";

    /// <summary>An <c>error</c> rule naming neither a scheme nor a listener, so its reply has no shape.</summary>
    public const string ErrorNeedsSchemeOrListener = "kind error needs key scheme or key listener";

    private const string ListenerKey = "listener";
    private const string SchemeKey = "scheme";
    private const string AccountKey = "account";
    private const string PathKey = "path";
    private const string CommandKey = "command";
    private const string StageKey = "stage";
    private const string MillisecondsKey = "ms";
    private const string AfterBytesKey = "after-bytes";
    private const string ReplyKey = "reply";
    private const string TimesKey = "times";

    private static readonly string[] ScopeKeys = [ListenerKey, SchemeKey, AccountKey, PathKey, TimesKey];

    /// <summary>The keys known only at a command, which a delay or drop at accept cannot match.</summary>
    private static readonly string[] KeysUnknownAtAccept = [AccountKey, PathKey, CommandKey];

    private static readonly FrozenDictionary<string, KindRule> KindsByWord = new Dictionary<string, KindRule>(StringComparer.Ordinal)
    {
        ["delay"] = new(FaultKind.Delay, [CommandKey, StageKey, MillisecondsKey], MillisecondsKey),
        ["drop"] = new(FaultKind.Drop, [CommandKey, StageKey, AfterBytesKey], null),
        ["error"] = new(FaultKind.Error, [CommandKey, ReplyKey], ReplyKey),
        ["out-of-space"] = new(FaultKind.OutOfSpace, [], null),
        ["refuse-login"] = new(FaultKind.RefuseLogin, [], null),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The <c>--fault</c> item: a kind, then any of the ten keys, each with one value.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<kind>[;<key>=<value>]",
        null,
        ConfigurationItemValueKind.None,
        new[] { ListenerKey, SchemeKey, AccountKey, PathKey, CommandKey, StageKey, MillisecondsKey, AfterBytesKey, ReplyKey, TimesKey }
            .ToDictionary(key => key, _ => ConfigurationItemAttributeKind.Value, StringComparer.Ordinal),
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredFault> Reading = new(Read, Shape.Reading.Type);

    /// <summary>A head that names no fault kind.</summary>
    /// <param name="kind">The head, decoded.</param>
    /// <returns><c>unknown kind &lt;kind&gt;</c>.</returns>
    public static string UnknownKind(string kind) => $"unknown kind {kind}";

    /// <summary>A key the item knows that the kind does not take.</summary>
    /// <param name="key">The key, lower-cased.</param>
    /// <param name="kind">The kind's word.</param>
    /// <returns><c>key &lt;key&gt; does not apply to &lt;kind&gt;</c>.</returns>
    public static string KeyDoesNotApply(string key, string kind) => $"key {key} does not apply to {kind}";

    /// <summary>A kind's required key missing.</summary>
    /// <param name="kind">The kind's word.</param>
    /// <param name="key">The required key.</param>
    /// <returns><c>kind &lt;kind&gt; needs key &lt;key&gt;</c>.</returns>
    public static string KindNeedsKey(string kind, string key) => $"kind {kind} needs key {key}";

    /// <summary>A number that is not decimal digits in its key's range.</summary>
    /// <param name="key">The key.</param>
    /// <param name="lowest">The smallest value allowed.</param>
    /// <param name="highest">The largest value allowed.</param>
    /// <returns><c>key &lt;key&gt; must be a whole number from &lt;lowest&gt; to &lt;highest&gt;</c>.</returns>
    public static string NumberOutOfRange(string key, long lowest, long highest) =>
        string.Create(CultureInfo.InvariantCulture, $"key {key} must be a whole number from {lowest} to {highest}");

    /// <summary>A <c>scheme</c> surl has no server for (ADR-0101 decision 5 row 10).</summary>
    /// <param name="scheme">The scheme, lower-cased.</param>
    /// <returns><c>unknown scheme &lt;scheme&gt;</c>.</returns>
    public static string UnknownScheme(string scheme) => $"unknown scheme {scheme}";

    /// <summary>A scope key a delay or drop at accept cannot match, because it is not known yet.</summary>
    /// <param name="key">The key.</param>
    /// <returns><c>key &lt;key&gt; cannot be given at stage accept</c>.</returns>
    public static string KeyUnknownAtAccept(string key) => $"key {key} cannot be given at stage accept";

    /// <summary>An <c>error</c> rule whose scheme has no error shape yet (the extended schemes, ADR-0124 decision 4 as amended).</summary>
    /// <param name="scheme">The scheme, lower-cased.</param>
    /// <returns><c>scheme &lt;scheme&gt; has no error shape</c>.</returns>
    public static string SchemeHasNoErrorShape(string scheme) => $"scheme {scheme} has no error shape";

    /// <summary>A <c>reply</c> that does not fit its scheme's error shape.</summary>
    /// <param name="scheme">The scheme, lower-cased.</param>
    /// <returns><c>key reply does not fit the &lt;scheme&gt; error shape</c>.</returns>
    public static string ReplyDoesNotFit(string scheme) => $"key reply does not fit the {scheme} error shape";

    /// <summary>Reads one <c>--fault</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The rule, when accepted; a bare rule of its kind (or a delay) otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredFault value)
    {
        value = new(FaultKind.Delay);
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item);
        if (failure is not null)
        {
            return failure;
        }

        var word = item.Name.ToLowerInvariant();
        if (!KindsByWord.TryGetValue(word, out var kind))
        {
            return UnknownKind(item.Name);
        }

        var values = item.Values;
        value = new(kind.Kind);
        return RefuseKeys(word, kind, values) ?? ReadFault(values, ref value);
    }

    /// <summary>Refuses the first key the kind does not take, then its required key missing.</summary>
    private static string? RefuseKeys(string word, KindRule kind, IReadOnlyDictionary<string, string> values)
    {
        var foreign = values.Keys.FirstOrDefault(key => !ScopeKeys.Contains(key) && !kind.OwnKeys.Contains(key));
        if (foreign is not null)
        {
            return KeyDoesNotApply(foreign, word);
        }

        return kind.RequiredKey is string required && !values.ContainsKey(required) ? KindNeedsKey(word, required) : null;
    }

    private static string? ReadFault(IReadOnlyDictionary<string, string> values, ref ConfiguredFault fault)
    {
        var failure = ReadNumbers(values, ref fault) ?? ReadStage(values, ref fault) ?? ReadScope(values, ref fault);
        return failure ?? RefuseStageScope(values, fault) ?? ReadReply(values, ref fault);
    }

    private static string? ReadNumbers(IReadOnlyDictionary<string, string> values, ref ConfiguredFault fault)
    {
        var millisecondsFailure = ReadNumber(values, MillisecondsKey, 0, 600000, out var milliseconds);
        var afterBytesFailure = ReadNumber(values, AfterBytesKey, 0, int.MaxValue, out var afterBytes);
        var timesFailure = ReadNumber(values, TimesKey, 1, 1000000, out var times);
        var failure = millisecondsFailure ?? afterBytesFailure ?? timesFailure;
        fault = fault with
        {
            DelayMilliseconds = (int?)milliseconds,
            AfterBytes = afterBytes,
            Times = (int?)times,
        };
        return failure;
    }

    private static string? ReadNumber(IReadOnlyDictionary<string, string> values, string key, long lowest, long highest, out long? number)
    {
        number = null;
        if (!values.TryGetValue(key, out var text))
        {
            return null;
        }

        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var read) || read < lowest || read > highest)
        {
            return NumberOutOfRange(key, lowest, highest);
        }

        number = read;
        return null;
    }

    private static string? ReadStage(IReadOnlyDictionary<string, string> values, ref ConfiguredFault fault)
    {
        if (!values.TryGetValue(StageKey, out var text))
        {
            return null;
        }

        FaultStage? stage = text.ToLowerInvariant() switch
        {
            "accept" => FaultStage.Accept,
            "command" => FaultStage.Command,
            _ => null,
        };
        fault = fault with { Stage = stage };
        return stage is null ? StageIsAcceptOrCommand : null;
    }

    /// <summary>Reads the scheme and listener, which must name a scheme surl serves and not come together, and the other scopes.</summary>
    private static string? ReadScope(IReadOnlyDictionary<string, string> values, ref ConfiguredFault fault)
    {
        var scheme = values.GetValueOrDefault(SchemeKey)?.ToLowerInvariant();
        var listener = values.GetValueOrDefault(ListenerKey);
        fault = fault with
        {
            Scheme = scheme,
            Listener = listener,
            Account = values.GetValueOrDefault(AccountKey),
            Path = values.GetValueOrDefault(PathKey),
            Command = values.GetValueOrDefault(CommandKey),
        };
        if (scheme is not null && !IsServedScheme(scheme))
        {
            return UnknownScheme(scheme);
        }

        if (listener is not null && !ListenUrlParser.Parse(listener, ExtendedSchemeDefaultPorts.Schemes).Succeeded)
        {
            return ListenerIsNotAListenUrl;
        }

        return scheme is not null && listener is not null ? ListenerWithScheme : null;
    }

    private static bool IsServedScheme(string scheme) =>
        SchemeDefaultPorts.TryGetDefaultPort(scheme, out _) || ExtendedSchemeDefaultPorts.TryGetDefaultPort(scheme, out _);

    /// <summary>
    /// Refuses what the stage cannot match: an account, path or command on a delay or drop at accept,
    /// where none is known yet, and <c>after-bytes</c> at command.
    /// </summary>
    private static string? RefuseStageScope(IReadOnlyDictionary<string, string> values, ConfiguredFault fault)
    {
        if (fault.Kind is not (FaultKind.Delay or FaultKind.Drop))
        {
            return null;
        }

        if (fault.Stage == FaultStage.Command)
        {
            return fault.AfterBytes is null ? null : AfterBytesAtCommand;
        }

        var unknown = KeysUnknownAtAccept.FirstOrDefault(values.ContainsKey);
        return unknown is null ? null : KeyUnknownAtAccept(unknown);
    }

    /// <summary>Reads an <c>error</c> rule's reply, which must fit the error shape of its scheme or its listener's.</summary>
    private static string? ReadReply(IReadOnlyDictionary<string, string> values, ref ConfiguredFault fault)
    {
        if (fault.Kind != FaultKind.Error)
        {
            return null;
        }

        var reply = values[ReplyKey];
        fault = fault with { Reply = reply };
        var scheme = ReplySchemeOf(fault);
        if (scheme is null)
        {
            return ErrorNeedsSchemeOrListener;
        }

        if (!FaultReplyShapes.HasShape(scheme))
        {
            return SchemeHasNoErrorShape(scheme);
        }

        return FaultReplyShapes.Fits(scheme, reply) ? null : ReplyDoesNotFit(scheme);
    }

    /// <summary>The scheme whose error shape a reply must fit: the rule's own, else its listener's, else null.</summary>
    private static string? ReplySchemeOf(ConfiguredFault fault) =>
        fault.Scheme ?? (fault.Listener is string listener ? ListenUrlParser.Parse(listener, ExtendedSchemeDefaultPorts.Schemes).ListenUrl!.Scheme : null);

    /// <summary>One kind's word table row: the kind, the keys it takes besides the scope keys, and the key it needs.</summary>
    private sealed record KindRule(FaultKind Kind, string[] OwnKeys, string? RequiredKey);
}
