using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// What the extended-server composer a host may replace
/// (<see cref="SurlServerCompositionSeams.ComposeExtendedProtocolServers"/>) is given: the services
/// every protocol server is built from, composed once per start (ADR-0083, ADR-0124 decision 2).
/// </summary>
/// <param name="ComposedServices">The composed services, the same the original servers are built from,
/// among them the one content store, policy, object metadata index and clock the S3 server is built over.</param>
public sealed record ExtendedProtocolServerServices(ComposedProtocolServices ComposedServices)
{
    /// <summary>Gets the instance's one event stream (ADR-0124, decisions 1 and 9).</summary>
    public ISurlEventPublisher Events => ComposedServices.Events;

    /// <summary>Gets the instance's one fault plan (ADR-0124, decisions 4 and 9).</summary>
    public IFaultPlan Faults => ComposedServices.Faults;

    /// <summary>
    /// Gets the policy a server answering a scheme judges logins by, over the accounts allowed on it
    /// (ADR-0102, decision 4.3); <see langword="null"/> for the one policy on every scheme.
    /// </summary>
    public Func<string, Surl.Authentication.AuthenticationPolicy>? AuthenticationPolicyForScheme =>
        ComposedServices.AuthenticationPolicyForScheme;

    /// <summary>
    /// The policy an extended server answering <paramref name="scheme"/> judges logins by.
    /// </summary>
    /// <param name="scheme">The server's scheme.</param>
    /// <returns>The policy.</returns>
    public Surl.Authentication.AuthenticationPolicy AuthenticationPolicyFor(string scheme) =>
        ComposedServices.AuthenticationPolicyFor(scheme);
}
