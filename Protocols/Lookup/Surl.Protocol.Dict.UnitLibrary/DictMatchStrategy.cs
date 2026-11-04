namespace Surl.Protocol.Dict;

/// <summary>
/// A DICT match strategy (RFC 2229, section 3.3): how <c>MATCH</c> compares a headword with
/// the word asked for. Surl offers <c>exact</c> and <c>prefix</c>, and the server's default,
/// <c>.</c>, is <c>prefix</c>.
/// </summary>
internal sealed class DictMatchStrategy
{
    private readonly bool matchesPrefix;

    private DictMatchStrategy(string name, string description, bool matchesPrefix)
    {
        Name = name;
        Description = description;
        this.matchesPrefix = matchesPrefix;
    }

    /// <summary>
    /// A headword matches when it is the word, compared ordinally.
    /// </summary>
    public static DictMatchStrategy Exact { get; } = new("exact", "Match headwords exactly", matchesPrefix: false);

    /// <summary>
    /// A headword matches when it starts with the word, compared ordinally.
    /// </summary>
    public static DictMatchStrategy Prefix { get; } = new("prefix", "Match prefixes", matchesPrefix: true);

    /// <summary>
    /// Every strategy offered, in the order <c>SHOW STRAT</c> lists them.
    /// </summary>
    public static IReadOnlyList<DictMatchStrategy> All { get; } = [Exact, Prefix];

    /// <summary>
    /// The strategy's name, as <c>MATCH</c> and <c>SHOW STRAT</c> spell it.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The one-line description <c>SHOW STRAT</c> gives.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Finds the strategy a <c>MATCH</c> command names.
    /// </summary>
    /// <param name="name">The strategy name sent: <c>.</c> for the server's default, or a strategy's name.</param>
    /// <returns>The strategy, or <see langword="null"/> when none has that name.</returns>
    public static DictMatchStrategy? Find(string name) =>
        name == "." ? Prefix : All.FirstOrDefault(strategy => strategy.Name == name);

    /// <summary>
    /// Whether <paramref name="headword"/> matches <paramref name="word"/> under this strategy.
    /// </summary>
    /// <param name="headword">A headword in the database.</param>
    /// <param name="word">The word asked for.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public bool Matches(string headword, string word) =>
        matchesPrefix
            ? headword.StartsWith(word, StringComparison.Ordinal)
            : string.Equals(headword, word, StringComparison.Ordinal);
}
