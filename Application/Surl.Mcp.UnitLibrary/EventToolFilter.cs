using System.Text.Json;
using Surl.Api;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

/// <summary>
/// The filters <c>get_exchanges</c>, <c>get_events</c> and <c>wait_for_event</c> take (ADR-0124 decision 8), read
/// from a call's arguments: <c>scheme</c> and <c>listen_url</c> for all three, and <c>after_sequence</c>,
/// <c>kinds</c> and <c>account</c> for the two event tools. Everything but <c>kinds</c> becomes the
/// <see cref="SurlEventQuery"/> a .NET test would pass to <see cref="SurlServer"/>; <c>kinds</c>, which may name
/// several kinds, is matched by <see cref="IsOfKind"/>.
/// </summary>
/// <param name="Query">The query <see cref="SurlServer.FindEvents"/>, <see cref="SurlServer.FindExchanges"/> and <see cref="SurlServer.WaitForEventAsync"/> take.</param>
/// <param name="Kinds">The event kinds asked for, as <see cref="RecordedEventWriter.KindOf"/> names them, or <see langword="null"/> for any.</param>
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
    public const string EventProperties = """
            "after_sequence": { "type": "integer", "minimum": 0,
              "description": "Only events with a larger sequence; pass the last one seen to ask for the rest" },
            "kinds": { "type": "array", "minItems": 1,
              "items": { "type": "string", "enum": ["exchange_started", "exchange_ended", "login_judged", "store_changed", "fault_applied", "hook_failed", "consumer_group_changed"] },
              "description": "Only events of these kinds" },
        """ + SchemeAndListenUrlProperties + """
        ,
            "account": { "type": "string", "minLength": 1,
              "description": "Only events that name this account, or belong to an exchange that judged a login for it; case-sensitive" }
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
            AfterSequence = arguments.TryGetProperty("after_sequence", out var after) ? after.GetInt64() : 0,
        };
        filter = new EventToolFilter(query, ReadKinds(arguments));
        return null;
    }

    /// <summary>Whether <paramref name="surlEvent"/> is of a kind <see cref="Kinds"/> names; any when it names none.</summary>
    /// <param name="surlEvent">An event the query matched.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool IsOfKind(SurlEvent surlEvent) => Kinds is null || Kinds.Contains(RecordedEventWriter.KindOf(surlEvent));

    private static string? StringOrNull(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static HashSet<string>? ReadKinds(JsonElement arguments) =>
        arguments.TryGetProperty("kinds", out var kinds)
            ? new HashSet<string>(kinds.EnumerateArray().Select(kind => kind.GetString()!), StringComparer.Ordinal)
            : null;
}
