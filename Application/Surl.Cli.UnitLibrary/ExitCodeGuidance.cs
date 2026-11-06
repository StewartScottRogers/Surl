using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>What one exit code means and what an agent should do next (ADR-0046 decision 6).</summary>
/// <param name="Code">The exit code; its number and name are the member's.</param>
/// <param name="Meaning">When surl returns it, as <c>--manual</c>'s <c>EXIT CODES</c> says.</param>
/// <param name="NextStep">What an agent that got it should do next.</param>
/// <param name="Topics">The <c>--aihelp</c> topics whose <c>Exit codes</c> section lists it.</param>
internal sealed record ExitCodeGuidance(SurlExitCode Code, string Meaning, string NextStep, IReadOnlyList<string> Topics);
