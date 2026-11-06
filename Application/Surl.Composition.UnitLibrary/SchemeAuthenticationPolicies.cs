using System.Collections.Concurrent;
using Surl.Authentication;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The authentication policy each server is composed with: for a scheme, the policy over the view of
/// the accounts allowed on it (ADR-0102, decision 4.3, and its amendment of 2026-11-06;
/// <see cref="AuthenticationSettings.ForScheme"/>), so an account whose <c>protocols=</c> leaves the
/// scheme out is refused there exactly as no such account. The view reads the start's one changing
/// account book (ADR-0132), so an account added while the server runs is judged through it at once;
/// while no account is restricted, every view admits every account, as the one policy does.
/// </summary>
public sealed class SchemeAuthenticationPolicies
{
    private readonly AuthenticationPolicy policy;
    private readonly TimeProvider timeProvider;
    private readonly ISurlEventPublisher? events;
    private readonly IFaultPlan? faults;
    private readonly ConcurrentDictionary<string, AuthenticationPolicy> byScheme = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates the policies over <paramref name="policy"/>'s settings.
    /// </summary>
    /// <param name="policy">The start's one policy, over every account.</param>
    /// <param name="timeProvider">The clock a scheme's own policy runs on.</param>
    /// <param name="events">The event stream a scheme's own policy publishes judged logins on; none when <see langword="null"/>.</param>
    /// <param name="faults">The fault plan a scheme's own policy consults; none when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public SchemeAuthenticationPolicies(
        AuthenticationPolicy policy, TimeProvider timeProvider, ISurlEventPublisher? events = null, IFaultPlan? faults = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.policy = policy;
        this.timeProvider = timeProvider;
        this.events = events;
        this.faults = faults;
    }

    /// <summary>
    /// The policy a server answering <paramref name="scheme"/> judges logins by: composed once per
    /// scheme, compared without regard to case, and the same instance on every later call.
    /// </summary>
    /// <param name="scheme">The scheme the server answers.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scheme"/> is <see langword="null"/>.</exception>
    public AuthenticationPolicy For(string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return byScheme.GetOrAdd(
            scheme,
            static (named, policies) => AuthenticationComposition.ComposePolicy(
                policies.policy.Settings.ForScheme(named), policies.timeProvider, policies.events, policies.faults),
            this);
    }
}
