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

    /// <summary>The error reply an error rule is read with in place of a custom server's own: one the stand-in scheme's shape accepts.</summary>
    private const string StandInReply = "500";

    private readonly Dictionary<string, Func<string, bool>?> fitsErrorReplies;
    private readonly IReadOnlyList<int> servedOrder;

    private CustomProtocolServers(
        Dictionary<string, Func<string, bool>?> fitsErrorReplies,
        IReadOnlyList<string> builtInListenUrls,
        IReadOnlyList<AdditionalProtocolServer> additionalProtocolServers,
        IReadOnlyList<int> servedOrder)
    {
        this.fitsErrorReplies = fitsErrorReplies;
        BuiltInListenUrls = builtInListenUrls;
        AdditionalProtocolServers = additionalProtocolServers;
        this.servedOrder = servedOrder;
    }

    /// <summary>The listen URLs that name no custom scheme, in the order given: those <c>surl</c> parses.</summary>
    public IReadOnlyList<string> BuiltInListenUrls { get; }

    /// <summary>The listen URLs <c>surl</c> is given: <see cref="BuiltInListenUrls"/>, or the stand-in when only custom servers have listen URLs.</summary>
    public IReadOnlyList<string> ListenUrlsToParse => UsesStandIn ? [StandInListenUrl] : BuiltInListenUrls;

    /// <summary>Whether <c>surl</c> parses the stand-in: only custom servers have listen URLs, so none is left for it to read.</summary>
    public bool UsesStandIn => BuiltInListenUrls.Count == 0 && AdditionalProtocolServers.Any(server => server.ListenUrls.Count > 0);

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
        var fitsErrorReplies = options.ProtocolServers.ToDictionary(
            server => server.Scheme, server => server.FitsErrorReply, StringComparer.OrdinalIgnoreCase);
        return new CustomProtocolServers(fitsErrorReplies, builtInListenUrls, additional, servedOrder);
    }

    /// <summary>
    /// Puts the bound URLs back in the order <see cref="SurlServerOptions.ListenUrls"/> gave them: the composition
    /// serves <c>surl</c>'s listen URLs first, then each custom server's.
    /// </summary>
    /// <param name="servedBoundUrls">The bound URLs in the order served.</param>
    /// <returns>The bound URLs in the order given, then any the command line added (a proxy listen URL from
    /// <see cref="SurlServerOptions.AdditionalArguments"/>) in the order served.</returns>
    public IReadOnlyList<Uri> InOrderGiven(IReadOnlyList<Uri> servedBoundUrls)
    {
        var inOrderGiven = new Uri[servedBoundUrls.Count];
        for (var served = 0; served < servedBoundUrls.Count; served++)
        {
            var position = served < servedOrder.Count ? servedOrder[served] : served;
            inOrderGiven[position] = servedBoundUrls[served];
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
        var customSchemes = CustomSchemesOf(fault);
        if (customSchemes.Count == 0)
        {
            return fault;
        }

        var readable = fault with
        {
            Listener = IsListenerCustom(fault) ? StandInListenUrl : fault.Listener,
            Scheme = IsSchemeCustom(fault) ? StandInScheme : fault.Scheme,
        };
        if (fault.Kind != FaultKind.Error)
        {
            return readable;
        }

        foreach (var customScheme in customSchemes)
        {
            ThrowUnlessReplyFits(fault, customScheme, parameterName);
        }

        // The stand-in scheme's own shape must accept the reply; the rule's real reply is put back after reading.
        return readable with { Reply = fault.Reply is null ? null : StandInReply };
    }

    private bool IsListenerCustom(SurlFault fault) => fault.Listener is { } listener && fitsErrorReplies.ContainsKey(SchemeOf(listener));

    private bool IsSchemeCustom(SurlFault fault) => fault.Scheme is { } scheme && fitsErrorReplies.ContainsKey(scheme);

    private List<string> CustomSchemesOf(SurlFault fault)
    {
        List<string> customSchemes = [];
        if (IsListenerCustom(fault))
        {
            customSchemes.Add(SchemeOf(fault.Listener!));
        }

        if (IsSchemeCustom(fault))
        {
            customSchemes.Add(fault.Scheme!);
        }

        return customSchemes;
    }

    /// <summary>Whether <paramref name="scheme"/> is a custom protocol server's scheme.</summary>
    /// <param name="scheme">The scheme, in any case.</param>
    /// <returns><see langword="true"/> for a custom scheme.</returns>
    public bool IsCustomScheme(string scheme) => fitsErrorReplies.ContainsKey(scheme);

    /// <summary>The rule as read, with <paramref name="fault"/>'s own listener, scheme and error reply back in place of any stand-in.</summary>
    /// <param name="read">The rule as <c>surl</c>'s reader read it.</param>
    /// <param name="fault">The rule as given.</param>
    /// <returns>The rule.</returns>
    public ConfiguredFault WithScopeOf(ConfiguredFault read, SurlFault fault) =>
        read with
        {
            Listener = fault.Listener,
            Scheme = fault.Scheme?.ToLowerInvariant(),
            Reply = ReplyAsGiven(read.Reply, fault),
        };

    /// <summary>Reads a rule added while serving, as <see cref="SurlServerFaults.Add"/> takes it.</summary>
    /// <param name="fault">The rule.</param>
    /// <param name="parameterName">The parameter a refusal names.</param>
    /// <returns>The rule for the plan.</returns>
    /// <exception cref="ArgumentException"><c>surl</c> would refuse the rule, or <see cref="ToReadable"/> does.</exception>
    public FaultRule Read(SurlFault fault, string parameterName)
    {
        var read = SurlServerFaults.Read(ToReadable(fault, parameterName), parameterName);
        return read with
        {
            Listener = fault.Listener,
            Scheme = fault.Scheme?.ToLowerInvariant(),
            Reply = ReplyAsGiven(read.Reply, fault),
        };
    }

    // An error rule scoped to a custom scheme was read with the stand-in reply; every other rule's reply is as read.
    private string? ReplyAsGiven(string? read, SurlFault fault) =>
        fault.Kind == FaultKind.Error && IsScopedToCustomScheme(fault) ? fault.Reply : read;

    private bool IsScopedToCustomScheme(SurlFault fault) => IsListenerCustom(fault) || IsSchemeCustom(fault);

    private void ThrowUnlessReplyFits(SurlFault fault, string scheme, string parameterName)
    {
        var fits = fitsErrorReplies[scheme];
        if (fits is null)
        {
            throw new ArgumentException(
                $"An error fault cannot be scoped to the custom protocol server's scheme {scheme} unless the server declares FitsErrorReply: surl knows no reply shape for it.",
                parameterName);
        }

        if (fault.Reply is null)
        {
            return;
        }

        bool isFitting;
        try
        {
            isFitting = fits(fault.Reply);
        }
        catch (Exception exception)
        {
            throw new ArgumentException(
                $"FitsErrorReply of the custom protocol server {scheme} threw for the error fault {fault.ToItem()}: {exception.Message}",
                parameterName,
                exception);
        }

        if (!isFitting)
        {
            throw new ArgumentException(
                $"The reply of the error fault {fault.ToItem()} does not fit the custom protocol server {scheme}'s FitsErrorReply.",
                parameterName);
        }
    }

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
