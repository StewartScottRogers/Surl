using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL <c>SCRAM-SHA-1</c>, RFC 5802, and <c>SCRAM-SHA-256</c>, RFC 7677, without channel binding
/// (ADR-0084, decision 4; BL-416). The client speaks first: with no initial response the exchange asks for the
/// <c>client-first-message</c> with an empty challenge. The <c>server-first-message</c> is
/// <c>r=&lt;client nonce&gt;&lt;server nonce&gt;,s=&lt;salt&gt;,i=4096</c>, the server nonce the
/// base64 of 18 random bytes, and the salt the account's, derived when the accounts were loaded
/// (<see cref="AccountBook.FindScramCredentials"/>); an unknown user gets a dummy salt that is the
/// same every time, so the answer never says whether the user exists. The
/// <c>client-final-message</c>'s proof is checked against <c>StoredKey</c> in fixed time, and a
/// match is answered with <c>v=&lt;ServerSignature&gt;</c>: as a challenge the client answers
/// empty, then accepted, on a server that sends no data with success (the mail servers), or as
/// <see cref="SaslLoginStep.AdditionalSuccessData"/> where it does
/// (<see cref="SaslExchangeContext.CanCarrySecurityLayer"/>). A refusal, after the refusal delay,
/// carries the <c>server-final-message</c> that refuses it, <c>e=&lt;server-error&gt;</c>, as its
/// <see cref="SaslLoginStep.RefusalNote"/>: <c>invalid-encoding</c> for a malformed message,
/// <c>channel-binding-not-supported</c> for <c>p=</c>, <c>extensions-not-supported</c> for
/// <c>m=</c>, <c>channel-bindings-dont-match</c> for a <c>c=</c> that is not the GS2 header,
/// <c>other-error</c> for a nonce that is not the one issued or an <c>a=</c> that is not the user,
/// and <c>invalid-proof</c> for a wrong proof and an unknown user alike. Under
/// <c>--allow-anonymous</c> every step runs and a well-formed final message ends the login accepted
/// unchecked. Passwords are normalized with <see cref="SaslPrep"/>.
/// </summary>
internal sealed class ScramSaslExchange : SaslMechanismExchange
{
    /// <summary>
    /// How many random bytes the server nonce is the base64 of.
    /// </summary>
    public const int ServerNonceLength = 18;

    private readonly ScramHash hash;
    private readonly Func<string> createServerNonce;
    private ScramClientFirstMessage? clientFirst;
    private ScramCredentials? credentials;
    private string? serverFirst;
    private string? issuedNonce;
    private SaslLoginStep? acceptance;

    /// <summary>
    /// Starts an exchange of <paramref name="hash"/>'s mechanism with server nonces from the
    /// policy's <see cref="ISaslNonceSource"/>.
    /// </summary>
    /// <param name="context">The policy and how the client started.</param>
    /// <param name="hash">The mechanism's hash.</param>
    public ScramSaslExchange(SaslExchangeContext context, ScramHash hash)
        : this(context, hash, () => Convert.ToBase64String(context.NonceSource.CreateNonce(ServerNonceLength)))
    {
    }

    /// <summary>
    /// As the other constructor, with the server nonce from <paramref name="createServerNonce"/>,
    /// so tests can replay the RFCs' examples.
    /// </summary>
    /// <param name="context">The policy and how the client started.</param>
    /// <param name="hash">The mechanism's hash.</param>
    /// <param name="createServerNonce">Makes the server's part of the nonce: printable ASCII but <c>,</c>.</param>
    internal ScramSaslExchange(SaslExchangeContext context, ScramHash hash, Func<string> createServerNonce)
        : base(context)
    {
        this.hash = hash;
        this.createServerNonce = createServerNonce;
    }

    /// <inheritdoc/>
    protected override ValueTask<SaslLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (acceptance is { } accepted)
        {
            return response.GetValueOrDefault().IsEmpty
                ? ValueTask.FromResult(accepted)
                : RefuseAsync(clientFirst!.UserName, cancellationToken, ServerError(ScramClientFirstMessage.InvalidEncoding));
        }

        if (response is not { } message)
        {
            return ValueTask.FromResult(Challenge(ReadOnlyMemory<byte>.Empty));
        }

        return serverFirst is null ? AnswerClientFirst(message.Span, cancellationToken) : AnswerClientFinal(message.Span, cancellationToken);
    }

    private ValueTask<SaslLoginStep> AnswerClientFirst(ReadOnlySpan<byte> message, CancellationToken cancellationToken)
    {
        var (first, error) = ScramClientFirstMessage.Read(message);
        if (first is null)
        {
            return RefuseAsync(null, cancellationToken, ServerError(error!));
        }

        clientFirst = first;
        credentials = Context.Accounts.FindScramCredentials(hash, first.UserName);
        issuedNonce = first.ClientNonce + createServerNonce();
        serverFirst = $"r={issuedNonce},s={Convert.ToBase64String(credentials.Salt)},i={credentials.IterationCount}";

        return ValueTask.FromResult(Challenge(Encoding.ASCII.GetBytes(serverFirst)));
    }

    private ValueTask<SaslLoginStep> AnswerClientFinal(ReadOnlySpan<byte> message, CancellationToken cancellationToken)
    {
        var final = ScramClientFinalMessage.Read(message);
        var user = clientFirst!.UserName;
        var error = final is null ? ScramClientFirstMessage.InvalidEncoding : FindFinalMessageError(final);
        if (error is not null)
        {
            return RefuseAsync(user, cancellationToken, ServerError(error));
        }

        return CheckProof(final!, user, cancellationToken);
    }

    private ValueTask<SaslLoginStep> CheckProof(ScramClientFinalMessage final, string user, CancellationToken cancellationToken)
    {
        var authMessage = Encoding.UTF8.GetBytes($"{clientFirst!.Bare},{serverFirst},{final.WithoutProof}");
        var serverFinal = Encoding.ASCII.GetBytes($"v={Convert.ToBase64String(hash.Hmac(credentials!.ServerKey, authMessage))}");
        if (Context.IsUnchecked)
        {
            return ValueTask.FromResult(Succeed(AcceptedUnchecked, serverFinal));
        }

        var isAuthorized = clientFirst.AuthorizationId is null || clientFirst.AuthorizationId == user;
        if (!(ProofMatches(final.Proof, authMessage) & credentials.AccountName is not null))
        {
            return RefuseAsync(user, cancellationToken, ServerError("invalid-proof"));
        }

        return isAuthorized
            ? ValueTask.FromResult(Succeed(Accept(credentials.AccountName!, user), serverFinal))
            : RefuseAsync(user, cancellationToken, ServerError("other-error"));
    }

    private string? FindFinalMessageError(ScramClientFinalMessage final)
    {
        if (!final.ChannelBinding.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(clientFirst!.Gs2Header)))
        {
            return "channel-bindings-dont-match";
        }

        return final.Nonce == issuedNonce ? null : "other-error";
    }

    // ClientKey := ClientProof XOR HMAC(StoredKey, AuthMessage); a match hashes it to StoredKey.
    private bool ProofMatches(byte[] proof, byte[] authMessage)
    {
        var clientKey = hash.Hmac(credentials!.StoredKey, authMessage);
        var sentProof = proof.Length == hash.Length ? proof : new byte[hash.Length];
        for (var index = 0; index < clientKey.Length; index++)
        {
            clientKey[index] ^= sentProof[index];
        }

        return Context.Accounts.SecretComparer.FixedTimeEquals(hash.Hash(clientKey), credentials.StoredKey)
            & proof.Length == hash.Length;
    }

    private SaslLoginStep Succeed(SaslLoginStep accepted, byte[] serverFinal)
    {
        if (Context.CanCarrySecurityLayer)
        {
            return accepted with { AdditionalSuccessData = serverFinal };
        }

        acceptance = accepted;

        return Challenge(serverFinal);
    }

    private static string ServerError(string error) => $"e={error}";
}
