using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The random source a served SSH connection uses: the operating system's cryptographic
/// generator, <see cref="RandomNumberGenerator"/>.
/// </summary>
public sealed class SshSystemRandomSource : ISshRandomSource
{
    /// <inheritdoc/>
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
