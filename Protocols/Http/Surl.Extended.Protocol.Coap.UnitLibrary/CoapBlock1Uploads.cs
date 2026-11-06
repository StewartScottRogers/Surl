using Surl.Content;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// One flow's Block1 uploads (RFC 7959 sections 2.3, 2.5 and 2.9; ADR-0097 decisions 4 and 7):
/// each an atomic <see cref="ContentUploadSession"/> keyed by the request's Uri-Path and Uri-Query,
/// written block by block and committed by the block with M unset.
/// </summary>
/// <remarks>
/// Each block with M set is answered <c>2.31 Continue</c> with Block1 echoing it in the size the
/// server uses; the last gets the method's final code with Block1. A block not at the next offset,
/// a block with no upload open, or a block whose Content-Format differs from block 0's is
/// <c>4.08</c> and discards the upload; a block repeating the last one is answered <c>2.31</c>
/// again and not written twice. A Size1 above <c>--max-filesize</c>, a total passing it, or a
/// fifth upload on the flow is <c>4.13</c> with Size1. An upload with no block for
/// EXCHANGE_LIFETIME is discarded, as is every upload when the flow ends.
/// </remarks>
/// <param name="store">The content store, for its upload limit.</param>
/// <param name="clock">The flow's clock.</param>
/// <param name="preferredSizeExponent">The server's largest SZX.</param>
internal sealed class CoapBlock1Uploads(ContentStore store, TimeProvider clock, int preferredSizeExponent) : IAsyncDisposable
{
    /// <summary>The most uploads one flow keeps open (ADR-0097 decision 7).</summary>
    public const int MaxUploadsPerFlow = 4;

    private readonly Dictionary<string, Upload> uploads = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>
    /// Starts an upload with its block 0, replacing any open under <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The request's Uri-Path and Uri-Query.</param>
    /// <param name="request">Block 0.</param>
    /// <param name="options">Its options.</param>
    /// <param name="open">Opens the session the upload is written to.</param>
    /// <param name="success">The final answer's code and options, without Block1.</param>
    /// <param name="cancellationToken">Cuts the store's work off.</param>
    /// <returns>The answer to block 0.</returns>
    public async Task<CoapResponse> BeginAsync(
        string key,
        CoapMessage request,
        CoapRequestOptions options,
        Func<CancellationToken, Task<ContentUploadOpeningOutcome>> open,
        CoapResponse success,
        CancellationToken cancellationToken)
    {
        await DiscardAsync(await TakeAsync(key));

        if (IsPastTheLimit(options.Size1 ?? 0) || await CountOpenAsync() >= MaxUploadsPerFlow)
        {
            return TooLarge();
        }

        var opened = await open(cancellationToken);

        if (opened.Session is not { } session)
        {
            return CoapResponse.Bare(CoapCode.NotFound);
        }

        var upload = new Upload(session, session.Length, options.ContentFormat, success);

        return await WriteAsync(key, upload, options.Block1!.Value, request.Payload, cancellationToken);
    }

    /// <summary>
    /// Writes a later block of the upload open under <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The request's Uri-Path and Uri-Query.</param>
    /// <param name="request">The block.</param>
    /// <param name="options">Its options.</param>
    /// <param name="cancellationToken">Cuts the store's work off.</param>
    /// <returns>The answer to the block.</returns>
    public async Task<CoapResponse> ContinueAsync(string key, CoapMessage request, CoapRequestOptions options, CancellationToken cancellationToken)
    {
        var block = options.Block1!.Value;

        var upload = await TakeAsync(key);

        if (upload is null || upload.ContentFormat != options.ContentFormat)
        {
            return await IncompleteAsync(upload);
        }

        if (block.More && block.Offset == upload.LastOffset && request.Payload.Length == upload.LastLength)
        {
            Keep(key, upload);
            return Continue(block, upload.LastOffset);
        }

        return await WriteAsync(key, upload, block, request.Payload, cancellationToken);
    }

    /// <summary>Discards every upload still open: the flow has ended.</summary>
    /// <returns>A task that completes when every temporary file is deleted.</returns>
    public async ValueTask DisposeAsync()
    {
        Upload[] open;

        lock (gate)
        {
            open = [.. uploads.Values];
            uploads.Clear();
        }

        foreach (var upload in open)
        {
            await upload.Session.DisposeAsync();
        }
    }

    private async Task<CoapResponse> WriteAsync(string key, Upload upload, CoapBlockOption block, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (block.Offset != upload.NextOffset)
        {
            return await IncompleteAsync(upload);
        }

        // A write that is too large or that throws ends the session, which deletes its own
        // temporary file; the upload is not kept, so the next block finds none and is 4.08.
        if (await upload.Session.WriteAtAsync(upload.BaseOffset + block.Offset, payload, cancellationToken) == ContentUploadResult.TooLarge)
        {
            return TooLarge();
        }

        upload.Received(block.Offset, payload.Length, clock.GetUtcNow());

        return block.More ? KeepAndContinue(key, upload, block) : await CommitAsync(upload, block, cancellationToken);
    }

    private CoapResponse KeepAndContinue(string key, Upload upload, CoapBlockOption block)
    {
        Keep(key, upload);
        return Continue(block, block.Offset);
    }

    private async Task<CoapResponse> CommitAsync(Upload upload, CoapBlockOption block, CancellationToken cancellationToken)
    {
        await using var session = upload.Session;
        // A session that stayed open was never too large, so a commit that is not written found
        // its target turned into a directory or its directory gone: answered as absent.
        if (await session.CommitAsync(cancellationToken) != ContentUploadResult.Written)
        {
            return CoapResponse.Bare(CoapCode.NotFound);
        }

        var echoed = CoapBlockOption.At(block.Offset, false, UsedSizeExponent(block)).ToOption(CoapOptionNumber.Block1);

        return upload.Success with { Options = [.. upload.Success.Options, echoed] };
    }

    private CoapResponse Continue(CoapBlockOption block, long offset) =>
        CoapResponse.WithOptions(CoapCode.Continue, CoapBlockOption.At(offset, true, UsedSizeExponent(block)).ToOption(CoapOptionNumber.Block1));

    private int UsedSizeExponent(CoapBlockOption block) => Math.Min(block.SizeExponent, preferredSizeExponent);

    private static async Task<CoapResponse> IncompleteAsync(Upload? upload)
    {
        await DiscardAsync(upload);
        return CoapResponse.Bare(CoapCode.RequestEntityIncomplete);
    }

    private static async Task DiscardAsync(Upload? upload)
    {
        if (upload is not null)
        {
            await upload.Session.DisposeAsync();
        }
    }

    private bool IsPastTheLimit(long length) =>
        store.ExposureOptions.MaxUploadBytes != 0 && length > store.ExposureOptions.MaxUploadBytes;

    private CoapResponse TooLarge() => CoapResponse.WithOptions(
        CoapCode.RequestEntityTooLarge,
        CoapOption.FromUInt(CoapOptionNumber.Size1, (uint)Math.Min(store.ExposureOptions.MaxUploadBytes, uint.MaxValue)));

    // Takes the upload out of the table, so one block at a time owns it; an expired one is
    // discarded and comes back null.
    private async Task<Upload?> TakeAsync(string key)
    {
        Upload? upload;

        lock (gate)
        {
            uploads.Remove(key, out upload);
        }

        if (upload is not null && IsExpired(upload))
        {
            await upload.Session.DisposeAsync();
            return null;
        }

        return upload;
    }

    private void Keep(string key, Upload upload)
    {
        lock (gate)
        {
            uploads[key] = upload;
        }
    }

    private async Task<int> CountOpenAsync()
    {
        List<Upload> expired = [];
        int count;

        lock (gate)
        {
            foreach (var (key, upload) in uploads.ToArray())
            {
                if (IsExpired(upload))
                {
                    uploads.Remove(key);
                    expired.Add(upload);
                }
            }

            count = uploads.Count;
        }

        foreach (var upload in expired)
        {
            await upload.Session.DisposeAsync();
        }

        return count;
    }

    private bool IsExpired(Upload upload) =>
        clock.GetUtcNow() - upload.LastReceived >= CoapTransmissionParameters.ExchangeLifetime;

    private sealed class Upload(ContentUploadSession session, long baseOffset, uint? contentFormat, CoapResponse success)
    {
        public ContentUploadSession Session { get; } = session;

        public long BaseOffset { get; } = baseOffset;

        public uint? ContentFormat { get; } = contentFormat;

        public CoapResponse Success { get; } = success;

        public long NextOffset { get; private set; }

        public long LastOffset { get; private set; } = -1;

        public int LastLength { get; private set; }

        public DateTimeOffset LastReceived { get; private set; }

        public void Received(long offset, int length, DateTimeOffset now)
        {
            LastOffset = offset;
            LastLength = length;
            NextOffset = offset + length;
            LastReceived = now;
        }
    }
}
