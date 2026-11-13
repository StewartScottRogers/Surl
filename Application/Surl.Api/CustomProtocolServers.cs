using Surl.Cli;
using Surl.Composition;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// The test's own protocol servers of one start (<see cref="SurlServerOptions.ProtocolServers"/>, ADR-0124 decision 2):
/// which listen URLs are theirs and which <c>surl</c> parses, each server's registration with its listen URLs for the
/// composition's <see cref="SurlServerCompositionSeams.AdditionalProtocolServers"/>, and how a fault rule scoped to a
/// custom scheme passes <c>surl</c>'s <c>--fault</c> reader, which knows only the built-in schemes.
/// </summary>
internal sealed class CustomProtocolServers
{
    /// <summary>
    /// The listen URL <c>surl</c> parses in place of a custom one: in a fault rule, and as the command line's only
    /// listen URL when every listen URL is custom, since <c>surl</c> refuses a command line without one.
    /// </summary>
    private const string StandInListenUrl = "http://127.0.0.1:0";

    /// <summary>The scheme <c>surl</c> reads in place of a custom one in a fault rule.</summary>
    private const string StandInScheme = "http";

    /// <summary>Every scheme an original or extended server claims, turned on or not: a test may not take one.</summary>
    private static readonly HashSet<string> BuiltInSchemes = new(
        [
            .. SchemeDefaultPorts.Schemes,
            .. ExtendedSchemeDefaultPorts.Schemes,
            .. ProtocolServerRegistrations.Original.SelectMany(registration => registration.Schemes),
            .. ExtendedProtocolServers.Registrations.SelectMany(registration => registration.Schemes),
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> schemes;
    private readonly IReadOnlyList<int> servedOrder;

    private CustomProtocolServers(
        HashSet<string> schemes,
        IReadOnlyList<string> builtInListenUrls,
        IReadOnlyList<AdditionalProtocolServer> additionalProtocolServers,
        IReadOnlyList<int> servedOrder)
    {
        this.schemes = schemes;
        BuiltInListenUrls = builtInListenUrls;
        AdditionalProtocolServers = additionalProtocolServers;
        this.servedOrder = servedOrder;
    }

    /// <summary>The listen URLs that name no custom scheme, in the order given: those <c>surl</c> parses.</summary>
    public IReadOnlyList<string> BuiltInListenUrls { get; }

    /// <summary>The listen URLs <c>surl</c> is given: <see cref="BuiltInListenUrls"/>, or the stand-in when there are none.</summary>
    public IReadOnlyList<string> ListenUrlsToParse => BuiltInListenUrls.Count == 0 ? [StandInListenUrl] : BuiltInListenUrls;

    /// <summary>Each custom server, in the order given, with the listen URLs that name its scheme, in the order given.</summary>
    public IReadOnlyList<AdditionalProtocolServer> AdditionalProtocolServers { get; }

    /// <summary>
    /// Reads <paramref name="options"/>' custom servers and splits its listen URLs between them and <c>surl</c>.
    /// </summary>
    /// <param name="options">The options, already checked by <see cref="SurlServerArguments.ThrowIfMisused"/>.</param>
    /// <returns>The custom servers.</returns>
    /// <exception cref="ArgumentException">A custom server has a null scheme or factory, a scheme that is not a URI
    /// scheme, a built-in scheme or one another custom server has, or a listen URL that is not
    /// <c>scheme://host:port</c>.</exception>
    public static CustomProtocolServers Of(SurlServerOptions options)
    {
        var schemes = ReadSchemes(options.ProtocolServers);
        var listenUrlsByScheme = schemes.ToDictionary(
            scheme => scheme, _ => new List<(int Index, ListenUrl ListenUrl)>(), StringComparer.OrdinalIgnoreCase);
        List<string> builtInListenUrls = [];
        List<int> builtInIndexes = [];
        for (var index = 0; index < options.ListenUrls.Count; index++)
        {
            var listenUrl = options.ListenUrls[index];
            if (listenUrlsByScheme.TryGetValue(SchemeOf(listenUrl), out var custom))
            {
                custom.Add((index, ReadListenUrl(listenUrl)));
            }
            else
            {
                builtInListenUrls.Add(listenUrl);
                builtInIndexes.Add(index);
            }
        }

        var customListenUrls = options.ProtocolServers.Select(server => listenUrlsByScheme[server.Scheme]).ToArray();
        AdditionalProtocolServer[] additional =
        [
            .. options.ProtocolServers.Select((server, position) => new AdditionalProtocolServer(
                ToRegistration(server), [.. customListenUrls[position].Select(given => given.ListenUrl)])),
        ];
        int[] servedOrder = [.. builtInIndexes, .. customListenUrls.SelectMany(given => given.Select(listenUrl => listenUrl.Index))];
        return new CustomProtocolServers(schemes, builtInListenUrls, additional, servedOrder);
    }

    /// <summary>
    /// Puts the bound URLs back in the order <see cref="SurlServerOptions.ListenUrls"/> gave them: the composition
    /// serves <c>surl</c>'s listen URLs first, then each custom server's.
    /// </summary>
    /// <param name="servedBoundUrls">The bound URLs in the order served.</param>
    /// <returns>The bound URLs in the order given.</returns>
    public IReadOnlyList<Uri> InOrderGiven(IReadOnlyList<Uri> servedBoundUrls)
    {
        var inOrderGiven = new Uri[servedBoundUrls.Count];
        for (var served = 0; served < servedBoundUrls.Count; served++)
        {
            inOrderGiven[servedOrder[served]] = servedBoundUrls[served];
        }

        return inOrderGiven;
    }

    /// <summary>
    /// The fault rule <c>surl</c>'s reader is given for <paramref name="fault"/>: a listener or scheme naming a custom
    /// scheme becomes the stand-in, since the reader refuses a scheme it does not know.
    /// </summary>
    /// <param name="fault">The rule.</param>
    /// <param name="parameterName">The parameter a refusal names.</param>
    /// <returns>The rule to read.</returns>
    /// <exception cref="ArgumentException">An <c>error</c> rule is scoped to a custom scheme, whose reply has no shape
    /// <c>surl</c> knows.</exception>
    public SurlFault ToReadable(SurlFault fault, string parameterName)
    {
        var isListenerCustom = fault.Listener is { } listener && schemes.Contains(SchemeOf(listener));
        var isSchemeCustom = fault.Scheme is { } scheme && schemes.Contains(scheme);
        if (!isListenerCustom && !isSchemeCustom)
        {
            return fault;
        }

        return fault.Kind == FaultKind.Error
            ? throw new ArgumentException(
                "An error fault cannot be scoped to a custom protocol server's scheme: surl knows no reply shape for it.", parameterName)
            : fault with
            {
                Listener = isListenerCustom ? StandInListenUrl : fault.Listener,
                Scheme = isSchemeCustom ? StandInScheme : fault.Scheme,
            };
    }

    /// <summary>The rule as read, with <paramref name="fault"/>'s own listener and scheme back in place of any stand-in.</summary>
    /// <param name="read">The rule as <c>surl</c>'s reader read it.</param>
    /// <param name="fault">The rule as given.</param>
    /// <returns>The rule.</returns>
    public static ConfiguredFault WithScopeOf(ConfiguredFault read, SurlFault fault) =>
        read with { Listener = fault.Listener, Scheme = fault.Scheme?.ToLowerInvariant() };

    /// <summary>Reads a rule added while serving, as <see cref="SurlServerFaults.Add"/> takes it.</summary>
    /// <param name="fault">The rule.</param>
    /// <param name="parameterName">The parameter a refusal names.</param>
    /// <returns>The rule for the plan.</returns>
    /// <exception cref="ArgumentException"><c>surl</c> would refuse the rule, or <see cref="ToReadable"/> does.</exception>
    public FaultRule Read(SurlFault fault, string parameterName) =>
        SurlServerFaults.Read(ToReadable(fault, parameterName), parameterName) with
        {
            Listener = fault.Listener,
            Scheme = fault.Scheme?.ToLowerInvariant(),
        };

    private static HashSet<string> ReadSchemes(IList<SurlProtocolServer> servers)
    {
        var schemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var server in servers)
        {
            if (server.Scheme is null || server.Create is null || !Uri.CheckSchemeName(server.Scheme))
            {
                throw new ArgumentException(
                    "A protocol server in ProtocolServers needs a factory and a scheme that is a URI scheme.", "options");
            }

            if (BuiltInSchemes.Contains(server.Scheme) || !schemes.Add(server.Scheme))
            {
                throw new ArgumentException(
                    $"ProtocolServers may only add a scheme: {server.Scheme} is served by Surl or given twice.", "options");
            }
        }

        return schemes;
    }

    private static string SchemeOf(string listenUrl)
    {
        var separator = listenUrl.IndexOf("://", StringComparison.Ordinal);
        return separator < 0 ? string.Empty : listenUrl[..separator];
    }

    // scheme://host:port with an optional trailing slash, read with System.Uri; IPv6 brackets are removed as for
    // every listen URL.
    private static ListenUrl ReadListenUrl(string listenUrl)
    {
        if (!Uri.TryCreate(listenUrl, UriKind.Absolute, out var uri)
            || uri.Port < 0
            || uri.UserInfo.Length > 0
            || uri.PathAndQuery is not ("/" or "")
            || uri.Fragment.Length > 0)
        {
            throw new ArgumentException(
                $"The listen URL {listenUrl} names a custom protocol server's scheme and must be <scheme>://<host>:<port>, with its port (0 for an ephemeral one).",
                "options");
        }

        return new ListenUrl(uri.Scheme.ToLowerInvariant(), uri.Host.Trim('[', ']'), uri.Port);
    }

    private static ProtocolServerRegistration ToRegistration(SurlProtocolServer server) =>
        new(
            [server.Scheme.ToLowerInvariant()],
            services => server.Create(new SurlProtocolServices(services))
                ?? throw new InvalidOperationException($"The protocol server factory for scheme {server.Scheme} returned null."));
}
