using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The <c>oci</c> server: answers OCI Distribution Specification v1.1.0 registry requests over
/// HTTP/1.1 on the shared content store, each repository an OCI Image Layout folder (ADR-0115;
/// ADR-0100, decision 2.4). It is judged by the specification, not by upstream curl, which has no
/// <c>oci</c> scheme (ADR-0083).
/// </summary>
/// <remarks>
/// <para>
/// It serves <c>GET /v2/</c>, manifest <c>GET</c> and <c>HEAD</c> by tag or digest, blob
/// <c>GET</c> and <c>HEAD</c> by digest with one <c>Range</c>, the tag list with <c>n</c> and
/// <c>last</c> paging, and the referrers API with its <c>artifactType</c> filter, each failure
/// with the specification's JSON error body. Pushes and deletes (BL-437) and token
/// authentication (BL-438) are not served yet: every request is read as the anonymous caller.
/// </para>
/// <para>
/// HTTP/1.1 framing, the head limit and timeouts are <see cref="Http1ConnectionServer"/>'s. As an
/// <see cref="IConnectionRefusalWriter"/>, the server answers a connection past a connection limit
/// <c>429 TOOMANYREQUESTS</c> with the JSON error body, then closes it (ADR-0115, decision 7).
/// </para>
/// </remarks>
public sealed class OciRegistryProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>
    /// The scheme the server answers.
    /// </summary>
    public const string Scheme = "oci";

    private readonly ContentStore store;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Creates the server.
    /// </summary>
    /// <param name="store">The shared content store; its folders holding an <c>oci-layout</c> are the repositories.</param>
    /// <param name="timeProvider">The clock a connection refusal's <c>Date</c> is taken from.</param>
    public OciRegistryProtocolServer(ContentStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.store = store;
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [Scheme];

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new Http1ConnectionServer(new OciRequestAnswerer(store, context), context).ServeAsync(connection);
    }

    /// <inheritdoc/>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var body = OciErrorBody.Render(OciError.TooManyRequests);
        var head = new HttpResponseHead(OciError.TooManyRequests.Status);
        foreach (var (name, value) in OciReply.CommonFields(timeProvider.GetUtcNow()))
        {
            head = head.AddField(name, value);
        }

        head = head
            .AddField("Content-Type", "application/json")
            .AddField("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))
            .AddField("Connection", "close");

        await connection.WriteAsync(head.ToBytes(), cancellationToken);
        await connection.WriteAsync(body, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
