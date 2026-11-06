using System.Buffers.Text;
using System.Security.Cryptography;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Makes the random ids the server hands out - an Engine.IO session's <c>sid</c> and a Socket.IO
/// namespace connection's <c>sid</c> - each 15 bytes from a <see cref="RandomNumberGenerator"/> in
/// base64url without padding: 20 characters, the length of EIO "Handshake"'s example (ADR-0120,
/// decisions 3 and 4).
/// </summary>
public static class SocketIoRandomId
{
    /// <summary>The random bytes in one id.</summary>
    public const int IdBytes = 15;

    /// <summary>Makes a fresh id.</summary>
    /// <param name="random">The random number generator; injected so a test can make it repeatable.</param>
    /// <returns>The id: 20 base64url characters.</returns>
    public static string Create(RandomNumberGenerator random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var bytes = new byte[IdBytes];
        random.GetBytes(bytes);
        return Base64Url.EncodeToString(bytes);
    }
}
