namespace Surl.McpServing;

/// <summary>
/// The capabilities an <c>initialize</c> result advertises (ADR-0103 decision 1): <c>tools</c> as
/// <c>{"listChanged":false}</c> and <c>resources</c> as <c>{"subscribe":false,"listChanged":false}</c>, each
/// only when the server serves it. No <c>logging</c>, <c>prompts</c> or <c>completions</c> is advertised.
/// </summary>
/// <param name="Tools">Whether the server serves tools.</param>
/// <param name="Resources">Whether the server serves resources.</param>
public sealed record McpServerCapabilities(bool Tools, bool Resources);
