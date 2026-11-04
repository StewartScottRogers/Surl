namespace Surl.Cli;

/// <summary>One help category <c>surl --help &lt;category&gt;</c> answers (ADR-0034 decision 1).</summary>
/// <param name="Name">The name given after <c>--help</c> (<c>tls</c>).</param>
/// <param name="Description">What the category holds, as <c>--help category</c> lists it.</param>
/// <param name="Schemes">
/// The listen-URL schemes a protocol category covers (<c>http</c> and <c>https</c>), empty for
/// every other category (ADR-0046 decision 6). <c>--help</c> does not print them.
/// </param>
internal sealed record HelpCategory(string Name, string Description, IReadOnlyList<string> Schemes);
