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
    // ADR-0179: the schemes, the plaintext one of each TLS twin, that offer SCRAM when --auth is not given.
    private static readonly HashSet<string> ScramDefaultSchemes = new(StringComparer.OrdinalIgnoreCase) { "kafka", "xmpp" };

    private readonly bool authenticationMethodsGiven;
    private readonly ConcurrentDictionary<string, AuthenticationPolicy> byScheme = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates the policies over <paramref name="policy"/>'s settings.
    /// </summary>
    /// <param name="policy">The start's one policy, over every account.</param>
    /// <param name="timeProvider">The clock a scheme's own policy runs on.</param>
    /// <param name="events">The event stream a scheme's own policy publishes judged logins on; none when <see langword="null"/>.</param>
    /// <param name="faults">The fault plan a scheme's own policy consults; none when <see langword="null"/>.</param>
    /// <param name="authenticationMethodsGiven">Whether <c>--auth</c> was given; when it was, its set applies to every server as it is, and the SCRAM default of <c>kafka</c> and <c>xmpp</c> (ADR-0179) is not added.</param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public SchemeAuthenticationPolicies(
        AuthenticationPolicy policy,
        TimeProvider timeProvider,
        ISurlEventPublisher? events = null,
        IFaultPlan? faults = null,
        bool authenticationMethodsGiven = false)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.policy = policy;
        this.timeProvider = timeProvider;
        this.events = events;
        this.faults = faults;
        this.authenticationMethodsGiven = authenticationMethodsGiven;
    }

    private static AuthenticationSettings ForSchemeWithScramDefault(
        AuthenticationSettings settings, string scheme, bool authenticationMethodsGiven)
    {
        var view = settings.ForScheme(scheme);
        return authenticationMethodsGiven || !ScramDefaultSchemes.Contains(scheme)
            ? view
            : view with
            {
                AcceptedMethods = new HashSet<AuthenticationMethod>(view.AcceptedMethods)
                {
                    AuthenticationMethod.ScramSha256,
                    AuthenticationMethod.ScramSha512,
                    AuthenticationMethod.ScramSha1,
                },
            };
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
                ForSchemeWithScramDefault(policies.policy.Settings, named, policies.authenticationMethodsGiven),
                policies.timeProvider, policies.events, policies.faults),
            this);
    }
}
