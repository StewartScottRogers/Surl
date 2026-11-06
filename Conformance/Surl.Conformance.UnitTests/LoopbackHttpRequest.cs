namespace Surl.Conformance;

/// <summary>One request a <see cref="LoopbackHttpResponder"/> recorded.</summary>
/// <param name="ClientPort">The port the request's connection came from.</param>
/// <param name="Head">The request line and header fields, ending with the blank line.</param>
internal sealed record LoopbackHttpRequest(int ClientPort, string Head);
