namespace Surl.Protocol.Abstractions;

/// <summary>
/// Why a listener could not bind (ADR-0004, section 6). <c>Surl.Core</c> maps it to the exit code.
/// </summary>
public enum ListenerBindFailure
{
    /// <summary>
    /// Any failure the other values do not name.
    /// </summary>
    Other = 0,

    /// <summary>
    /// Something else is already listening on the address and port.
    /// </summary>
    AddressInUse,

    /// <summary>
    /// The address is not one of this machine's.
    /// </summary>
    AddressNotAvailable,

    /// <summary>
    /// The operating system refused the bind, such as for a privileged port.
    /// </summary>
    PermissionDenied,

    /// <summary>
    /// The host name did not resolve to any address.
    /// </summary>
    HostNotFound,
}
