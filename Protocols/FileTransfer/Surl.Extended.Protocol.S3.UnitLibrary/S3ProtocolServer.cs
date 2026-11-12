using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The <c>s3</c> server: answers Amazon S3 REST requests over HTTP/1.1 on the shared content
/// store, whose top-level folders are the buckets (ADR-0108; ADR-0100, decision 2). It is judged
/// by the Amazon S3 API Reference, not by upstream curl, which has no <c>s3</c> scheme (ADR-0083).
/// </summary>
/// <remarks>
/// <para>
/// It serves GetObject and HeadObject - ranges, conditional fields, the object's stored header
/// fields and user metadata - PutObject, DeleteObject and the multipart uploads when the store allows
/// uploads, and ListBuckets and ListObjectsV2 when it lists directories, path style or virtual-hosted style, and answers every other request
/// <c>501 NotImplemented</c>, each failure with the S3 <c>Error</c> body. Every request must carry
/// a valid AWS Signature Version 4 <c>Authorization</c> of an account (anonymous reads only under
/// <c>--allow-anonymous</c>), checked through the policy's HTTP session (ADR-0108, decision 4).
/// </para>
/// <para>
/// HTTP/1.1 framing, the head limit and timeouts are <see cref="Http1ConnectionServer"/>'s; a
/// head past <c>--max-request-head</c> is refused <c>400 RequestHeaderSectionTooLarge</c> with the
/// <c>Error</c> body, as the request answerer words it, and closed (ADR-0108, decision 7). As an <see cref="IConnectionRefusalWriter"/>, the server answers a connection past a
/// connection limit <c>503 SlowDown</c> with the <c>Error</c> body, then closes it (ADR-0108,
/// decision 7).
/// </para>
/// </remarks>
public sealed class S3ProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>
    /// The scheme the server answers.
    /// </summary>
    public const string Scheme = "s3";

    private readonly ContentStore store;
    private readonly ObjectMetadataIndex index;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly TimeProvider timeProvider;
    private readonly S3MultipartUploads uploads;
    private readonly IIdentifierRandom identifiers;

    /// <summary>
    /// Creates the server.
    /// </summary>
    /// <param name="store">The shared content store; its top-level folders are the buckets.</param>
    /// <param name="index">The object metadata index of <paramref name="store"/>'s served root.</param>
    /// <param name="authenticationPolicy">The policy whose HTTP session checks each request's Signature Version 4 login.</param>
    /// <param name="timeProvider">The clock a connection refusal's <c>Date</c> is taken from, and the open
    /// multipart uploads' idleness is measured on.</param>
    /// <param name="identifiers">Where a connection refusal's request ID is drawn from; null draws from the cryptographic random number generator.</param>
    public S3ProtocolServer(ContentStore store, ObjectMetadataIndex index, IAuthenticationPolicy authenticationPolicy, TimeProvider timeProvider, IIdentifierRandom? identifiers = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.store = store;
        this.index = index;
        this.authenticationPolicy = authenticationPolicy;
        this.timeProvider = timeProvider;
        this.identifiers = identifiers ?? new S3RequestId.Unseeded();
        uploads = new S3MultipartUploads(timeProvider);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [Scheme];

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new Http1ConnectionServer(new S3RequestAnswerer(store, index, authenticationPolicy.StartHttpConnection(connection.TlsSession), context, uploads, connection.TlsSession is not null), context).ServeAsync(connection);
    }

    /// <inheritdoc/>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var requestId = S3RequestId.Draw(identifiers);
        var body = S3ErrorBody.Render(S3Error.SlowDown, string.Empty, requestId, []);
        var head = new HttpResponseHead(S3Error.SlowDown.Status)
            .AddField("Date", timeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture))
            .AddField("Server", HttpResponseHead.ServerName)
            .AddField("x-amz-request-id", requestId)
            .AddField("Content-Type", "application/xml")
            .AddField("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))
            .AddField("Connection", "close");

        await connection.WriteAsync(head.ToBytes(), cancellationToken);
        await connection.WriteAsync(body, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
