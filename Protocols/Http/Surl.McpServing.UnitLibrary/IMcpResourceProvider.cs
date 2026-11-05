namespace Surl.McpServing;

/// <summary>
/// Serves a fixed set of text resources, added to an <see cref="McpServerRegistry" /> with one explicit
/// <see cref="McpServerRegistry.AddResources" /> call (ADR-0103 decision 1, the port's ADR-0801 decision 4 adopted).
/// </summary>
public interface IMcpResourceProvider
{
    /// <summary>Gets the resources the provider serves; read once, when the provider is added.</summary>
    IReadOnlyList<McpResource> Resources { get; }

    /// <summary>Reads one of the provider's resources as text.</summary>
    /// <param name="uri">The URI of one of <see cref="Resources" />.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The resource's text, or <see langword="null" /> when the resource does not exist.</returns>
    Task<string?> ReadTextAsync(string uri, CancellationToken cancellationToken);
}
