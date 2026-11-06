namespace Surl.Kerberos;

/// <summary>
/// Where <see cref="KerberosAcceptor" /> and <see cref="KerberosSecurityContext" /> take their
/// random bytes: the acceptor's AP-REP sequence number and the confounder encrypted ahead of the
/// AP-REP (ADR-0057 decision 5). Injected, so tests fix every byte surl sends.
/// </summary>
public interface IKerberosRandomSource
{
    /// <summary>Fills <paramref name="destination" /> with random bytes.</summary>
    /// <param name="destination">The bytes to fill.</param>
    void Fill(Span<byte> destination);
}
