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
internal sealed class CoapResourceAnswerer(ContentStore store, CoapAccessRules accessRules, ICoapRandomSource random)
{
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
    /// <param name="cancellationToken">Cuts the store's work off.</param>
    /// <returns>The response.</returns>
    public async Task<CoapResponse> AnswerAsync(
        CoapMessage request,
        CoapRequestOptions options,
        string scheme,
        bool peerIsAuthenticated,
        CancellationToken cancellationToken)
    {
        var refusal = Refusal(request.Code, options, peerIsAuthenticated);

        if (refusal is not null)
        {
            return refusal;
        }

        if (options.UriPath is [".well-known", "core"])
        {
            return AnswerWellKnownCore(request.Code, options, cancellationToken);
        }

        try
        {
            return await AnswerResourceAsync(new Target(store, request, options, scheme), cancellationToken);
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

        if (options.UriPath.Any(segment => segment is "." or ".."))
        {
            return CoapResponse.Bare(CoapCode.BadRequest);
        }

        if (options.NamesAProxy)
        {
            return CoapResponse.Bare(CoapCode.ProxyingNotSupported);
        }

        return AccessRefusal(method, peerIsAuthenticated);
    }

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

    private CoapResponse AnswerWellKnownCore(CoapCode method, CoapRequestOptions options, CancellationToken cancellationToken)
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

        return Represent(options, CoapEntityTag.ForPayload(payload), CoapContentFormat.LinkFormat, payload);
    }

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

        using var bytes = new MemoryStream();
        await store.CopyFileBytesAsync(target.Mapping, ContentByteRange.WholeFile(status.Length), bytes, cancellationToken);

        return Represent(target.Options, CoapEntityTag.ForFile(status), CoapContentFormat.ForFileName(target.Path), bytes.ToArray());
    }

    private static CoapResponse Represent(CoapRequestOptions options, byte[] entityTag, uint format, byte[] payload)
    {
        if (PreconditionFails(options, exists: true, entityTag))
        {
            return CoapResponse.Bare(CoapCode.PreconditionFailed);
        }

        if (options.Accept is { } accept && accept != format)
        {
            return CoapResponse.Bare(CoapCode.NotAcceptable);
        }

        var tagOption = new CoapOption(CoapOptionNumber.ETag, entityTag);
        var maxAge = CoapOption.FromUInt(CoapOptionNumber.MaxAge, MaxAgeSeconds);

        if (options.EntityTags.Any(offered => offered.Span.SequenceEqual(entityTag)))
        {
            return CoapResponse.WithOptions(CoapCode.Valid, tagOption, maxAge);
        }

        return new CoapResponse(CoapCode.Content, [tagOption, maxAge, CoapOption.FromUInt(CoapOptionNumber.ContentFormat, format)], payload);
    }

    private async Task<CoapResponse> UploadAsync(Target target, bool existed, bool appending, CancellationToken cancellationToken)
    {
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
        using var source = new MemoryStream(target.Request.Payload.ToArray(), writable: false);
        var result = await store.WriteUploadAsync(mapping, source, cancellationToken);
        var location = target.Options.UriPath
            .Where(segment => segment.Length != 0)
            .Append(name)
            .Select(segment => CoapOption.FromString(CoapOptionNumber.LocationPath, segment))
            .ToArray();

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

    private sealed class Target
    {
        public Target(ContentStore store, CoapMessage request, CoapRequestOptions options, string scheme)
        {
            Request = request;
            Options = options;
            Scheme = scheme;
            Path = "/" + string.Join('/', options.UriPath.Select(Uri.EscapeDataString));
            Mapping = store.MapRequestPathForScheme(Path, scheme);
        }

        public CoapMessage Request { get; }

        public CoapRequestOptions Options { get; }

        public string Scheme { get; }

        public string Path { get; }

        public CoapCode Method => Request.Code;

        public ContentPathMapping Mapping { get; }
    }
}
