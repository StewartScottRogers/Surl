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

    /// <summary>
    /// Waits, bounded as <see cref="Http3TestQuicClient.UntilAsync"/> is, until a note satisfying <paramref name="isWanted"/>
    /// is in <see cref="Log"/>, and fails with the notes seen when none comes. A note written by a stream or task
    /// other than the one a test waited for may land after the response, so a test reads it only through this.
    /// </summary>
    public async Task UntilNoteAsync(Func<string, bool> isWanted, CancellationToken cancellationToken)
    {
        try
        {
            await UntilAsync(() => Log.Notes.ToArray().Any(isWanted), cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Assert.Fail("The wanted note never appeared. Notes seen: " + string.Join(" | ", Log.Notes.ToArray()));
        }
    }

    /// <summary>Waits until <see cref="Log"/> holds exactly <paramref name="note"/>; see <see cref="UntilNoteAsync(Func{string, bool}, CancellationToken)"/>.</summary>
    public Task UntilNoteAsync(string note, CancellationToken cancellationToken) =>
        UntilNoteAsync(seen => seen == note, cancellationToken);

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

    /// <summary>The client-certificate trust check <see cref="Http3ProtocolServer"/> is given; null asks for no client certificate.</summary>
    public Func<IReadOnlyList<ReadOnlyMemory<byte>>, bool>? TrustsClientCertificateChain { get; set; }

    /// <summary>The idle timeout <see cref="Http3ProtocolServer"/> is given.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The fault plan the exchange asks, when a test sets one before starting the server.</summary>
    public IFaultPlan? Faults { get; set; }

    /// <summary>Where the server reports decrypted bytes; null leaves the context's discarding sink.</summary>
    public IDecryptedBytesSink? DecryptedBytes { get; set; }

    /// <summary>Starts the server with <paramref name="limits"/> and a content store holding <c>file.txt</c>.</summary>
    public Task Serve(CancellationToken cancellationToken, ExchangeLimits? limits = null, IContentFileSystem? fileSystem = null, bool allowUploads = false)
    {
        var context = HttpServerHarness.Context(Log, Time, Exchange.Token, limits);
        if (Faults is { } faults)
        {
            context = context with { Faults = faults };
        }
        if (DecryptedBytes is { } sink)
        {
            context = context with { DecryptedBytes = sink };
        }

        var exposure = ContentExposureOptions.ServeEverythingInsideTheRoot with { AllowUploads = allowUploads };
        var store = new ContentStore(HttpServerHarness.Root, fileSystem ?? HttpServerHarness.StandardFileSystem(), exposure);
        if (ProtocolServerPolicy is { } policy)
        {
            Clock = Time;
            Serving = new Http3ProtocolServer(store, policy, [SelfSignedCertificate()], Key, IdleTimeout, TrustsClientCertificateChain).ServeAsync(Flow, context);
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
