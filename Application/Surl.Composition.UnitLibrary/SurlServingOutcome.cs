using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// How a <see cref="ComposedServing"/> ended (ADR-0099, decision 2).
/// </summary>
/// <param name="ExitCode">The exit code serving ended with.</param>
/// <param name="Failure">
/// A listener that could not bind (<c>(45)</c> or <c>(6)</c>) or serving that failed
/// (<c>(125)</c>), with its line after <c>surl: </c>; <see langword="null"/> otherwise.
/// </param>
public sealed record SurlServingOutcome(SurlExitCode ExitCode, SurlStartFailure? Failure);
