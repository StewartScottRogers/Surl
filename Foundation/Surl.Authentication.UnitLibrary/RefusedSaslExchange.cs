using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// An exchange refused before any credential is read (ADR-0049, section 7): a mechanism that is
/// unknown, not accepted or not offered (<see cref="SaslLoginOutcome.RefusedMechanism"/>), or a
/// plain-text one on an unencrypted connection without <c>--allow-plaintext-auth</c>
/// (<see cref="SaslLoginOutcome.RefusedPlaintext"/>). Its one step is not delayed and carries no
/// note, since nothing was checked.
/// </summary>
/// <param name="outcome">The refusal.</param>
internal sealed class RefusedSaslExchange(SaslLoginOutcome outcome) : ISaslExchange
{
    private readonly SaslLoginStep refusal = new(outcome, ReadOnlyMemory<byte>.Empty, null, null);
    private bool isBegun;

    /// <inheritdoc/>
    public ValueTask<SaslLoginStep> BeginAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (isBegun)
        {
            throw new InvalidOperationException("The SASL exchange has already begun.");
        }

        isBegun = true;
        return ValueTask.FromResult(refusal);
    }

    /// <inheritdoc/>
    public ValueTask<SaslLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The SASL exchange's last step was not a challenge.");
}
