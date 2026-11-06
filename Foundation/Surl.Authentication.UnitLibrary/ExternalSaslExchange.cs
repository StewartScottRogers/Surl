using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL <c>EXTERNAL</c>, RFC 4422 appendix A (ADR-0049, sections 4 and 5): one empty challenge
/// when no initial response was sent, then the response, the authorization identity, possibly
/// empty. The client is its verified TLS client certificate, named by the certificate's subject
/// simple name; the login is accepted when an account of exactly that name exists, its password
/// unused, and the authorization identity is empty or that name as UTF-8 bytes (ordinal): Surl
/// grants no proxy rights. The policy starts it only on a connection with a client certificate.
/// </summary>
internal sealed class ExternalSaslExchange(SaslExchangeContext context) : SaslMechanismExchange(context)
{
    /// <inheritdoc/>
    protected override ValueTask<SaslLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (response is not { } authzid)
        {
            return ValueTask.FromResult(Challenge(ReadOnlyMemory<byte>.Empty));
        }

        return Context.IsUnchecked ? ValueTask.FromResult(AcceptedUnchecked) : CheckAsync(authzid.Span, cancellationToken);
    }

    private ValueTask<SaslLoginStep> CheckAsync(ReadOnlySpan<byte> authzid, CancellationToken cancellationToken)
    {
        var name = Context.ClientCertificate!.GetNameInfo(X509NameType.SimpleName, false);
        var isAuthorized = authzid.IsEmpty || authzid.SequenceEqual(Encoding.UTF8.GetBytes(name));

        return Context.Accounts.HasNamedAccount(name) && isAuthorized
            ? ValueTask.FromResult(Accept(name, name))
            : RefuseAsync(name, cancellationToken);
    }
}
