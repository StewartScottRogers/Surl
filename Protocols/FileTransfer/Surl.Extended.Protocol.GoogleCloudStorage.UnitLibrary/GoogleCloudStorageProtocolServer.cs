using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The <c>gs</c> server: answers Google Cloud Storage XML API requests over HTTP/1.1 on the shared
/// content store, whose top-level folders are the buckets (ADR-0110; ADR-0100, decision 2). It is
/// judged by the Cloud Storage XML API pages, not by upstream curl, which has no <c>gs</c> scheme
/// (ADR-0083).
/// </summary>
/// <remarks>
/// <para>
/// It serves List Buckets and List Objects (both versions) when the store lists directories, GET
/// Object and HEAD Object - ranges, conditions, generations, the object's stored header fields,
/// <c>x-goog-meta-*</c> and <c>x-goog-hash</c> - and PUT Object, DELETE Object and the XML API
/// multipart and resumable uploads when the store allows uploads (their open uploads shared by every
/// connection; ADR-0110, decision 9), path style or virtual-hosted style, and answers every other request
/// <c>400 NotImplemented</c>, each failure with the XML API's <c>Error</c> body. Every request must
/// carry a Bearer token or a <c>GOOG4-HMAC-SHA256</c> signature of an account, checked through the
/// policy's HTTP session (anonymous reads only when the policy allows them; ADR-0110, decision 3).
/// </para>
/// <para>
/// HTTP/1.1 framing, the head limit and timeouts are <see cref="Http1ConnectionServer"/>'s. As an
/// <see cref="IConnectionRefusalWriter"/>, the server answers a connection past a connection limit
/// <c>503 SlowDown</c> with the <c>Error</c> body, then closes it (ADR-0110, decision 7).
/// </para>
/// </remarks>
public sealed class GoogleCloudStorageProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>
    /// The scheme the server answers.
    /// </summary>
    public const string Scheme = "gs";

    private readonly ContentStore store;
    private readonly ObjectMetadataIndex index;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly TimeProvider timeProvider;
    private readonly GcsStagedUploads uploads;

    /// <summary>
    /// Creates the server.
    /// </summary>
    /// <param name="store">The shared content store; its top-level folders are the buckets.</param>
    /// <param name="index">The object metadata index of <paramref name="store"/>'s served root.</param>
    /// <param name="authenticationPolicy">The policy whose HTTP session checks each request's login.</param>
    /// <param name="timeProvider">The clock a connection refusal's <c>Date</c> is taken from.</param>
    public GoogleCloudStorageProtocolServer(ContentStore store, ObjectMetadataIndex index, IAuthenticationPolicy authenticationPolicy, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.store = store;
        this.index = index;
        this.authenticationPolicy = authenticationPolicy;
        this.timeProvider = timeProvider;
        uploads = new GcsStagedUploads(timeProvider);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [Scheme];

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new Http1ConnectionServer(new GcsRequestAnswerer(store, index, authenticationPolicy.StartHttpConnection(connection.TlsSession), context, uploads, connection.TlsSession is not null), context).ServeAsync(connection);
    }

    /// <inheritdoc/>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var body = GcsErrorBody.Render(GcsError.SlowDown);
        var head = new HttpResponseHead(GcsError.SlowDown.Status)
            .AddField("Date", timeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture))
            .AddField("Server", HttpResponseHead.ServerName)
            .AddField("Content-Type", GcsReply.XmlContentType)
            .AddField("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))
            .AddField("Connection", "close");

        await connection.WriteAsync(head.ToBytes(), cancellationToken);
        await connection.WriteAsync(body, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
