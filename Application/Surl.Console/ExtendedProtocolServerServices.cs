using Surl.Authentication;
using Surl.Content;

namespace Surl.Console;

/// <summary>
/// What an extended protocol server may be built from: the services the original servers are
/// built from, composed once per start (ADR-0083). A server that needs more adds it here.
/// </summary>
/// <param name="ContentStore">The one content store every protocol server reads.</param>
/// <param name="AuthenticationPolicy">The one policy every login is judged by.</param>
/// <param name="IsTlsUpgradeAvailable">Whether a certificate is configured, so a TLS upgrade can be offered.</param>
internal sealed record ExtendedProtocolServerServices(
    ContentStore ContentStore,
    AuthenticationPolicy AuthenticationPolicy,
    bool IsTlsUpgradeAvailable);
