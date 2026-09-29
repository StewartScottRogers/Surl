using System.Diagnostics.CodeAnalysis;

namespace Surl.Cli;

/// <summary>
/// ADR-0046 decision 3's <c>--aihelp</c> topics: every help category, with its name,
/// description and schemes, plus <c>exit-codes</c> and <c>listen-urls</c>, in ordinal order of
/// the name.
/// </summary>
public static class AiHelpTopics
{
    private static readonly AiHelpTopic[] TopicsAddedBesideTheCategories =
    [
        new("exit-codes", "Exit codes and what to do next", []),
        new("listen-urls", "Listen URLs, ports and the Listening on line", []),
    ];

    private static readonly AiHelpTopic[] Table =
    [
        .. HelpCategories.All
            .Select(category => new AiHelpTopic(category.Name, category.Description, category.Schemes))
            .Concat(TopicsAddedBesideTheCategories)
            .OrderBy(topic => topic.Name, StringComparer.Ordinal),
    ];

    /// <summary>Every topic, in ordinal order of its name.</summary>
    public static IReadOnlyList<AiHelpTopic> All => Table;

    /// <summary>Finds a topic by its name, in any case, as <c>--help</c> matches its categories.</summary>
    /// <param name="name">The name as given (<c>MQTT</c>).</param>
    /// <param name="topic">The topic, when found.</param>
    /// <returns><see langword="true"/> when a topic has the name.</returns>
    public static bool TryFind(string name, [NotNullWhen(true)] out AiHelpTopic? topic)
    {
        topic = Array.Find(Table, candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return topic is not null;
    }
}
