namespace Surl.McpServing;

/// <summary>
/// A hand-written <see cref="IMcpResourceProvider" /> for tests: lists the resources it is given and reads each as
/// <c>text of &lt;uri&gt;</c> unless <see cref="ReadText" /> says otherwise.
/// </summary>
internal sealed class UnitTestResourceProvider(params McpResource[] resources) : IMcpResourceProvider
{
    public int ResourcesReadCount { get; private set; }

    public Func<string, CancellationToken, Task<string?>> ReadText { get; init; } =
        (uri, _) => Task.FromResult<string?>($"text of {uri}");

    public IReadOnlyList<McpResource> Resources
    {
        get
        {
            ResourcesReadCount++;
            return resources;
        }
    }

    public Task<string?> ReadTextAsync(string uri, CancellationToken cancellationToken) => ReadText(uri, cancellationToken);
}
