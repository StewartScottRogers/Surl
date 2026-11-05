using System.Globalization;
using System.Security.Cryptography;
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
/// It serves path-style GetObject and HeadObject - ranges, conditional fields, the object's
/// stored header fields and user metadata - and answers every other request
/// <c>501 NotImplemented</c>, each failure with the S3 <c>Error</c> body. Reads are anonymous:
/// the SigV4 check is BL-420's, and PutObject, ListObjectsV2 and virtual-hosted addressing are
/// BL-419's.
/// </para>
/// <para>
/// HTTP/1.1 framing, the head limit and timeouts are <see cref="Http1ConnectionServer"/>'s, so a
/// head past <c>--max-request-head</c> is refused <c>431</c> and closed as ADR-0006 section 5
/// gives it. As an <see cref="IConnectionRefusalWriter"/>, the server answers a connection past a
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
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Creates the server.
    /// </summary>
    /// <param name="store">The shared content store; its top-level folders are the buckets.</param>
    /// <param name="index">The object metadata index of <paramref name="store"/>'s served root.</param>
    /// <param name="timeProvider">The clock a connection refusal's <c>Date</c> is taken from.</param>
    public S3ProtocolServer(ContentStore store, ObjectMetadataIndex index, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.store = store;
        this.index = index;
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [Scheme];

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new Http1ConnectionServer(new S3RequestAnswerer(store, index, context), context).ServeAsync(connection);
    }

    /// <inheritdoc/>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var requestId = RandomNumberGenerator.GetHexString(16);
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
