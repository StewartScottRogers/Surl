using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// What every SASL mechanism's exchange shares (ADR-0049, sections 5 and 6): the order of
/// <see cref="BeginAsync"/> and <see cref="ContinueAsync"/>, the steps a mechanism answers with,
/// and the refusal delay before a refused credential. Each mechanism answers one response at a
/// time in <see cref="AnswerAsync"/>, holding its own state between them.
/// </summary>
internal abstract class SaslMechanismExchange : ISaslExchange
{
    private readonly ReadOnlyMemory<byte>? initialResponse;
    private bool isBegun;
    private bool isAwaitingResponse;

    /// <summary>
    /// Starts an exchange for <paramref name="context"/>'s mechanism.
    /// </summary>
    /// <param name="context">The policy's accounts and delay, and how the client started.</param>
    protected SaslMechanismExchange(SaslExchangeContext context)
    {
        Context = context;
        initialResponse = context.InitialResponse;
    }

    /// <summary>
    /// The accounts, the delay, whether nothing is checked, and the mechanism's name.
    /// </summary>
    protected SaslExchangeContext Context { get; }

    /// <summary>
    /// The step accepting a login unchecked, under <c>--allow-anonymous</c> (ADR-0049, section 5).
    /// </summary>
    protected static MailLoginStep AcceptedUnchecked { get; } =
        new(MailLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null);

    /// <inheritdoc/>
    public ValueTask<MailLoginStep> BeginAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (isBegun)
        {
            throw new InvalidOperationException("The SASL exchange has already begun.");
        }

        isBegun = true;
        return TrackAsync(AnswerAsync(initialResponse, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask<MailLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!isAwaitingResponse)
        {
            throw new InvalidOperationException("The SASL exchange's last step was not a challenge.");
        }

        isAwaitingResponse = false;
        return TrackAsync(AnswerAsync(response, cancellationToken));
    }

    /// <summary>
    /// Answers one response: the initial one or <see langword="null"/> when none was sent (from
    /// <see cref="BeginAsync"/>), or the response to the last challenge.
    /// </summary>
    /// <param name="response">The decoded response, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the step and the refusal delay.</param>
    /// <returns>A challenge, or how the login ended.</returns>
    protected abstract ValueTask<MailLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken);

    /// <summary>
    /// A challenge carrying <paramref name="challenge"/>'s bytes, which the server base64-encodes.
    /// </summary>
    /// <param name="challenge">The continuation's bytes; empty for an empty challenge.</param>
    /// <param name="checkedLogin">The login note, when the challenge answers checked credentials.</param>
    /// <returns>The step.</returns>
    protected static MailLoginStep Challenge(ReadOnlyMemory<byte> challenge, CheckedLogin? checkedLogin = null) =>
        new(MailLoginOutcome.Challenge, challenge, null, checkedLogin);

    /// <summary>
    /// Accepts the login as <paramref name="accountName"/>, noting <paramref name="user"/>.
    /// </summary>
    /// <param name="accountName">The account whose credentials matched.</param>
    /// <param name="user">The user as the note names it.</param>
    /// <returns>The step.</returns>
    protected MailLoginStep Accept(string accountName, string? user) =>
        new(MailLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, accountName, Note(user, true));

    /// <summary>
    /// Refuses the credentials after the refusal delay, noting <paramref name="user"/>; a user that
    /// cannot be read is <see langword="null"/> and left out (ADR-0038, section 6).
    /// </summary>
    /// <param name="user">The user as the note names it.</param>
    /// <param name="cancellationToken">Cancels the delay.</param>
    /// <param name="refusalNote">Why, for the verbose log after the login note; <see langword="null"/> for no reason.</param>
    /// <returns>The step.</returns>
    protected async ValueTask<MailLoginStep> RefuseAsync(
        string? user, CancellationToken cancellationToken, string? refusalNote = null)
    {
        await Context.WaitRefusalDelayAsync(cancellationToken).ConfigureAwait(false);

        return new MailLoginStep(MailLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, Note(user, false), refusalNote);
    }

    /// <summary>
    /// The login note for this mechanism: its registered name and <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The user as the note names it.</param>
    /// <param name="isAccepted">Whether the credentials matched.</param>
    /// <returns>The note.</returns>
    protected CheckedLogin Note(string? user, bool isAccepted) => new(Context.Mechanism, user, isAccepted);

    private async ValueTask<MailLoginStep> TrackAsync(ValueTask<MailLoginStep> answer)
    {
        var step = await answer.ConfigureAwait(false);
        isAwaitingResponse = step.Outcome == MailLoginOutcome.Challenge;

        return step;
    }
}
