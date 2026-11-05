namespace Surl.MailStore;

/// <summary>
/// Thrown by <see cref="MailboxStore"/>'s constructor and <see cref="MailboxStore.LoadAsync"/>
/// when a configured alias or catch-all cannot be resolved (ADR-0102, decision 1.3); the
/// composition root answers it with exit 2 and the text the ADR gives for
/// <see cref="Problem"/>.
/// </summary>
public sealed class MailRouteException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="routeKind">The kind of route at fault.</param>
    /// <param name="problem">What is wrong with it.</param>
    /// <param name="address">The address the refusal names: the target for a target problem,
    /// the route's own address (a catch-all's target) for one that includes itself.</param>
    public MailRouteException(MailRouteKind routeKind, MailRouteProblem problem, string address)
        : base($"{routeKind} {problem}: {address}")
    {
        RouteKind = routeKind;
        Problem = problem;
        Address = address;
    }

    /// <summary>
    /// The kind of route at fault.
    /// </summary>
    public MailRouteKind RouteKind { get; }

    /// <summary>
    /// What is wrong with it.
    /// </summary>
    public MailRouteProblem Problem { get; }

    /// <summary>
    /// The address the refusal names.
    /// </summary>
    public string Address { get; }
}
