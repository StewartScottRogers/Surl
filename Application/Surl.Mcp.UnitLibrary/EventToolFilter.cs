using System.Text.Json;
using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// The filters <c>get_exchanges</c>, <c>get_events</c> and <c>wait_for_event</c> take (ADR-0124 decision 8), read
/// from a call's arguments: <c>scheme</c> and <c>listen_url</c> for all three, and <c>after_sequence</c>,
/// <c>kinds</c>, <c>account</c>, <c>store</c> and <c>name</c> for the two event tools. Everything but <c>kinds</c> becomes the
/// <see cref="SurlEventQuery"/> a .NET test would pass to <see cref="SurlServer"/>; <c>kinds</c>, which may name
/// several kinds, is matched by <see cref="IsOfKind"/>.
/// </summary>
/// <param name="Query">The query <see cref="SurlServer.FindEvents"/>, <see cref="SurlServer.FindExchanges"/> and <see cref="SurlServer.WaitForEventAsync"/> take.</param>
/// <param name="Kinds">The event kinds asked for, as <see cref="SurlEvent.KindName"/> names them, or <see langword="null"/> for any.</param>
internal sealed record EventToolFilter(SurlEventQuery Query, IReadOnlySet<string>? Kinds)
{
    /// <summary>The <c>scheme</c> and <c>listen_url</c> properties every one of the three tools' schemas holds.</summary>
    public const string SchemeAndListenUrlProperties = """
            "scheme": { "type": "string", "minLength": 1,
              "description": "Only what came through a listen URL of this scheme, e.g. smtp; case-insensitive" },
            "listen_url": { "type": "string", "minLength": 1,
              "description": "Only what came through this listen URL, as bound_urls lists it, e.g. http://127.0.0.1:49731/" }
        """;

    /// <summary>The properties <c>get_events</c> and <c>wait_for_event</c> both take, after <c>server_id</c>.</summary>
    public static readonly string EventProperties = """
            "after_sequence": { "type": "integer", "minimum": 0, "maximum": 9007199254740991,
              "description": "Only events with a larger sequence; pass the last one seen to ask for the rest" },
            "kinds": { "type": "array", "minItems": 1,
              "items": { "type": "string", "enum": [
        """ + KindEnumList() + """
              ] },
              "description": "Only events of these kinds. exchange_bytes is in the list but is never kept, so asking for it finds no events and a wait for it times out; read the bytes of an exchange with get_exchanges" },
        """ + SchemeAndListenUrlProperties + """
        ,
            "account": { "type": "string", "minLength": 1,
              "description": "Only events that name this account, or belong to an exchange that judged a login for it; case-sensitive" },
            "store": { "type": "string", "enum": ["content", "mail", "bus", "accounts", "keyspace", "directory", "zones", "exchanges", "sessions", "producers", "uploads", "proxy_accounts", "tls_srp_accounts"],
              "description": "Only changes to this store; no other kind of event matches" },
            "name": { "type": "string", "minLength": 1,
              "description": "Only changes to this name (a path, mailbox, topic, queue, key or account), exactly or as a segment prefix: /uploads matches /uploads and /uploads/a.txt but not /uploads2; case-sensitive; no other kind of event matches" }
        """;

    /// <summary>Reads the filters <paramref name="arguments"/> give.</summary>
    /// <param name="arguments">A call's arguments, already checked against the tool's schema.</param>
    /// <param name="filter">The filter, when every argument is good.</param>
    /// <returns><see langword="null"/>, or why the arguments are refused: a <c>listen_url</c> that is not an absolute URL.</returns>
    public static string? TryRead(JsonElement arguments, out EventToolFilter? filter)
    {
        filter = null;
        Uri? listenUrl = null;
        if (arguments.TryGetProperty("listen_url", out var listenUrlText)
            && !Uri.TryCreate(listenUrlText.GetString(), UriKind.Absolute, out listenUrl))
        {
            return $"listen_url is not an absolute URL: {listenUrlText.GetString()}";
        }

        var query = new SurlEventQuery
        {
            Scheme = StringOrNull(arguments, "scheme"),
            ListenUrl = listenUrl,
            Account = StringOrNull(arguments, "account"),
            Store = ReadStore(arguments),
            Name = StringOrNull(arguments, "name"),
            AfterSequence = arguments.TryGetProperty("after_sequence", out var after) ? after.GetWholeInt64() : 0,
        };
        filter = new EventToolFilter(query, ReadKinds(arguments));
        return null;
    }

    /// <summary>Whether <paramref name="surlEvent"/> is of a kind <see cref="Kinds"/> names; any when it names none.</summary>
    /// <param name="surlEvent">An event the query matched.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool IsOfKind(SurlEvent surlEvent) => Kinds is null || Kinds.Contains(surlEvent.KindName);

    /// <summary>The kind of every <see cref="SurlEvent"/> record the abstractions export, named by <c>ReadAllKinds</c>, as <see cref="SurlEvent.KindName"/> spells it, in ordinal order (ADR-0171).</summary>
    internal static IReadOnlyList<string> AllKinds => ReadAllKinds();

    // Reflection over the assembly's types is trim- and AOT-unsafe (IL2026), so the records are named here by
    // typeof; a test enumerates the exported SurlEvent subclasses and fails when one is missing (ADR-0171).
    private static string[] ReadAllKinds() => new[]
        {
            typeof(ConsumerGroupChangedEvent),
            typeof(CookieReceivedEvent),
            typeof(ExchangeBytesEvent),
            typeof(ExchangeEndedEvent),
            typeof(ExchangeStartedEvent),
            typeof(FaultAppliedEvent),
            typeof(HookFailedEvent),
            typeof(LoginJudgedEvent),
            typeof(StoreChangedEvent),
        }
        .Select(type => KindNameOf(type.Name))
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string KindNameOf(string typeName) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(typeName[..^"Event".Length]);

    private static string KindEnumList() => string.Join(", ", AllKinds.Select(kind => "\"" + kind + "\""));

    private static StoreKind? ReadStore(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("store", out var store))
        {
            return null;
        }

        // The schema has already restricted the value to the snake_case names of StoreKind.
        return Enum.GetValues<StoreKind>().Single(kind => JsonNamingPolicy.SnakeCaseLower.ConvertName(kind.ToString()) == store.GetString());
    }

    private static string? StringOrNull(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static HashSet<string>? ReadKinds(JsonElement arguments) =>
        arguments.TryGetProperty("kinds", out var kinds)
            ? new HashSet<string>(kinds.EnumerateArray().Select(kind => kind.GetString()!), StringComparer.Ordinal)
            : null;
}
