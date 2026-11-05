namespace Surl.McpServing;

/// <summary>A resource as its registry holds it: the resource and the provider that reads it.</summary>
/// <param name="Resource">The resource.</param>
/// <param name="Provider">The provider that listed it.</param>
internal sealed record McpRegisteredResource(McpResource Resource, IMcpResourceProvider Provider);
