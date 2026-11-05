namespace Surl.Composition;

/// <summary>
/// What the extended-server composer a host may replace
/// (<see cref="SurlServerCompositionSeams.ComposeExtendedProtocolServers"/>) is given: the services
/// every protocol server is built from, composed once per start (ADR-0083, ADR-0124 decision 2).
/// </summary>
/// <param name="ComposedServices">The composed services, the same the original servers are built from.</param>
public sealed record ExtendedProtocolServerServices(ComposedProtocolServices ComposedServices);
