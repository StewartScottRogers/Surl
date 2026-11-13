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

    // The login this step judged, and the account it accepted, until TrackAsync concludes it.
    private CheckedLogin? judgedLogin;
    private string? judgedAccountName;

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
    protected static SaslLoginStep AcceptedUnchecked { get; } =
        new(SaslLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null);

    /// <inheritdoc/>
    public ValueTask<SaslLoginStep> BeginAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (isBegun)
        {
            throw new InvalidOperationException("The SASL exchange has already begun.");
        }

        isBegun = true;
        return TrackAsync(AnswerAsync(initialResponse, cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SaslLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!isAwaitingResponse)
        {
            throw new InvalidOperationException("The SASL exchange's last step was not a challenge.");
        }

        isAwaitingResponse = false;
        return TrackAsync(AnswerAsync(response, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Answers one response: the initial one or <see langword="null"/> when none was sent (from
    /// <see cref="BeginAsync"/>), or the response to the last challenge.
    /// </summary>
    /// <param name="response">The decoded response, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the step and the refusal delay.</param>
    /// <returns>A challenge, or how the login ended.</returns>
    protected abstract ValueTask<SaslLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken);

    /// <summary>
    /// A challenge carrying <paramref name="challenge"/>'s bytes, which the server base64-encodes.
    /// </summary>
    /// <param name="challenge">The continuation's bytes; empty for an empty challenge.</param>
    /// <param name="checkedLogin">The login note, when the challenge answers checked credentials.</param>
    /// <returns>The step.</returns>
    protected static SaslLoginStep Challenge(ReadOnlyMemory<byte> challenge, CheckedLogin? checkedLogin = null) =>
        new(SaslLoginOutcome.Challenge, challenge, null, checkedLogin);

    /// <summary>
    /// Accepts the login as <paramref name="accountName"/>, noting <paramref name="user"/>.
    /// </summary>
    /// <param name="accountName">The account whose credentials matched.</param>
    /// <param name="user">The user as the note names it.</param>
    /// <returns>The step.</returns>
    protected SaslLoginStep Accept(string accountName, string? user)
    {
        judgedAccountName = accountName;
        judgedLogin = Note(user, true);

        return new(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, accountName, judgedLogin);
    }

    /// <summary>
    /// The login note of credentials refused without <see cref="RefuseAsync"/> (a mechanism that answers
    /// a wrong credential with an error challenge), recorded as this step's judged login.
    /// </summary>
    /// <param name="user">The user as the note names it.</param>
    /// <returns>The note.</returns>
    protected CheckedLogin RefusedNote(string? user)
    {
        var refused = Note(user, false);
        judgedLogin = refused;

        return refused;
    }

    /// <summary>
    /// Refuses credentials that matched because a <see cref="FaultKind.RefuseLogin"/> rule applies
    /// (ADR-0124, decision 4): as this mechanism refuses a wrong credential, after the refusal delay.
    /// </summary>
    /// <param name="user">The user as the note names it.</param>
    /// <param name="cancellationToken">Cancels the delay.</param>
    /// <returns>The step.</returns>
    protected virtual ValueTask<SaslLoginStep> RefuseByFaultAsync(string? user, CancellationToken cancellationToken) =>
        RefuseAsync(user, cancellationToken);

    /// <summary>
    /// Refuses the credentials after the refusal delay, noting <paramref name="user"/>; a user that
    /// cannot be read is <see langword="null"/> and left out (ADR-0038, section 6).
    /// </summary>
    /// <param name="user">The user as the note names it.</param>
    /// <param name="cancellationToken">Cancels the delay.</param>
    /// <param name="refusalNote">Why, for the verbose log after the login note; <see langword="null"/> for no reason.</param>
    /// <param name="checkStartedAt">When the check started, so the delay is counted from it; <see langword="null"/> for the whole delay.</param>
    /// <returns>The step.</returns>
    protected async ValueTask<SaslLoginStep> RefuseAsync(
        string? user, CancellationToken cancellationToken, string? refusalNote = null, DateTimeOffset? checkStartedAt = null)
    {
        var refused = RefusedNote(user);
        await Context.WaitRefusalDelayAsync(cancellationToken, checkStartedAt).ConfigureAwait(false);

        return new SaslLoginStep(SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, refused, refusalNote);
    }

    /// <summary>
    /// The login note for this mechanism: its registered name and <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The user as the note names it.</param>
    /// <param name="isAccepted">Whether the credentials matched.</param>
    /// <returns>The note.</returns>
    protected CheckedLogin Note(string? user, bool isAccepted) => new(Context.Mechanism, user, isAccepted);

    private async ValueTask<SaslLoginStep> TrackAsync(ValueTask<SaslLoginStep> answer, CancellationToken cancellationToken)
    {
        var step = await answer.ConfigureAwait(false);
        if (judgedLogin is { } judged)
        {
            judgedLogin = null;
            step = await ConcludeAsync(step, judged, cancellationToken).ConfigureAwait(false);
        }

        isAwaitingResponse = step.Outcome == SaslLoginOutcome.Challenge;

        return step;
    }

    // ADR-0124 decisions 1 and 4: an accepted login a refuse-login fault applies to is refused as a
    // wrong credential is, and every judged login is published.
    private async ValueTask<SaslLoginStep> ConcludeAsync(SaslLoginStep step, CheckedLogin judged, CancellationToken cancellationToken)
    {
        var policy = Context.Policy;
        if (!judged.IsAccepted
            || !await policy.IsRefusedByFaultAsync(Context.Scheme, judgedAccountName!, Context.Exchange, cancellationToken).ConfigureAwait(false))
        {
            policy.PublishJudgedLogin(Context.Scheme, judged.User, judged.Method, judged.IsAccepted, false, Context.Exchange);
            return step;
        }

        var refused = await RefuseByFaultAsync(judged.User, cancellationToken).ConfigureAwait(false);
        judgedLogin = null;
        policy.PublishJudgedLogin(Context.Scheme, judged.User, judged.Method, false, true, Context.Exchange);

        return refused;
    }
}
