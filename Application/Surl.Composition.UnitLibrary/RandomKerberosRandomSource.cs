using System.Security.Cryptography;
using Surl.Kerberos;

namespace Surl.Composition;

/// <summary>
/// The random bytes the Kerberos acceptor takes in production (ADR-0057, decision 6), from
/// <see cref="RandomNumberGenerator.Fill(Span{byte})"/>.
/// </summary>
public sealed class RandomKerberosRandomSource : IKerberosRandomSource
{
    /// <inheritdoc/>
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
