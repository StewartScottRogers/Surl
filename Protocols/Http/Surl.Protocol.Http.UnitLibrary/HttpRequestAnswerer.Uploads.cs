using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// How the answerer stores a <c>PUT</c> under <c>--allow-uploads</c> (ADR-0133, decision 2).
/// </summary>
internal sealed partial class HttpRequestAnswerer
{
    private static readonly HttpStatus Created = new(201, "Created");

    private static readonly HttpStatus NoContent = new(204, "No Content");

    private static readonly HttpStatus Conflict = new(409, "Conflict");

    private static readonly ContentUploadOpening ReplacingUpload = new(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false);

    // A PUT is an upload the server serves only under --allow-uploads; otherwise it is the 405
    // every unserved method is.
    private bool IsServedUpload(HttpRequest request) =>
        request.Method == "PUT" && contentStore.ExposureOptions.AllowUploads;

    // The body goes into an upload session beside the target, which is committed only once all
    // of it has arrived and any login bound to it accepts it; anything else disposes the session
    // uncommitted, so the file the PUT would replace stands. With no session and no body check
    // the body is never read.
    private async Task<HttpAnswer> AnswerPutAsync(HttpRequest request, ContentCaller caller, IHttpRequestBodyCheck? bodyCheck, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        if (!request.Body.IsReadable)
        {
            return Refuse(HttpStatus.BadRequest, null, challenges, $"{request.Method} {request.Target}: the body's framing is not one the server reads");
        }

        var mapping = contentStore.MapRequestPathForCaller(RequestPath(request.Target), caller);
        var replaces = mapping.IsMapped && contentStore.GetEntryKind(mapping) == ContentEntryKind.File;
        var opening = mapping.IsMapped
            ? await contentStore.OpenUploadAsync(mapping, ReplacingUpload, cancellationToken)
            : new ContentUploadOpeningOutcome(ContentUploadOpeningResult.NotPermitted, null);
        if (opening.Session is null && bodyCheck is null)
        {
            return UploadNotOpened(request, opening.Result, challenges, request.Body.IsAnnounced);
        }

        await using (opening.Session)
        {
            return await ReceiveUploadAsync(request, new PutTarget(mapping, opening, replaces), bodyCheck, challenges, cancellationToken);
        }
    }

    private async Task<HttpAnswer> ReceiveUploadAsync(HttpRequest request, PutTarget target, IHttpRequestBodyCheck? bodyCheck, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        using var bodyHash = bodyCheck is null ? null : IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var sink = new HttpUploadStream(target.Opening.Session, bodyHash);
        var bodyOutcome = request.Body.IsAnnounced
            ? await request.Body.CopyToAsync(sink, cancellationToken)
            : HttpRequestBodyDiscardOutcome.Discarded;
        if (bodyOutcome != HttpRequestBodyDiscardOutcome.Discarded)
        {
            return RefuseBody(request, bodyOutcome, challenges);
        }

        if (bodyCheck is null)
        {
            return await StoreUploadAsync(request, target, sink.IsTooLarge, challenges, cancellationToken);
        }

        var verdict = await bodyCheck.JudgeBodyAsync(bodyHash!.GetHashAndReset(), cancellationToken);

        return await RefuseUploadLoginAsync(request, verdict)
            ?? await StoreUploadAsync(request, target, sink.IsTooLarge, verdict.WwwAuthenticateValues, cancellationToken);
    }

    // The verdict on a login bound to the upload's body: refused or challenged, or, the login
    // proven only now, a fault rule's chosen error, which leaves the upload uncommitted
    // (ADR-0134 decision 1); null when the upload is to be stored.
    private async Task<HttpAnswer?> RefuseUploadLoginAsync(HttpRequest request, HttpAuthenticationVerdict verdict)
    {
        if (verdict.CheckedLogin is not null)
        {
            context.Log.Note(verdict.CheckedLogin.Note);
        }

        return verdict.Outcome != HttpAuthenticationOutcome.Proceed
            ? RefuseLogin(request, verdict.Outcome, verdict.WwwAuthenticateValues)
            : await AnswerChosenErrorAsync(request, verdict.AccountName, verdict.WwwAuthenticateValues);
    }

    private async Task<HttpAnswer> StoreUploadAsync(HttpRequest request, PutTarget target, bool isTooLarge, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        if (target.Opening.Session is not { } session)
        {
            return UploadNotOpened(request, target.Opening.Result, challenges, leavesBodyUnread: false);
        }

        var committed = isTooLarge ? ContentUploadResult.TooLarge : await session.CommitAsync(cancellationToken);
        if (committed == ContentUploadResult.TooLarge)
        {
            return Refuse(HttpStatus.ContentTooLarge, null, challenges, $"{request.Method} {request.Target}: the body went past the upload limit of {contentStore.ExposureOptions.MaxUploadBytes} bytes");
        }

        if (committed != ContentUploadResult.Written)
        {
            return UploadNotOpened(request, ContentUploadOpeningResult.NotPermitted, challenges, leavesBodyUnread: false);
        }

        return CommittedUploadAnswer(request, target, session.Length, challenges);
    }

    // The 201 or 204 for an upload that was written.
    private HttpAnswer CommittedUploadAnswer(HttpRequest request, PutTarget target, long storedLength, IReadOnlyList<string> challenges)
    {
        var status = target.Replaces ? NoContent : Created;
        context.Log.Note($"{request.Method} {request.Target}: {status.Code}, {storedLength} bytes stored at {target.Mapping.Location}");
        List<KeyValuePair<string, string>> fields = [new("Date", FormatHttpDate(Now())), new("Server", HttpResponseHead.ServerName)];
        if (!target.Replaces)
        {
            fields.Add(new("Content-Length", "0"));
        }

        return new HttpAnswer(status, AddChallenges(fields, challenges), HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: false);
    }

    // A location that is missing its directory, or is a directory, conflicts with the PUT
    // (409); any other the store does not permit - hidden, refused, a share the caller may not
    // write - is forbidden (403). Neither is a refusal: the connection stays as a 404 keeps it.
    private HttpAnswer UploadNotOpened(HttpRequest request, ContentUploadOpeningResult result, IReadOnlyList<string> challenges, bool leavesBodyUnread)
    {
        var status = result is ContentUploadOpeningResult.NoSuchDirectory or ContentUploadOpeningResult.IsADirectory ? Conflict : HttpStatus.Forbidden;
        context.Log.Note($"{request.Method} {request.Target}: {status.Code}, the upload could not be opened ({result})");

        return new HttpAnswer(status, EmptyAnswerFields(null, challenges), HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: leavesBodyUnread);
    }

    // Where a PUT's body goes: the mapped location, the upload opened there, and whether a file
    // was already there for it to replace.
    private sealed record PutTarget(ContentPathMapping Mapping, ContentUploadOpeningOutcome Opening, bool Replaces);
}
