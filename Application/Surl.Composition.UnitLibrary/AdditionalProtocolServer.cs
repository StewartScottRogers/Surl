using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A protocol server a host adds beside the original and extended servers, such as a test's own
/// server <c>Surl.Api</c> registers (ADR-0124, decision 2), with the listen URLs it is served on.
/// The command line cannot name one; the host has parsed its listen URLs already.
/// </summary>
/// <param name="Registration">How the server is built and the schemes it answers.</param>
/// <param name="ListenUrls">The listen URLs it is served on, after the command line's own.</param>
public sealed record AdditionalProtocolServer(
    ProtocolServerRegistration Registration,
    IReadOnlyList<ListenUrl> ListenUrls);
