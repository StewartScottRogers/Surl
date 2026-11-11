using System.Security.Cryptography;
using Surl.Content;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Protocol.Http;

/// <summary>
/// The <see cref="Http3TestQuicClient"/> the HTTP server's HTTP/3 tests drive: the server is
/// <see cref="Http3ConnectionServer"/> with <see cref="Http3StreamAnswerWriter"/> over
/// <see cref="HttpRequestAnswerer"/>, ridden by a <see cref="QuicDatagramFlowServer"/>, on a
/// <see cref="ManualTimeProvider"/> that stands still unless a test moves it.
/// </summary>
internal sealed class Http3TestClient(Func<byte[], byte[]>? editClientHello = null) : Http3TestQuicClient(editClientHello)
{
    public ManualTimeProvider Time { get; } = new(HttpServerHarness.Now);

    public RecordingExchangeLog Log { get; } = new();

    /// <summary>Cancels the exchange, as shutdown or <c>--max-time</c> does.</summary>
    public CancellationTokenSource Exchange { get; } = new();

    /// <summary>A handler that answers in place of <see cref="HttpRequestAnswerer"/>, when a test sets one before starting the server.</summary>
    public IHttpRequestHandler? Handler { get; set; }

    /// <summary>
    /// When a test sets it before starting the server, the server is <see cref="Http3ProtocolServer"/>
    /// with this policy and <see cref="IdleTimeout"/>, as <c>surl --http3</c> composes it, in place of
    /// a hand-built <see cref="QuicDatagramFlowServer"/>.
    /// </summary>
    public IAuthenticationPolicy? ProtocolServerPolicy { get; set; }

    /// <summary>The idle timeout <see cref="Http3ProtocolServer"/> is given.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Starts the server with <paramref name="limits"/> and a content store holding <c>file.txt</c>.</summary>
    public Task Serve(CancellationToken cancellationToken, ExchangeLimits? limits = null, IContentFileSystem? fileSystem = null, bool allowUploads = false)
    {
        var context = HttpServerHarness.Context(Log, Time, Exchange.Token, limits);
        var exposure = ContentExposureOptions.ServeEverythingInsideTheRoot with { AllowUploads = allowUploads };
        var store = new ContentStore(HttpServerHarness.Root, fileSystem ?? HttpServerHarness.StandardFileSystem(), exposure);
        if (ProtocolServerPolicy is { } policy)
        {
            Clock = Time;
            Serving = new Http3ProtocolServer(store, policy, [SelfSignedCertificate()], Key, IdleTimeout).ServeAsync(Flow, context);
            return Serving;
        }

        var answerer = Handler ?? new HttpRequestAnswerer(context, store, new AnonymousAuthenticationPolicy().StartHttpConnection(null));
        var options = new Http3ServerOptions { ServerFieldValue = HttpResponseHead.ServerName };
        return Serve(new Http3ConnectionServer(new Http3StreamAnswerWriter(answerer, context), options, context), Time, cancellationToken);
    }

    /// <summary>Starts the server, completes the handshake, and opens the client's control stream with <paramref name="settings"/> and its two QPACK streams.</summary>
    /// <remarks>
    /// The server grants three unidirectional streams (ADR-0082 decision 4), so a test that opens
    /// another kind of stream passes <paramref name="openQpackStreams"/> false and uses stream 6 or 10.
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken, ExchangeLimits? limits = null, IContentFileSystem? fileSystem = null, bool allowUploads = false, byte[]? settings = null, bool openQpackStreams = true)
    {
        _ = Serve(cancellationToken, limits, fileSystem, allowUploads);
        await OpenAsync(cancellationToken, settings, openQpackStreams);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Exchange.Dispose();
        }

        base.Dispose(disposing);
    }
}
