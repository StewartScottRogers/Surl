namespace Surl.Composition;

/// <summary>
/// What <see cref="SurlServerComposition.ComposeAsync"/> came to: exactly one of a refused start
/// and a composed serving is set (ADR-0099, decision 2).
/// </summary>
/// <param name="Failure">Why the start was refused, or <see langword="null"/> when it was composed.</param>
/// <param name="Serving">What serves until cancelled, or <see langword="null"/> when the start was refused.</param>
public sealed record SurlServerStart(SurlStartFailure? Failure, ComposedServing? Serving);
