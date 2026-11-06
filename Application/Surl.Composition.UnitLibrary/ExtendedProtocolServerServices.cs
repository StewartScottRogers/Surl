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
}
