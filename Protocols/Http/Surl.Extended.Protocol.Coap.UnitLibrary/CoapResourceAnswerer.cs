using System.Text;
using Surl.Content;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Answers one request from the content store (RFC 7252 sections 5.8 to 5.10; ADR-0097 decisions
/// 1, 3 and 6): <c>GET</c>, <c>PUT</c>, <c>POST</c> and <c>DELETE</c> on files, and
/// <c>/.well-known/core</c>.
/// </summary>
/// <remarks>
/// The order of checks, first match answers: <c>4.02</c> for an unrecognised critical option;
/// <c>4.00</c> for a Uri-Path of <c>.</c> or <c>..</c>; <c>5.05</c> for Proxy-Uri or
/// Proxy-Scheme; <c>4.05</c> for any method but the four; <c>4.01</c> when the access rules refuse
/// the peer; <c>4.05</c> for a write without <c>--allow-uploads</c>; then the resource. A
/// content-store failure (<see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>)
/// is <c>5.00</c> with no payload, never the exception's text.
/// </remarks>
/// <param name="store">The resources.</param>
/// <param name="accessRules">Who may read and write.</param>
/// <param name="random">Draws the names of files posted into a directory.</param>
/// <param name="preferredSizeExponent">The largest block SZX the server sends or asks for:
/// 6, 1024 bytes, unless a test or a smaller DTLS record needs less (ADR-0097 decision 4).</param>
internal sealed class CoapResourceAnswerer(
    ContentStore store,
    CoapAccessRules accessRules,
    ICoapRandomSource random,
    int preferredSizeExponent = CoapBlockOption.LargestSizeExponent)
{
    /// <summary>The largest block SZX the server sends or asks for.</summary>
    public int PreferredSizeExponent => preferredSizeExponent;

    /// <summary>Max-Age, sent on every <c>2.05</c> and <c>2.03</c> (ADR-0097 decision 1).</summary>
    public const uint MaxAgeSeconds = 60;

    private const int PostedNameBytes = 16;

    private static readonly HashSet<CoapCode> ServedMethods = [CoapCode.Get, CoapCode.Post, CoapCode.Put, CoapCode.Delete];

    /// <summary>
    /// Answers <paramref name="request"/>.
    /// </summary>
    /// <param name="request">A request: its code is a method.</param>
    /// <param name="options">Its options, read.</param>
    /// <param name="scheme">The scheme it came in on, for the content store's shares.</param>
    /// <param name="peerIsAuthenticated">Whether the peer is authenticated; never over <c>coap</c>.</param>
    /// <param name="uploads">The flow's Block1 uploads.</param>
    /// <param name="cancellationToken">Cuts the store's work off.</param>
    /// <returns>The response.</returns>
    public async Task<CoapResponse> AnswerAsync(
        CoapMessage request,
        CoapRequestOptions options,
        string scheme,
        bool peerIsAuthenticated,
        CoapBlock1Uploads uploads,
        CancellationToken cancellationToken)
    {
        var refusal = Refusal(request.Code, options, peerIsAuthenticated);

        if (refusal is not null)
        {
            return refusal;
        }

        if (options.UriPath is [".well-known", "core"])
        {
            return await AnswerWellKnownCoreAsync(request.Code, options, cancellationToken);
        }

        try
        {
            return await AnswerTargetAsync(new Target(store, request, options, scheme, uploads), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CoapResponse.Bare(CoapCode.InternalServerError);
        }
    }

    private CoapResponse? Refusal(CoapCode method, CoapRequestOptions options, bool peerIsAuthenticated)
    {
        if (options.UnrecognisedCriticalOption is { } number)
        {
            return new CoapResponse(CoapCode.BadOption, [], Encoding.UTF8.GetBytes($"Unrecognized option {number}"));
        }

        if (IsABadRequest(options))
        {
            return CoapResponse.Bare(CoapCode.BadRequest);
        }

        if (options.NamesAProxy)
        {
            return CoapResponse.Bare(CoapCode.ProxyingNotSupported);
        }

        return AccessRefusal(method, peerIsAuthenticated);
    }

    // 4.00: a Uri-Path segment of . or .., or a block of SZX 7, which RFC 7959 section 2.2 reserves.
    private static bool IsABadRequest(CoapRequestOptions options) =>
        options.UriPath.Any(segment => segment is "." or "..")
        || IsReservedSize(options.Block1)
        || IsReservedSize(options.Block2);

    private static bool IsReservedSize(CoapBlockOption? block) =>
        block?.SizeExponent == CoapBlockOption.ReservedSizeExponent;

    private CoapResponse? AccessRefusal(CoapCode method, bool peerIsAuthenticated)
    {
        if (!ServedMethods.Contains(method))
        {
            return CoapResponse.Bare(CoapCode.MethodNotAllowed);
        }

        return method == CoapCode.Get ? ReadRefusal(peerIsAuthenticated) : WriteRefusal(peerIsAuthenticated);
    }

    private CoapResponse? ReadRefusal(bool peerIsAuthenticated) =>
        accessRules.MayRead(peerIsAuthenticated) ? null : CoapResponse.Bare(CoapCode.Unauthorized);

    private CoapResponse? WriteRefusal(bool peerIsAuthenticated)
    {
        if (!accessRules.MayWrite(peerIsAuthenticated))
        {
            return CoapResponse.Bare(CoapCode.Unauthorized);
        }

        return store.ExposureOptions.AllowUploads ? null : CoapResponse.Bare(CoapCode.MethodNotAllowed);
    }

    private async Task<CoapResponse> AnswerWellKnownCoreAsync(CoapCode method, CoapRequestOptions options, CancellationToken cancellationToken)
    {
        if (method != CoapCode.Get)
        {
            return CoapResponse.Bare(CoapCode.MethodNotAllowed);
        }

        if (!store.ExposureOptions.ListDirectories)
        {
            return CoapResponse.Bare(CoapCode.NotFound);
        }

        var payload = CoapResourceDirectory.Write(store, options.UriQuery, cancellationToken);

        return await RepresentAsync(
            options,
            new Representation(CoapEntityTag.ForPayload(payload), CoapContentFormat.LinkFormat, payload.Length),
            window => Task.FromResult<ReadOnlyMemory<byte>>(payload.AsMemory((int)window.Offset, window.Count)));
    }

    // A later block continues the upload its block 0 opened, whose conditions were checked then
    // (RFC 7959 section 2.10); only PUT and POST may carry Block1.
    private Task<CoapResponse> AnswerTargetAsync(Target target, CancellationToken cancellationToken) =>
        target.Options.Block1 is { Number: > 0 }
            ? target.Uploads.ContinueAsync(target.UploadKey, target.Request, target.Options, cancellationToken)
            : AnswerResourceAsync(target, cancellationToken);

    private async Task<CoapResponse> AnswerResourceAsync(Target target, CancellationToken cancellationToken)
    {
        if (!target.Mapping.IsMapped)
        {
            return CoapResponse.Bare(target.Method == CoapCode.Delete ? CoapCode.Deleted : CoapCode.NotFound);
        }

        if (store.GetEntryKind(target.Mapping) == ContentEntryKind.Directory)
        {
            return await AnswerDirectoryAsync(target, cancellationToken);
        }

        var status = store.GetFileStatus(target.Mapping);

        return target.Method == CoapCode.Get
            ? await GetAsync(target, status, cancellationToken)
            : await AnswerFileWriteAsync(target, status, cancellationToken);
    }

    private async Task<CoapResponse> AnswerFileWriteAsync(Target target, ContentFileStatus? status, CancellationToken cancellationToken)
    {
        var exists = status is not null;

        if (PreconditionFails(target.Options, exists, exists ? CoapEntityTag.ForFile(status!) : []))
        {
            return CoapResponse.Bare(CoapCode.PreconditionFailed);
        }

        return target.Method == CoapCode.Delete
            ? Delete(target.Mapping)
            : await UploadAsync(target, exists, appending: target.Method == CoapCode.Post, cancellationToken);
    }

    private async Task<CoapResponse> AnswerDirectoryAsync(Target target, CancellationToken cancellationToken)
    {
        if (target.Method == CoapCode.Post)
        {
            return await PostIntoDirectoryAsync(target, cancellationToken);
        }

        return CoapResponse.Bare(target.Method == CoapCode.Delete ? CoapCode.MethodNotAllowed : CoapCode.NotFound);
    }

    private async Task<CoapResponse> GetAsync(Target target, ContentFileStatus? status, CancellationToken cancellationToken)
    {
        if (status is null)
        {
            return CoapResponse.Bare(CoapCode.NotFound);
        }

        return await RepresentAsync(
            target.Options,
            new Representation(CoapEntityTag.ForFile(status), CoapContentFormat.ForFileName(target.Path), status.Length),
            window => ReadWindowAsync(target.Mapping, status.Length, window, cancellationToken));
    }

    // Each block is read afresh from the store as a byte range: answering is stateless (ADR-0097 decision 4).
    private async Task<ReadOnlyMemory<byte>> ReadWindowAsync(ContentPathMapping mapping, long length, CoapBlock2Window window, CancellationToken cancellationToken)
    {
        using var bytes = new MemoryStream();
        var range = window.Count == 0
            ? ContentByteRange.WholeFile(length)
            : ContentByteRange.Select(length, window.Offset, window.Offset + window.Count - 1);
        await store.CopyFileBytesAsync(mapping, range, bytes, cancellationToken);

        return bytes.ToArray();
    }

    private async Task<CoapResponse> RepresentAsync(
        CoapRequestOptions options,
        Representation representation,
        Func<CoapBlock2Window, Task<ReadOnlyMemory<byte>>> read)
    {
        if (PreconditionFails(options, exists: true, representation.EntityTag))
        {
            return CoapResponse.Bare(CoapCode.PreconditionFailed);
        }

        if (options.Accept is { } accept && accept != representation.Format)
        {
            return CoapResponse.Bare(CoapCode.NotAcceptable);
        }

        var tagOption = new CoapOption(CoapOptionNumber.ETag, representation.EntityTag);
        var maxAge = CoapOption.FromUInt(CoapOptionNumber.MaxAge, MaxAgeSeconds);

        if (options.EntityTags.Any(offered => offered.Span.SequenceEqual(representation.EntityTag)))
        {
            return CoapResponse.WithOptions(CoapCode.Valid, tagOption, maxAge);
        }

        if (CoapBlock2Window.Select(options, representation.Length, preferredSizeExponent) is not { } window)
        {
            return CoapResponse.Bare(CoapCode.BadOption);
        }

        var format = CoapOption.FromUInt(CoapOptionNumber.ContentFormat, representation.Format);

        return new CoapResponse(CoapCode.Content, [tagOption, maxAge, format, .. window.Options], await read(window));
    }

    private async Task<CoapResponse> UploadAsync(Target target, bool existed, bool appending, CancellationToken cancellationToken)
    {
        var success = CoapResponse.Bare(existed ? CoapCode.Changed : CoapCode.Created);

        if (target.Options.Block1 is not null)
        {
            var opening = new ContentUploadOpening(StartsFromExistingBytes: appending, CreatesMissingFile: true, RefusesExistingFile: false);

            return await target.Uploads.BeginAsync(
                target.UploadKey,
                target.Request,
                target.Options,
                token => store.OpenUploadAsync(target.Mapping, opening, token),
                success,
                cancellationToken);
        }

        using var source = new MemoryStream(target.Request.Payload.ToArray(), writable: false);
        var result = appending
            ? await store.AppendUploadAsync(target.Mapping, source, cancellationToken)
            : await store.WriteUploadAsync(target.Mapping, source, cancellationToken);

        return UploadAnswer(result, existed ? CoapCode.Changed : CoapCode.Created);
    }

    private async Task<CoapResponse> PostIntoDirectoryAsync(Target target, CancellationToken cancellationToken)
    {
        if (PreconditionFails(target.Options, exists: false, []))
        {
            return CoapResponse.Bare(CoapCode.PreconditionFailed);
        }

        Span<byte> nameBytes = stackalloc byte[PostedNameBytes];
        random.Fill(nameBytes);
        var name = Convert.ToHexStringLower(nameBytes);
        var mapping = store.MapRequestPathForScheme(target.Path.TrimEnd('/') + "/" + name, target.Scheme);
        var location = target.Options.UriPath
            .Where(segment => segment.Length != 0)
            .Append(name)
            .Select(segment => CoapOption.FromString(CoapOptionNumber.LocationPath, segment))
            .ToArray();

        if (target.Options.Block1 is not null)
        {
            var opening = new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: true);

            return await target.Uploads.BeginAsync(
                target.UploadKey,
                target.Request,
                target.Options,
                token => store.OpenUploadAsync(mapping, opening, token),
                CoapResponse.WithOptions(CoapCode.Created, location),
                cancellationToken);
        }

        using var source = new MemoryStream(target.Request.Payload.ToArray(), writable: false);
        var result = await store.WriteUploadAsync(mapping, source, cancellationToken);

        return UploadAnswer(result, CoapCode.Created, location);
    }

    private CoapResponse UploadAnswer(ContentUploadResult result, CoapCode success, params CoapOption[] successOptions) => result switch
    {
        ContentUploadResult.Written => CoapResponse.WithOptions(success, successOptions),
        ContentUploadResult.TooLarge => CoapResponse.WithOptions(
            CoapCode.RequestEntityTooLarge,
            CoapOption.FromUInt(CoapOptionNumber.Size1, (uint)Math.Min(store.ExposureOptions.MaxUploadBytes, uint.MaxValue))),
        _ => CoapResponse.Bare(CoapCode.NotFound),
    };

    private CoapResponse Delete(ContentPathMapping mapping) =>
        CoapResponse.Bare(store.DeleteFile(mapping) == ContentChangeResult.NotPermitted ? CoapCode.MethodNotAllowed : CoapCode.Deleted);

    private static bool PreconditionFails(CoapRequestOptions options, bool exists, byte[] entityTag)
    {
        if (options.IfNoneMatch && exists)
        {
            return true;
        }

        if (options.IfMatch.Count == 0)
        {
            return false;
        }

        return !exists || !options.IfMatch.Any(value => value.Length == 0 || value.Span.SequenceEqual(entityTag));
    }

    private sealed record Representation(byte[] EntityTag, uint Format, long Length);

    private sealed class Target
    {
        public Target(ContentStore store, CoapMessage request, CoapRequestOptions options, string scheme, CoapBlock1Uploads uploads)
        {
            Request = request;
            Options = options;
            Scheme = scheme;
            Uploads = uploads;
            Path = "/" + string.Join('/', options.UriPath.Select(Uri.EscapeDataString));
            UploadKey = Path + "?" + string.Join('&', options.UriQuery.Select(argument => Uri.EscapeDataString(argument)));
            Mapping = store.MapRequestPathForScheme(Path, scheme);
        }

        public CoapBlock1Uploads Uploads { get; }

        // RFC 7959 section 2.5: a Block1 upload is told apart by its request's Uri-Path and Uri-Query.
        public string UploadKey { get; }

        public CoapMessage Request { get; }

        public CoapRequestOptions Options { get; }

        public string Scheme { get; }

        public string Path { get; }

        public CoapCode Method => Request.Code;

        public ContentPathMapping Mapping { get; }
    }
}
