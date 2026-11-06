namespace Surl.Protocol.HttpProxy;

/// <summary>
/// An FTP origin failed the proxy's fetch before its <c>200</c> was written (ADR-0088 Amendment
/// 4): curl is answered <c>504</c> when it did not answer in time, otherwise <c>502</c>.
/// </summary>
/// <param name="timedOut">Whether the origin did not answer in time.</param>
/// <param name="message">What the origin did, without any credential.</param>
internal sealed class FtpOriginException(bool timedOut, string message) : Exception(message)
{
    /// <summary>Whether the origin did not answer in time.</summary>
    public bool TimedOut { get; } = timedOut;
}
