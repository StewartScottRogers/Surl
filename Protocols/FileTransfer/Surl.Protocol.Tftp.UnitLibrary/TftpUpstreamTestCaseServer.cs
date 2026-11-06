using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// The TFTP server half of an upstream curl test case (ADR-0080 decision 2): Surl's own TFTP
/// server, <see cref="TftpProtocolServer"/>, parsing every request, negotiating its options and
/// running the transfer in lock step with its own packets and its own errors, while the case's
/// <see cref="TftpUpstreamTestCaseReplies"/> supplies what FILEFORMAT.md at tag
/// <c>curl-8_21_0</c> makes the case's rather than the server's.
/// </summary>
/// <remarks>
/// <para>
/// Each flow is answered from a content store of its own, in memory: a read request finds the
/// case's file for its name there, and finds nothing - ERROR 1 - when the case has none; a write
/// request is allowed only to a name carrying a case number, and refused with ERROR 2 otherwise,
/// as uploads are off. A finished upload is kept in <see cref="TftpUpstreamTestCaseReplies.UploadedBytes"/>.
/// Every well-formed request is logged first; a first datagram that is not one is answered as
/// <see cref="TftpProtocolServer"/> answers it and logged as nothing. With <c>writedelay:</c>,
/// every DATA packet waits that long on the exchange's clock before it is sent.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c>: <c>Surl.Conformance.UnitTests</c> composes it on
/// the loopback datagram listener for upstream's test cases.
/// </para>
/// </remarks>
public sealed class TftpUpstreamTestCaseServer : IDatagramProtocolServer
{
    private readonly TftpUpstreamTestCaseReplies replies;

    /// <summary>
    /// Creates the server half of one case.
    /// </summary>
    /// <param name="replies">The case's reply, server commands and logs.</param>
    public TftpUpstreamTestCaseServer(TftpUpstreamTestCaseReplies replies)
    {
        ArgumentNullException.ThrowIfNull(replies);

        this.replies = replies;
    }

    /// <summary>The one scheme answered: <c>tftp</c>.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["tftp"]);

    /// <summary>
    /// Logs the request that opened <paramref name="flow"/> and serves it with Surl's TFTP server.
    /// </summary>
    /// <param name="flow">The flow opened by its first datagram.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(context);

        var fileSystem = new InMemoryContentFileSystem(context.TimeProvider);
        if (!TftpRequest.TryParse(flow.FirstDatagram.Span, out var request))
        {
            await new TftpProtocolServer(StoreOver(fileSystem, allowUploads: false)).ServeAsync(flow, context);
            return;
        }

        replies.RecordRequest(request!);
        var fileName = Encoding.Latin1.GetString(request!.FileName);
        var store = StoreOver(fileSystem, allowUploads: request.IsWrite && TftpUpstreamTestCaseReplies.NamesACaseFile(fileName));
        var mapping = store.MapRequestPath(TftpFileName.ToRequestPath(request.FileName));
        if (!request.IsWrite)
        {
            await PlaceTheCaseFileAsync(fileSystem, mapping, fileName, context.CancellationToken);
        }

        var served = replies.WriteDelay > TimeSpan.Zero ? new WriteDelayingDatagramFlow(flow, replies.WriteDelay, context.TimeProvider) : flow;
        await new TftpProtocolServer(store).ServeAsync(served, context);
        if (request.IsWrite)
        {
            await KeepTheUploadAsync(store, mapping, context.CancellationToken);
        }
    }

    private static ContentStore StoreOver(InMemoryContentFileSystem fileSystem, bool allowUploads) =>
        new(InMemoryContentFileSystem.RootPath, fileSystem, new ContentExposureOptions { AllowUploads = allowUploads });

    private async Task PlaceTheCaseFileAsync(InMemoryContentFileSystem fileSystem, ContentPathMapping mapping, string fileName, CancellationToken cancellationToken)
    {
        if (mapping.Location is { } location && replies.FindFile(fileName) is { } file)
        {
            await using var stream = fileSystem.CreateFileForAsyncWrite(location);
            await stream.WriteAsync(file, cancellationToken);
        }
    }

    private async Task KeepTheUploadAsync(ContentStore store, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        if (store.GetFileStatus(mapping) is { } status)
        {
            using var upload = new MemoryStream();
            await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(status.Length), upload, cancellationToken);
            replies.RecordUpload(upload.ToArray());
        }
    }
}
