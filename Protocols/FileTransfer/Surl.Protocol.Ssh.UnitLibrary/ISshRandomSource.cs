namespace Surl.Protocol.Ssh;

/// <summary>
/// Where the SSH server's random bytes come from: the <c>SSH_MSG_KEXINIT</c> cookie and the
/// padding of every binary packet (RFC 4253, sections 6 and 7.1). Injected so a test can fix
/// them and pin the bytes the server writes.
/// </summary>
public interface ISshRandomSource
{
    /// <summary>
    /// Fills <paramref name="destination"/> with random bytes.
    /// </summary>
    /// <param name="destination">Where the bytes go.</param>
    void Fill(Span<byte> destination);
}
