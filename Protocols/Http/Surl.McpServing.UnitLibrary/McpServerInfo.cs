namespace Surl.McpServing;

/// <summary>The <c>serverInfo</c> an <c>initialize</c> result carries, given by the server's host.</summary>
/// <param name="Name">The server's programmatic name.</param>
/// <param name="Title">The server's display name, or <see langword="null" /> to write none.</param>
/// <param name="Version">The server's version.</param>
public sealed record McpServerInfo(string Name, string? Title, string Version);
