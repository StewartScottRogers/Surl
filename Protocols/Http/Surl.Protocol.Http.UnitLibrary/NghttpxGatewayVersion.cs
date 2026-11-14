namespace Surl.Protocol.Http;

/// <summary>
/// Which frontend of upstream's nghttpx gateway a request arrived on or a reply goes back to
/// (ADR-0154). The two differ in the request line form and in whether <c>server: nghttpx</c> is added.
/// </summary>
public enum NghttpxGatewayVersion
{
    /// <summary>HTTP/2: origin-form request line, and the reply gains <c>server: nghttpx</c>.</summary>
    Http2,

    /// <summary>HTTP/3: absolute-form request line, and the reply's <c>Server</c> is left alone.</summary>
    Http3,
}
