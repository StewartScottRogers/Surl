using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// <c>CONNECT</c> (ADR-0088 decision 4): the authority-form target, the origin connection,
/// the exact <c>200</c> reply and the tunnel.
/// </summary>
internal sealed partial class HttpProxyExchange
{
    private async Task<bool> AnswerConnectAsync(HttpRequestHead head)
    {
        var target = ProxyTarget.FromAuthorityForm(head.RequestTarget);
        if (target is null)
        {
            return await AnswerAsync(HttpStatus.BadRequest, "the CONNECT target is not host:port", closes: true);
        }

        await DropOriginAsync();
        OriginLink link;
        try
        {
            link = await ConnectAsync(target);
        }
        catch (OriginConnectionException exception)
        {
            return await AnswerOriginFailureAsync(exception.Failure, target, RequestEndsConnection(head));
        }

        await using (link)
        {
            context.Log.Note($"Tunnelling to {target.Host}:{target.Port}; answered 200.");
            await client.WriteAsync(HttpProxyStatus.ConnectionEstablished, Cancellation);
            var closedCleanly = await ProxyTunnel.RelayAsync(client, reader, link, Cancellation);
            context.Log.Note(closedCleanly
                ? $"The tunnel to {target.Host}:{target.Port} closed."
                : $"The tunnel to {target.Host}:{target.Port} failed; both connections were aborted.");
        }

        return false;
    }

    private Task<bool> AnswerOriginFailureAsync(OriginConnectionFailure failure, ProxyTarget target, bool closes)
    {
        var (status, logLine) = HttpProxyStatus.ForOriginFailure(failure, target.Host, target.Port);
        context.Log.Note(logLine);

        return AnswerAsync(status, $"the origin {target.Host}:{target.Port} could not be used ({failure})", closes);
    }
}
