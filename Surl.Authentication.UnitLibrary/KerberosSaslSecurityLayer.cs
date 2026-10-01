using Surl.Kerberos;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// Kerberos's SASL security layer after an LDAP <c>GSS-SPNEGO</c> bind that selected Kerberos
/// (ADR-0072 Amendment 1): every message in either direction is one RFC 4121 wrap token under the
/// accepted context's key. Surl's are sealed when the client asked for confidentiality, as
/// <c>WinLDAP</c> does (measured), and only signed when it asked for integrity alone; the client's
/// are read sealed or signed, with any rotation, in sequence.
/// </summary>
/// <param name="context">The context the bind's AP-REQ opened.</param>
internal sealed class KerberosSaslSecurityLayer(KerberosSecurityContext context) : ISaslSecurityLayer
{
    /// <summary>
    /// Kerberos has no buffer limit of its own: the server's <c>--max-message</c> bounds the buffer.
    /// </summary>
    public int MaximumProtectedBytes => int.MaxValue;

    /// <summary>
    /// Whether a context's client asked for a layer at all: confidentiality or integrity.
    /// </summary>
    /// <param name="context">The accepted context.</param>
    /// <returns><see langword="true"/> when a layer follows the bind.</returns>
    public static bool IsNegotiated(KerberosSecurityContext context) =>
        context.IsConfidentialityRequested || context.IsIntegrityRequested;

    /// <inheritdoc/>
    public byte[] Protect(ReadOnlySpan<byte> message) =>
        context.IsConfidentialityRequested ? context.Seal(message) : context.Wrap(message);

    /// <inheritdoc/>
    public bool TryUnprotect(ReadOnlySpan<byte> buffer, out byte[] message) =>
        context.TryUnwrap(buffer, out message);
}
