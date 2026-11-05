namespace Surl.McpServing;

/// <summary>The MCP revisions the core speaks and how it negotiates one (ADR-0103 decision 1).</summary>
public static class McpProtocolVersions
{
    /// <summary>The latest revision, answered to a client that asks for one the core does not speak.</summary>
    public const string Latest = "2025-06-18";

    /// <summary>Gets the revisions the core speaks, newest first.</summary>
    public static IReadOnlyList<string> Supported { get; } = [Latest, "2025-03-26", "2024-11-05"];

    /// <summary>
    /// Chooses the revision an <c>initialize</c> result answers: the client's when the core speaks it,
    /// otherwise <see cref="Latest" />, and the client decides whether to disconnect.
    /// </summary>
    /// <param name="requested">The <c>protocolVersion</c> the client asked for.</param>
    /// <returns>The revision to answer.</returns>
    public static string Negotiate(string requested) =>
        Supported.Contains(requested, StringComparer.Ordinal) ? requested : Latest;
}
