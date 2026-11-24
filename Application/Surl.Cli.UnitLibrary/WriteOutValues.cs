namespace Surl.Cli;

/// <summary>
/// What one exchange tells <c>-w</c> / <c>--write-out</c>, seen from the client's side as upstream
/// curl names it: <c>size_download</c> is what the client downloaded, so what surl sent.
/// </summary>
/// <param name="ExchangeId">The exchange's number on this instance.</param>
/// <param name="Scheme">The scheme the exchange was served over.</param>
/// <param name="ListenUrl">The listen URL that accepted it, bound port included.</param>
/// <param name="RemoteIp">The client's address, or empty when it has none (a Unix socket).</param>
/// <param name="RemotePort">The client's port, or 0 when it has none.</param>
/// <param name="SizeDownload">The bytes surl sent in the exchange.</param>
/// <param name="SizeUpload">The bytes surl received in the exchange.</param>
/// <param name="TimeTotal">How long the exchange lasted.</param>
public sealed record WriteOutValues(
    long ExchangeId,
    string Scheme,
    string ListenUrl,
    string RemoteIp,
    int RemotePort,
    long SizeDownload,
    long SizeUpload,
    TimeSpan TimeTotal);
