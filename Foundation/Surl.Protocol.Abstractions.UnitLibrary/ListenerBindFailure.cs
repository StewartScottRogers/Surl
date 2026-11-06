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
    /// Something else is already listening on the address and port, or, for a Unix socket file,
    /// something already stands at its path (ADR-0094, decision 5).
    /// </summary>
    AddressInUse,

    /// <summary>
    /// The address is not one of this machine's, or a Unix socket file's directory does not exist.
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

    /// <summary>
    /// A Unix socket path or abstract name is longer, in UTF-8 bytes, than the platform's
    /// <c>sun_path</c> holds (ADR-0094, decision 5).
    /// </summary>
    UnixSocketPathTooLong,

    /// <summary>
    /// An abstract Unix socket name was asked for on a platform other than Linux, the only one
    /// that has them (ADR-0094, decision 3).
    /// </summary>
    AbstractUnixSocketUnsupported,
}
