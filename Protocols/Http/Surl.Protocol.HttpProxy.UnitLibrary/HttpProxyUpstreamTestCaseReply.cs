namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The reply the proxy server half chose for one request of an upstream test case.
/// </summary>
/// <param name="PartName">The part chosen: <c>data</c>, <c>data2</c>, <c>connect</c>, <c>connect1001</c> ...</param>
/// <param name="Bytes">The part's bytes, sent verbatim; <see langword="null"/> when the case has no such part.</param>
/// <param name="ClosesConnection">Whether the connection closes after it: <c>swsclose</c> is in the part.</param>
internal sealed record HttpProxyUpstreamTestCaseReply(string PartName, byte[]? Bytes, bool ClosesConnection);
