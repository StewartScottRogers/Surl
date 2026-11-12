using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A test's own protocol server for a scheme Surl does not serve (ADR-0124, decision 2), given in
/// <see cref="SurlServerOptions.ProtocolServers"/>. It is composed beside the built-in servers and served on each
/// listen URL in <see cref="SurlServerOptions.ListenUrls"/> that names <paramref name="Scheme"/>, through the same
/// listener seam, limits, event stream and fault plan. Add only: a scheme an original or extended server claims,
/// turned on or not, is refused at start.
/// </summary>
/// <param name="Scheme">The scheme it answers, such as <c>echo</c>; matched without regard to case.</param>
/// <param name="Create">
/// Builds the server, once per start, from the instance's services; it returns an <see cref="IConnectionProtocolServer"/>
/// or an <see cref="IDatagramProtocolServer"/>.
/// </param>
public sealed record SurlProtocolServer(string Scheme, Func<SurlProtocolServices, IProtocolServer> Create);
