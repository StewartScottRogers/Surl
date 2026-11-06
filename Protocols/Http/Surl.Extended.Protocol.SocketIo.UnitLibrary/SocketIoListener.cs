using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// What every session of one listener shares: the namespaces it serves, its sessions, and the
/// policy that judges a CONNECT's credentials (ADR-0120, decisions 4, 5 and 7).
/// </summary>
/// <param name="Namespaces">The namespaces served.</param>
/// <param name="Sessions">The listener's sessions.</param>
/// <param name="AuthenticationPolicy">The policy that judges each CONNECT's login.</param>
internal sealed record SocketIoListener(
    SocketIoServedNamespaces Namespaces,
    SocketIoSessions Sessions,
    IAuthenticationPolicy AuthenticationPolicy);
