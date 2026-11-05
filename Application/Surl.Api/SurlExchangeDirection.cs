namespace Surl.Api;

/// <summary>Which way a <see cref="SurlExchangeChunk"/>'s bytes crossed the connection (ADR-0099, decision 7).</summary>
public enum SurlExchangeDirection
{
    /// <summary>From the client to the server.</summary>
    Received,

    /// <summary>From the server to the client.</summary>
    Sent,
}
