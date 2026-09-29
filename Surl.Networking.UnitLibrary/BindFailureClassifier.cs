using System.Collections.Frozen;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Names why a bind or a host-name resolution failed, from the <see cref="SocketError"/>
/// behind it (ADR-0004, section 6). Networking never picks an exit code; <c>Surl.Core</c>
/// maps the <see cref="ListenerBindFailure"/> to one.
/// </summary>
internal static class BindFailureClassifier
{
    private static readonly FrozenDictionary<SocketError, ListenerBindFailure> FailureBySocketError =
        new Dictionary<SocketError, ListenerBindFailure>
        {
            [SocketError.AddressAlreadyInUse] = ListenerBindFailure.AddressInUse,
            [SocketError.AddressNotAvailable] = ListenerBindFailure.AddressNotAvailable,
            [SocketError.AccessDenied] = ListenerBindFailure.PermissionDenied,
            [SocketError.HostNotFound] = ListenerBindFailure.HostNotFound,
            [SocketError.NoData] = ListenerBindFailure.HostNotFound,
        }.ToFrozenDictionary();

    /// <summary>
    /// Maps <paramref name="socketError"/> to the <see cref="ListenerBindFailure"/> it means.
    /// </summary>
    /// <param name="socketError">The error the socket call reported.</param>
    /// <returns>The failure; <see cref="ListenerBindFailure.Other"/> for an error ADR-0004 does not name.</returns>
    public static ListenerBindFailure Classify(SocketError socketError) =>
        FailureBySocketError.GetValueOrDefault(socketError, ListenerBindFailure.Other);
}
