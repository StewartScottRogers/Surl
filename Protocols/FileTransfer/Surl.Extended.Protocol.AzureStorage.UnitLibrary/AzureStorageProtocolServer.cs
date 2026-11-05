using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Azure Storage server: answers the Azure Blob service REST API over HTTP/1.1 on the shared
/// content store, whose top-level folders are the containers (ADR-0109; ADR-0100, decision 2). It
/// is judged by the Azure Storage REST API reference at <c>x-ms-version: 2025-01-05</c>, not by
/// upstream curl, which has no <c>az</c> scheme (ADR-0083).
/// </summary>
/// <remarks>
/// <para>
/// It serves Get Blob and Get Blob Properties - ranges, conditional fields, the blob's stored
/// header fields and metadata - checks <c>x-ms-version</c> and <c>x-ms-date</c>, logs in with a
/// bearer token through the <see cref="IAuthenticationPolicy"/>'s HTTP session (or anonymously
/// when the policy allows it), and refuses every other request with the Blob service's XML
/// <c>Error</c> and <c>x-ms-error-code</c>. Put Blob, List Blobs and Delete Blob are BL-425's, the
/// Data Lake schemes BL-426's, Shared Key and shared access signatures BL-717's.
/// </para>
/// <para>
/// HTTP/1.1 framing, the head limit and timeouts are <see cref="Http1ConnectionServer"/>'s. As an
/// <see cref="IConnectionRefusalWriter"/>, the server answers a connection past a connection limit
/// <c>503 ServerBusy</c> with the <c>Error</c> body, then closes it (ADR-0109, decision 7).
/// </para>
/// </remarks>
public sealed class AzureStorageProtocolServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>
    /// The Blob endpoint's scheme.
    /// </summary>
    public const string BlobScheme = "az";

    private readonly ContentStore store;
    private readonly ObjectMetadataIndex index;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly TimeProvider timeProvider;

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
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [BlobScheme];

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var authentication = authenticationPolicy.StartHttpConnection(connection.TlsSession);

        return new Http1ConnectionServer(new AzureBlobRequestAnswerer(store, index, authentication, context), context).ServeAsync(connection);
    }

    /// <inheritdoc/>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var requestId = AzureReply.NewRequestId();
        var now = timeProvider.GetUtcNow();
        var body = AzureErrorBody.Render(AzureRefusal.Of(AzureError.ServerBusy), requestId, now);
        var head = new HttpResponseHead(AzureError.ServerBusy.Status);
        foreach (var field in AzureReply.CommonFields(requestId, now))
        {
            head.AddField(field.Key, field.Value);
        }

        head.AddField("x-ms-error-code", AzureError.ServerBusy.Code)
            .AddField("Content-Type", "application/xml")
            .AddField("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))
            .AddField("Connection", "close");

        await connection.WriteAsync(head.ToBytes(), cancellationToken);
        await connection.WriteAsync(body, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
