using Surl.Cli;

namespace Surl.Mcp;

/// <summary>One <see cref="SurlCommandLine"/> member that holds local paths.</summary>
/// <param name="MemberName">The member's name.</param>
/// <param name="OptionName">The option that sets it, as a refusal names it (<c>--cert</c>).</param>
/// <param name="ReadPaths">Reads every path the member holds.</param>
internal sealed record CommandLinePathMember(string MemberName, string OptionName, Func<SurlCommandLine, IEnumerable<string>> ReadPaths);
