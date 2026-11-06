using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The WebTransport server on one QUIC connection (ADR-0117 decisions 0 to 3 and 6): the
/// <see cref="IQuicApplicationProtocol"/> a <see cref="QuicDatagramFlowServer"/> hands each
/// connection to once its handshake has agreed <c>h3</c>. It serves HTTP/3 with an
/// <see cref="Http3ConnectionServer"/> that registers <c>webtransport-h3</c> (with stream signal
/// <c>0x41</c> and stream type <c>0x54</c>) and sends <c>SETTINGS_WT_ENABLED</c> = 1; every request
/// that is not a WebTransport <c>CONNECT</c> is answered <c>405</c>.
/// </summary>
/// <remarks>
/// The server composing it sets <c>QuicServerConnectionOptions.MaxDatagramFrameSize</c> to
/// <see cref="MaxDatagramFrameSize"/> and <c>ResetStreamAt</c>, which WTH3 3.1 requires.
/// </remarks>
/// <param name="acceptedPath">The listen URL's path: it and every path below it at a <c>/</c> accept a session; empty or <c>/</c> accepts every path.</param>
/// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
public sealed class WebTransportApplicationProtocol(string acceptedPath, ExchangeContext context) : IQuicApplicationProtocol
{
    /// <summary>The <c>max_datagram_frame_size</c> transport parameter the server sends (ADR-0117 decision 0; RFC 9221 section 3).</summary>
    public const ulong MaxDatagramFrameSize = 65535;

    private readonly string acceptedPath = acceptedPath ?? throw new ArgumentNullException(nameof(acceptedPath));
    private readonly ExchangeContext context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var directory = new WebTransportSessionDirectory(context);
        var options = new Http3ServerOptions
        {
            ExtendedConnectHandlers = [new WebTransportConnectHandler(acceptedPath, directory, context)],
            FurtherSettings = [new(WebTransportHttp3Codes.EnabledSetting, 1)],
        };
        return new Http3ConnectionServer(new WebTransportRequestStreamHandler(directory), options, context).ServeAsync(connection, cancellationToken);
    }
}
