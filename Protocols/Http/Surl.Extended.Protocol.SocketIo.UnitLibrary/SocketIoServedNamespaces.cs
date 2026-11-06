namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The namespaces a listener serves (ADR-0120, decision 4): the main namespace <c>/</c>, and the
/// listen URL's path, with any trailing <c>/</c> removed, when that is not empty or <c>/</c>.
/// </summary>
public sealed class SocketIoServedNamespaces
{
    private SocketIoServedNamespaces(IReadOnlyList<string> names) => Names = names;

    /// <summary>
    /// The namespaces served, <c>/</c> first.
    /// </summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// The namespaces a listen URL's path names: <c>/admin</c> and <c>/admin/</c> serve <c>/</c>
    /// and <c>/admin</c>; an empty path or <c>/</c> serves <c>/</c> only.
    /// </summary>
    /// <param name="listenPath">The listen URL's path, or <see langword="null"/> when it has none.</param>
    /// <returns>The namespaces served.</returns>
    public static SocketIoServedNamespaces FromListenPath(string? listenPath)
    {
        var trimmed = (listenPath ?? string.Empty).TrimEnd('/');

        return trimmed.Length == 0
            ? new([SocketIoPacket.MainNamespace])
            : new([SocketIoPacket.MainNamespace, trimmed.StartsWith('/') ? trimmed : "/" + trimmed]);
    }

    /// <summary>
    /// Whether <paramref name="name"/> is served, compared byte for byte.
    /// </summary>
    /// <param name="name">A namespace a CONNECT names.</param>
    /// <returns><see langword="true"/> when it is one of <see cref="Names"/>.</returns>
    public bool Serves(string name) => Names.Contains(name, StringComparer.Ordinal);
}
