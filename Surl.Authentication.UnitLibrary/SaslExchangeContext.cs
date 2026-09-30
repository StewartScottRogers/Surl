namespace Surl.Authentication;

/// <summary>
/// What a <see cref="SaslMechanismExchange"/> needs from the policy that started it and from
/// the client's start.
/// </summary>
/// <param name="Policy">The policy: its accounts, <c>--allow-anonymous</c> and refusal delay.</param>
/// <param name="Mechanism">The mechanism's registered name, upper case: the login note's method.</param>
/// <param name="InitialResponse">The decoded initial response, or <see langword="null"/> when none was sent.</param>
internal sealed record SaslExchangeContext(
    AuthenticationPolicy Policy,
    string Mechanism,
    ReadOnlyMemory<byte>? InitialResponse)
{
    /// <summary>
    /// The configured accounts.
    /// </summary>
    public AccountBook Accounts => Policy.Settings.Accounts;

    /// <summary>
    /// <c>--allow-anonymous</c>: every step runs, and the login ends accepted unchecked.
    /// </summary>
    public bool IsUnchecked => Policy.Settings.AllowAnonymous;

    /// <summary>
    /// Waits the refusal delay on the policy's clock (ADR-0032, section 8).
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The wait.</returns>
    public Task WaitRefusalDelayAsync(CancellationToken cancellationToken) =>
        Policy.WaitRefusalDelayAsync(cancellationToken);
}
