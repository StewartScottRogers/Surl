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
/// with the specification's JSON error body. Under <c>--allow-uploads</c> it accepts blob uploads
/// (a session with chunks, a single <c>POST</c>, a mount), manifest pushes by tag or digest, and
/// deletes. Every <c>/v2/</c> request needs a Bearer token from the server's own <c>GET /token</c>,
/// which checks <c>Basic</c> credentials through the <see cref="IAuthenticationPolicy"/>; under
/// <c>--allow-anonymous</c> reads need none (BL-438; ADR-0115, decision 4).
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

    // Makes upload session UUIDs outside tests: a version 4 UUID each.
    private static readonly Func<Guid> NewRandomSessionId = Guid.NewGuid;

    private readonly ContentStore store;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly TimeProvider timeProvider;
    private readonly OciServerState state;

    /// <summary>
    /// Creates the server.
    /// </summary>
    /// <param name="store">The shared content store; its folders holding an <c>oci-layout</c> are the repositories.</param>
    /// <param name="authenticationPolicy">
    /// Checks the token endpoint's <c>Basic</c> credentials against the accounts, and says whether
    /// anonymous reads are allowed (<c>--allow-anonymous</c>).
    /// </param>
    /// <param name="timeProvider">
    /// The clock a connection refusal's <c>Date</c> is taken from, and upload sessions and tokens expire on.
    /// </param>
    public OciRegistryProtocolServer(ContentStore store, IAuthenticationPolicy authenticationPolicy, TimeProvider timeProvider)
        : this(store, authenticationPolicy, timeProvider, NewRandomSessionId)
    {
    }

    /// <summary>
    /// Creates the server with the shared object metadata index, where each blob or manifest
    /// file's verified digest is cached against the file's stamp, so a file is hashed once until it
    /// changes rather than on every read (ADR-0115, decision 2.1).
    /// </summary>
    /// <param name="store">The shared content store; its folders holding an <c>oci-layout</c> are the repositories.</param>
    /// <param name="index">The object metadata index kept beside <paramref name="store"/>'s served root.</param>
    /// <param name="authenticationPolicy">Checks the token endpoint's credentials, and whether anonymous reads are allowed.</param>
    /// <param name="timeProvider">The clock.</param>
    public OciRegistryProtocolServer(ContentStore store, ObjectMetadataIndex index, IAuthenticationPolicy authenticationPolicy, TimeProvider timeProvider)
        : this(store, authenticationPolicy, timeProvider, NewRandomSessionId, metadataIndex: index)
    {
        ArgumentNullException.ThrowIfNull(index);
    }

    /// <summary>
    /// Creates the server with upload session UUIDs from <paramref name="newSessionId"/>, so a test
    /// knows each session's <c>Location</c>.
    /// </summary>
    /// <param name="store">The shared content store.</param>
    /// <param name="authenticationPolicy">Checks credentials.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="newSessionId">Makes each upload session's UUID.</param>
    /// <param name="maxLiveTokens">How many live tokens it keeps, so a test reaches the bound.</param>
    /// <param name="metadataIndex">Where verified digests are cached; <see langword="null"/> hashes on every read.</param>
    internal OciRegistryProtocolServer(ContentStore store, IAuthenticationPolicy authenticationPolicy, TimeProvider timeProvider, Func<Guid> newSessionId, int maxLiveTokens = OciTokenBook.MaxLiveTokens, ObjectMetadataIndex? metadataIndex = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.store = store;
        this.authenticationPolicy = authenticationPolicy;
        this.timeProvider = timeProvider;
        state = new OciServerState(new OciUploadSessions(timeProvider), new SemaphoreSlim(1, 1), newSessionId, new OciTokenBook(timeProvider, maxLiveTokens), metadataIndex);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [Scheme];

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new Http1ConnectionServer(new OciRequestAnswerer(store, state, authenticationPolicy, connection.TlsSession, context), context).ServeAsync(connection);
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
