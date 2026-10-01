using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Unicode;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL <c>PLAIN</c>, RFC 4616 (ADR-0049, section 5): one empty challenge when no initial response
/// was sent, then the response <c>[authzid] NUL authcid NUL passwd</c>, the two identities UTF-8
/// and the password compared as the bytes sent. The <c>authzid</c> must be empty or equal to the
/// <c>authcid</c> (ordinal): Surl grants no proxy rights. Any other shape is refused as a bad
/// credential with no user in the note.
/// </summary>
internal sealed class PlainSaslExchange(SaslExchangeContext context) : SaslMechanismExchange(context)
{
    /// <inheritdoc/>
    protected override ValueTask<SaslLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (response is not { } message)
        {
            return ValueTask.FromResult(Challenge(ReadOnlyMemory<byte>.Empty));
        }

        return Context.IsUnchecked ? ValueTask.FromResult(AcceptedUnchecked) : CheckAsync(message.Span, cancellationToken);
    }

    // The password is always compared, so a refused authzid costs what a wrong password does.
    private ValueTask<SaslLoginStep> CheckAsync(ReadOnlySpan<byte> message, CancellationToken cancellationToken)
    {
        if (!TrySplit(message, out var authzid, out var authcid, out var passwordStart))
        {
            return RefuseAsync(null, cancellationToken);
        }

        var user = authcid.Length > 0 ? authcid : null;
        var matches = Context.Accounts.CheckPassword(authcid, message[passwordStart..]);

        return matches && (authzid.Length == 0 || authzid == authcid)
            ? ValueTask.FromResult(Accept(authcid, user))
            : RefuseAsync(user, cancellationToken);
    }

    // authzid NUL authcid NUL passwd: exactly two NULs, and both identities UTF-8.
    private static bool TrySplit(
        ReadOnlySpan<byte> message,
        [NotNullWhen(true)] out string? authzid,
        [NotNullWhen(true)] out string? authcid,
        out int passwordStart)
    {
        var hasTwoNuls = TryFindTwoNuls(message, out var firstNul, out var secondNul);
        passwordStart = secondNul + 1;
        authzid = hasTwoNuls ? ReadIdentity(message[..firstNul]) : null;
        authcid = hasTwoNuls ? ReadIdentity(message[(firstNul + 1)..secondNul]) : null;

        return authzid is not null && authcid is not null;
    }

    private static bool TryFindTwoNuls(ReadOnlySpan<byte> message, out int firstNul, out int secondNul)
    {
        firstNul = message.IndexOf((byte)0);
        secondNul = firstNul + 1 + message[(firstNul + 1)..].IndexOf((byte)0);

        return firstNul >= 0 && secondNul > firstNul && !message[(secondNul + 1)..].Contains((byte)0);
    }

    private static string? ReadIdentity(ReadOnlySpan<byte> identity) =>
        Utf8.IsValid(identity) ? Encoding.UTF8.GetString(identity) : null;
}
