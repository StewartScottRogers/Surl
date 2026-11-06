using Surl.Cli;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// Serves what <c>surl --aihelp</c> prints as MCP resources (ADR-0103 decision 6): <c>surl://aihelp</c> for the
/// overview, <c>surl://aihelp/&lt;topic&gt;</c> for each entry of <see cref="AiHelpTopics.All"/> in its order, and
/// <c>surl://aihelp/all</c>, each <c>text/markdown</c> and read from <see cref="AiHelpText.Answer"/>, never copied.
/// </summary>
public sealed class AiHelpResourceProvider : IMcpResourceProvider
{
    /// <summary>The URI of the overview; each topic's URI is this, a slash and the topic's name.</summary>
    public const string OverviewUri = "surl://aihelp";

    /// <summary>The media type every resource has.</summary>
    public const string MimeType = "text/markdown";

    private const string AllTopic = "all";

    private readonly Dictionary<string, string?> _topicByUri = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="AiHelpResourceProvider"/> class.</summary>
    public AiHelpResourceProvider()
    {
        List<McpResource> resources = [Describe(null, "Overview and topic list", "What surl is, its command line, its conventions and the topics.")];
        resources.AddRange(AiHelpTopics.All.Select(topic => Describe(topic.Name, topic.Description, $"The {topic.Name} topic of surl --aihelp.")));
        resources.Add(Describe(AllTopic, "The overview and every topic", "The overview, then every topic page, in one document."));
        Resources = resources;
    }

    /// <inheritdoc/>
    public IReadOnlyList<McpResource> Resources { get; }

    /// <summary>Gets the URI of a topic's resource.</summary>
    /// <param name="topic">The topic's name, or <see langword="null"/> for the overview.</param>
    /// <returns><c>surl://aihelp</c> for the overview, otherwise <c>surl://aihelp/&lt;topic&gt;</c>.</returns>
    public static string UriOf(string? topic) => topic is null ? OverviewUri : $"{OverviewUri}/{topic}";

    /// <inheritdoc/>
    public Task<string?> ReadTextAsync(string uri, CancellationToken cancellationToken) =>
        Task.FromResult(_topicByUri.TryGetValue(uri, out var topic) ? AiHelpText.Answer(topic).Output : null);

    private McpResource Describe(string? topic, string title, string description)
    {
        var uri = UriOf(topic);
        _topicByUri.Add(uri, topic);
        var name = topic is null ? "surl --aihelp" : $"surl --aihelp {topic}";
        return new McpResource(uri, name, title, description, MimeType);
    }
}
