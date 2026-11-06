using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// The decisions behind a Unix domain socket listener (ADR-0094, decisions 3, 5 and 6), kept
/// apart from the socket so the fast tests cover every platform's arm: what is refused before a
/// socket is made, the name the endpoint is bound with, and how an accepted client is named.
/// </summary>
internal static class UnixSocketBindRules
{
    /// <summary>
    /// The longest socket file path on Linux and Windows, in UTF-8 bytes: <c>sun_path</c>'s 108
    /// less its terminating NUL. Also the longest abstract name, which spends the first byte on
    /// the leading NUL instead.
    /// </summary>
    public const int LinuxAndWindowsMaximumPathBytes = 107;

    /// <summary>
    /// The longest socket file path on macOS, in UTF-8 bytes: <c>sun_path</c>'s 104 less its
    /// terminating NUL.
    /// </summary>
    public const int MacOsMaximumPathBytes = 103;

    /// <summary>
    /// The longest path or abstract name this platform's <c>sun_path</c> holds, in UTF-8 bytes.
    /// </summary>
    /// <param name="isMacOs">Whether the platform is macOS.</param>
    /// <returns><see cref="MacOsMaximumPathBytes"/> on macOS, otherwise <see cref="LinuxAndWindowsMaximumPathBytes"/>.</returns>
    public static int MaximumPathBytes(bool isMacOs) => isMacOs ? MacOsMaximumPathBytes : LinuxAndWindowsMaximumPathBytes;

    /// <summary>
    /// Finds why <paramref name="address"/> must be refused before any socket is made: an abstract
    /// name off Linux, a path or name too long for <c>sun_path</c>, or a path where something
    /// already stands, which <c>surl</c> never deletes.
    /// </summary>
    /// <param name="address">The socket asked for.</param>
    /// <param name="isLinux">Whether the platform is Linux, the only one with abstract names.</param>
    /// <param name="isMacOs">Whether the platform is macOS, whose <c>sun_path</c> is shorter.</param>
    /// <param name="pathExists">Whether anything - a file, a socket, a directory - stands at a path.</param>
    /// <returns>The failure, or <see langword="null"/> when the bind may be tried.</returns>
    public static ListenerBindFailure? FindRefusalBeforeBinding(
        UnixSocketAddress address, bool isLinux, bool isMacOs, Func<string, bool> pathExists)
    {
        if (address.IsAbstract && !isLinux)
        {
            return ListenerBindFailure.AbstractUnixSocketUnsupported;
        }

        if (Encoding.UTF8.GetByteCount(address.Name) > MaximumPathBytes(isMacOs))
        {
            return ListenerBindFailure.UnixSocketPathTooLong;
        }

        return address.IsAbstract ? null : FindPathRefusal(address.Name, pathExists);
    }

    /// <summary>
    /// The name a <see cref="UnixDomainSocketEndPoint"/> is made with: the path, or the abstract
    /// name behind its leading NUL.
    /// </summary>
    /// <param name="address">The socket asked for.</param>
    /// <returns>The endpoint's name.</returns>
    public static string EndPointNameOf(UnixSocketAddress address) =>
        address.IsAbstract ? "\0" + address.Name : address.Name;

    /// <summary>
    /// Names an accepted client: <see cref="UnnamedUnixSocketPeer.Instance"/> when it bound no
    /// name - no endpoint at all, or one whose name is empty or only NULs, as Windows reports it -
    /// and otherwise the endpoint it bound.
    /// </summary>
    /// <param name="remoteEndPoint">The accepted socket's remote endpoint, if it could be read.</param>
    /// <returns>The peer the exchange context names.</returns>
    public static EndPoint DescribePeer(EndPoint? remoteEndPoint) =>
        remoteEndPoint is null || IsUnnamed(remoteEndPoint.ToString())
            ? UnnamedUnixSocketPeer.Instance
            : remoteEndPoint;

    // Something at the path is AddressInUse; a directory that is not there is AddressNotAvailable,
    // which the operating systems report differently (Windows says NetworkDown), so it is
    // checked here rather than classified from the bind's SocketError.
    private static ListenerBindFailure? FindPathRefusal(string path, Func<string, bool> pathExists)
    {
        if (pathExists(path))
        {
            return ListenerBindFailure.AddressInUse;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));

        return directory is null || pathExists(directory) ? null : ListenerBindFailure.AddressNotAvailable;
    }

    private static bool IsUnnamed(string? name) =>
        string.IsNullOrEmpty(name?.TrimStart('@').Trim('\0', ' '));
}
