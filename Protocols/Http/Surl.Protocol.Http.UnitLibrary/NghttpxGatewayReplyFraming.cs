namespace Surl.Protocol.Http;

/// <summary>How much of a backend's HTTP/1 reply has arrived, by the reply's own framing (ADR-0154 decision 3, "The end of the stream").</summary>
public enum NghttpxGatewayReplyFraming
{
    /// <summary>No complete reply head yet.</summary>
    NoHead,

    /// <summary>A head with a body that ends only when the backend closes.</summary>
    ReadToClose,

    /// <summary>A head whose <c>Content-Length</c> or chunks say more body is still to come.</summary>
    Short,

    /// <summary>A head and all the body its framing announces.</summary>
    Whole,
}
