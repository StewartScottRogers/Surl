using Surl.Http2;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The tunnel of a <c>CONNECT</c> stream on an HTTP/2 connection to the proxy (RFC 9113
/// section 8.5; ADR-0088 decision 6 as Amendment 5 amends it): curl's <c>DATA</c> is written to
/// the origin, and the origin's bytes go back as <c>DATA</c>, unchanged and unread.
/// </summary>
/// <remarks>
/// When curl ends its half of the stream (<c>END_STREAM</c>) the origin's connection is
/// half-closed; when the origin stops sending, an empty <c>DATA</c> with <c>END_STREAM</c> ends
/// the proxy's half. When the origin's connection fails, the stream is reset with
/// <c>CONNECT_ERROR</c> and the origin's connection aborted, once, whichever direction saw it;
/// when curl resets the stream, the origin's connection is aborted.
/// </remarks>
internal sealed class Http2ProxyTunnel
{
    private const int RelayBufferBytes = 16384;

    private readonly Http2RequestStream stream;
    private readonly IConnection origin;
    private readonly CancellationToken cancellationToken;
    private int hasFailed;

    private Http2ProxyTunnel(Http2RequestStream stream, IConnection origin, CancellationToken cancellationToken)
    {
        this.stream = stream;
        this.origin = origin;
        this.cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Relays until both sides have stopped sending, or either fails.
    /// </summary>
    /// <param name="stream">curl's <c>CONNECT</c> stream, its <c>:status 200</c> already written.</param>
    /// <param name="origin">The origin's connection.</param>
    /// <param name="cancellationToken">The stream's cancellation: cut off when curl resets it or the connection ends.</param>
    /// <returns><see langword="true"/> when both sides closed cleanly; <see langword="false"/> when one failed.</returns>
    public static async Task<bool> RelayAsync(Http2RequestStream stream, IConnection origin, CancellationToken cancellationToken)
    {
        var tunnel = new Http2ProxyTunnel(stream, origin, cancellationToken);
        var relayed = await Task.WhenAll(tunnel.RelayToOriginAsync(), tunnel.RelayToCurlAsync());

        return relayed[0] && relayed[1];
    }

    private async Task<bool> RelayToOriginAsync()
    {
        await Task.Yield();
        try
        {
            while (await stream.ReadBodyAsync(cancellationToken) is { } piece)
            {
                await origin.WriteAsync(piece, cancellationToken);
            }

            if (stream.HasRequestBodyEndedEarly)
            {
                origin.Abort();

                return false;
            }

            await origin.CompleteWritesAsync(cancellationToken);

            return true;
        }
        catch (IOException)
        {
            return await FailAsync();
        }
    }

    private async Task<bool> RelayToCurlAsync()
    {
        await Task.Yield();
        var buffer = new byte[RelayBufferBytes];
        try
        {
            int read;
            while ((read = await origin.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await stream.WriteDataAsync(buffer.AsMemory(0, read), endStream: false);
            }

            await stream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream: true);

            return true;
        }
        catch (IOException)
        {
            return await FailAsync();
        }
    }

    // The first failure aborts the origin and resets the stream; a later one, the echo of the
    // first in the other direction, only reports it.
    private async Task<bool> FailAsync()
    {
        if (Interlocked.Exchange(ref hasFailed, 1) == 0)
        {
            origin.Abort();
            await stream.ResetAsync(Http2ErrorCode.ConnectError, "the tunnel's origin connection failed");
        }

        return false;
    }
}
