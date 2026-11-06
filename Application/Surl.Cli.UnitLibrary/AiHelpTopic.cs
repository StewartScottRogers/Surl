namespace Surl.Cli;

/// <summary>One topic <c>surl --aihelp &lt;topic&gt;</c> answers (ADR-0046 decision 3).</summary>
/// <param name="Name">The name given after <c>--aihelp</c> (<c>mqtt</c>).</param>
/// <param name="Description">What the topic covers: its page title and its row in the topic table.</param>
/// <param name="Schemes">The listen-URL schemes a protocol topic covers; empty for every other topic.</param>
public sealed record AiHelpTopic(string Name, string Description, IReadOnlyList<string> Schemes);
