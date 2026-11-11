using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Azure Storage server: answers the Azure Blob service REST API on <c>az</c> and the Data Lake
/// Storage Gen2 endpoint on <c>abfs</c> and <c>abfss</c>, over HTTP/1.1 on the shared content
/// store, whose top-level folders are the containers and filesystems (ADR-0109; ADR-0100,
/// decision 2). It is judged by the Azure Storage REST API reference at
/// <c>x-ms-version: 2025-01-05</c>, not by upstream curl, which has none of these schemes (ADR-0083).
/// </summary>
/// <remarks>
/// <para>
/// The listen scheme decides the endpoint. On <c>az</c> it serves Get Blob and Get Blob Properties -
/// ranges, conditional fields, the blob's stored header fields and metadata - Put Blob (under
/// <c>--allow-uploads</c>), Delete Blob and List Blobs (under <c>--list-directories</c>), refusing
/// every other request with the Blob service's XML <c>Error</c>. On <c>abfs</c> and <c>abfss</c> it
/// serves Path - Read, Path - Create, Path - Update's <c>append</c> and <c>flush</c>, Path - Delete
/// and Path - List, refusing every other request with the Data Lake JSON error. Both check
/// <c>x-ms-version</c> and <c>x-ms-date</c> and log in with a bearer token, Shared Key or a shared
/// access signature through the <see cref="IAuthenticationPolicy"/>'s HTTP session (or anonymously
/// when the policy allows it and no credentials are sent). Both read the path-style form,
/// <c>/&lt;account&gt;/&lt;container&gt;/&lt;blob&gt;</c> on an IP-literal or <c>localhost</c> host
/// (<see cref="AzurePathStyle"/>).
/// </para>
/// <para>
/// HTTP/1.1 framing, the head limit and timeouts are <see cref="Http1ConnectionServer"/>'s. As an
/// <see cref="IConnectionRefusalWriter"/>, the server answers a connection past a connection limit
/// <c>503 ServerBusy</c> with the <c>Error</c> body on <c>az</c> and the JSON error on <c>abfs</c> and
/// <c>abfss</c>, then closes it (ADR-0109, decisions 5.2 and 7).
/// </para>
/// </remarks>
public sealed class AzureStorageProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>
    /// The Blob endpoint's scheme.
    /// </summary>
    public const string BlobScheme = "az";

    /// <summary>
    /// The address an exchange's peer came from, which a shared access signature's <c>sip</c>
    /// must admit: <see cref="System.Net.IPAddress.None"/> for a peer with no IP address (a Unix
    /// socket), which no signed range admits.
    /// </summary>
    /// <param name="context">The exchange.</param>
    /// <returns>The peer's address.</returns>
    public static System.Net.IPAddress PeerAddressOf(ExchangeContext context) =>
        context.RemoteEndPoint is System.Net.IPEndPoint peer ? peer.Address : System.Net.IPAddress.None;

    /// <summary>
    /// The Data Lake endpoint's scheme over plain HTTP/1.1.
    /// </summary>
    public const string DataLakeScheme = "abfs";

    /// <summary>
    /// The Data Lake endpoint's scheme over TLS.
    /// </summary>
    public const string SecureDataLakeScheme = "abfss";

    private readonly ContentStore store;
    private readonly ObjectMetadataIndex index;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly TimeProvider timeProvider;
    private readonly AzureStagingArea staging;

    /// <summary>
    /// Creates the server.
    /// </summary>
    /// <param name="store">The shared content store; its top-level folders are the containers.</param>
    /// <param name="index">The object metadata index of <paramref name="store"/>'s served root.</param>
    /// <param name="authenticationPolicy">The policy whose HTTP session judges each request's login.</param>
    /// <param name="timeProvider">The clock a connection refusal's <c>Date</c> is taken from.</param>
    public AzureStorageProtocolServer(ContentStore store, ObjectMetadataIndex index, IAuthenticationPolicy authenticationPolicy, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.store = store;
        this.index = index;
        this.authenticationPolicy = authenticationPolicy;
        this.timeProvider = timeProvider;
        staging = new AzureStagingArea(timeProvider);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [BlobScheme, DataLakeScheme, SecureDataLakeScheme];

    /// <summary>
    /// The uploads every connection of this server stages: Data Lake appends and Put Block's blocks.
    /// </summary>
    internal AzureStagingArea StagingArea => staging;

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var authentication = authenticationPolicy.StartHttpConnection(connection.TlsSession);

        var endpointScheme = connection.TlsSession is null ? "http" : "https";
        var pathStyle = new AzurePathStyle(authenticationPolicy.NamesAccount);

        // The listen scheme decides the endpoint (ADR-0109, decision 1).
        IHttpRequestHandler answerer = context.Scheme == BlobScheme
            ? new AzureBlobRequestAnswerer(store, index, authentication, context, endpointScheme, staging, pathStyle)
            : new AzureDataLakeRequestAnswerer(store, index, authentication, context, staging, pathStyle);

        return new Http1ConnectionServer(answerer, context).ServeAsync(connection);
    }

    /// <inheritdoc/>
    /// <remarks>Written as the Blob endpoint's refusal, the XML <c>Error</c>.</remarks>
    public ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken) =>
        WriteRefusalAsync(connection, refusal, BlobScheme, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// The Blob endpoint's XML <c>Error</c> on <c>az</c>; the Data Lake endpoint's JSON error on
    /// <c>abfs</c> and <c>abfss</c> (ADR-0109, decision 5.2).
    /// </remarks>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, string listenScheme, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var requestId = AzureReply.NewRequestId();
        var now = timeProvider.GetUtcNow();
        var writesJson = listenScheme != BlobScheme;
        var body = writesJson
            ? AzureDataLakeErrorBody.Render(AzureError.ServerBusy, requestId, now)
            : AzureErrorBody.Render(AzureRefusal.Of(AzureError.ServerBusy), requestId, now);
        var head = new HttpResponseHead(AzureError.ServerBusy.Status);
        foreach (var field in AzureReply.CommonFields(requestId, now))
        {
            head.AddField(field.Key, field.Value);
        }

        head.AddField("x-ms-error-code", AzureError.ServerBusy.Code)
            .AddField("Content-Type", writesJson ? AzureDataLakeErrorBody.ContentType : "application/xml")
            .AddField("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))
            .AddField("Connection", "close");

        await connection.WriteAsync(head.ToBytes(), cancellationToken);
        await connection.WriteAsync(body, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
