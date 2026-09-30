using Surl.Content;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The write side of the <c>sftp</c> subsystem (ADR-0054, decisions 9 and 10): uploads through
/// <see cref="ContentUploadSession"/>, and the requests behind curl's <c>-Q</c> commands through
/// the content store's changes (decision 11).
/// </summary>
internal sealed partial class SftpSession
{
    private const uint OpenForWriting = 0x00000002;
    private const uint OpenForAppending = 0x00000004;
    private const uint OpenCreating = 0x00000008;
    private const uint OpenTruncating = 0x00000010;
    private const uint OpenExclusively = 0x00000020;
    private const uint OpenFlagsDefined = 0x0000003F;

    private static readonly (string Message, string Reason) TooLarge = ("File too large", "past --max-filesize");

    // The requests whose store failure is answered Write failed rather than Read failed: CLOSE
    // included, since only closing an upload touches the store.
    private static readonly HashSet<byte> WritingRequests =
    [
        SftpPacketType.Close, SftpPacketType.Write, SftpPacketType.SetStat, SftpPacketType.HandleSetStat, SftpPacketType.Remove,
        SftpPacketType.MakeDirectory, SftpPacketType.RemoveDirectory, SftpPacketType.Rename,
    ];

    // Decision 10's answer for each change the store refuses.
    private static readonly Dictionary<ContentChangeResult, (SftpStatusCode Code, string Message, string? Reason)> ChangeRefusals = new()
    {
        [ContentChangeResult.Absent] = (SftpStatusCode.NoSuchFile, "No such file", "answered as absent"),
        [ContentChangeResult.NoSuchDirectory] = (SftpStatusCode.NoSuchFile, "No such file", "no such directory"),
        [ContentChangeResult.NotEmpty] = (SftpStatusCode.Failure, "Directory not empty", null),
        [ContentChangeResult.Exists] = (SftpStatusCode.Failure, "File already exists", null),
        [ContentChangeResult.NotPermitted] = (SftpStatusCode.PermissionDenied, "Permission denied", "not permitted there"),
    };

    // Decision 9: OPEN with WRITE opens an upload, committed over the target at CLOSE.
    private async ValueTask<byte[]> OpenForWritingAsync(SftpRequest request, ReadOnlyMemory<byte> path, uint flags, SftpAttributes attributes, string subject, CancellationToken cancellationToken)
    {
        if (OpenForWritingRefusal(request, flags, subject) is { } refused)
        {
            return refused;
        }

        if (Map(path, out var canonical) is not { } mapping)
        {
            return Absent(request, subject);
        }

        var wasThere = StatusAt(mapping) is not null;
        var opening = new ContentUploadOpening(
            StartsFromExistingBytes: (flags & OpenTruncating) == 0,
            CreatesMissingFile: (flags & OpenCreating) != 0,
            RefusesExistingFile: (flags & OpenExclusively) != 0);
        var outcome = await OpenUploadAsync(mapping, opening, cancellationToken);
        if (outcome.Session is not { } upload)
        {
            return RefuseOpening(request, subject, outcome);
        }

        NotePermissionsNotKept(request, canonical!, wasThere ? null : attributes.Permissions);

        return Issue(request, subject, new SftpHandle(canonical!, mapping, null)
        {
            Upload = upload,
            Appends = (flags & OpenForAppending) != 0,
            ReadsUpload = (flags & OpenForReading) != 0,
        });
    }

    // Uploads off first, before anything else is looked at; then the flags draft-02 section 6.3
    // defines; then the handle limit, so no upload is opened only to be refused.
    private byte[]? OpenForWritingRefusal(SftpRequest request, uint flags, string subject)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return UploadsOff(request, subject);
        }

        if ((flags & ~OpenFlagsDefined) != 0)
        {
            return Refuse(request, subject, SftpStatusCode.OperationUnsupported, "Operation unsupported", "a flag draft-02 does not define");
        }

        if ((flags & (OpenTruncating | OpenExclusively)) != 0 && (flags & OpenCreating) == 0)
        {
            return Refuse(request, subject, SftpStatusCode.BadMessage, "Bad message", "TRUNC or EXCL without CREAT");
        }

        return handles.Count >= MaxOpenHandles
            ? Refuse(request, subject, SftpStatusCode.Failure, "Too many open handles")
            : null;
    }

    private async Task<(ContentUploadOpeningResult Result, ContentUploadSession? Session, string? Failure)> OpenUploadAsync(
        ContentPathMapping mapping, ContentUploadOpening opening, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await store.OpenUploadAsync(mapping, opening, cancellationToken);

            return (outcome.Result, outcome.Session, null);
        }
        catch (Exception failure) when (IsStoreFailure(failure))
        {
            return (ContentUploadOpeningResult.NotPermitted, null, failure.Message);
        }
    }

    // Decision 9's answers for an upload the store did not open: its failure, or why not.
    private byte[] RefuseOpening(SftpRequest request, string subject, (ContentUploadOpeningResult Result, ContentUploadSession? Session, string? Failure) outcome) => outcome switch
    {
        { Failure: { } failure } => Refuse(request, subject, SftpStatusCode.Failure, "Write failed", failure),
        { Result: ContentUploadOpeningResult.NotPermitted } => Refuse(request, subject, SftpStatusCode.PermissionDenied, "Permission denied", "not permitted there"),
        { Result: ContentUploadOpeningResult.IsADirectory } => Refuse(request, subject, SftpStatusCode.Failure, "Is a directory"),
        { Result: ContentUploadOpeningResult.Exists } => Refuse(request, subject, SftpStatusCode.Failure, "File already exists"),
        _ => Absent(request, subject),
    };

    // Decision 13: a creation carrying PERMISSIONS says they are not kept.
    private void NotePermissionsNotKept(SftpRequest request, string path, uint? permissions)
    {
        if (permissions is { } mode)
        {
            context.Log.Note($"SFTP {request.Name} {RenderPath(path)}: permissions {Convert.ToString(mode, 8)} not kept");
        }
    }

    // Decision 9: an APPEND handle writes at the upload's end, any other at its offset.
    private async ValueTask<byte[]> AnswerWriteAsync(SftpRequest request, CancellationToken cancellationToken)
    {
        var handleBytes = request.ReadString();
        var offset = request.ReadUInt64();
        var data = request.ReadString();
        request.RequireEnd();
        if (WritableHandle(request, handleBytes) is not { } handle)
        {
            return RefusalForHandle(request, handleBytes);
        }

        var at = handle.Appends ? (ulong)handle.Upload!.Length : offset;
        var failure = at > (ulong)(long.MaxValue - data.Length)
            ? TooLarge
            : await UploadFailureAsync(() => handle.Upload!.WriteAtAsync((long)at, data, cancellationToken));
        if (failure is not null)
        {
            return await DiscardAsync(request, handle, failure.Value);
        }

        handle.Progress += data.Length;

        return SftpReply.Status(request.Id, SftpStatusCode.Ok, "Success");
    }

    // A handle a WRITE or an FSETSTAT SIZE may act on: open, an upload, not discarded.
    private SftpHandle? WritableHandle(SftpRequest request, ReadOnlyMemory<byte> handleBytes) =>
        FindHandle(handleBytes) is { Handle: { Upload: not null, Discarded: null } handle } ? handle : null;

    // Why WritableHandle found none: no such handle, one not open for writing, or one discarded.
    private byte[] RefusalForHandle(SftpRequest request, ReadOnlyMemory<byte> handleBytes)
    {
        if (FindHandle(handleBytes) is not { Handle: var handle })
        {
            return InvalidHandle(request, handleBytes);
        }

        return handle.Discarded is { } discarded
            ? Refuse(request, RenderPath(handle.Path), SftpStatusCode.Failure, discarded.Message, discarded.Reason)
            : Refuse(request, RenderPath(handle.Path), SftpStatusCode.Failure, "Handle not open for writing");
    }

    // A write or resize that went past --max-filesize, or that the store failed; null when it was made.
    private static async Task<(string Message, string Reason)?> UploadFailureAsync(Func<Task<ContentUploadResult>> change)
    {
        try
        {
            return await change() == ContentUploadResult.Written ? null : TooLarge;
        }
        catch (Exception failure) when (IsStoreFailure(failure))
        {
            return ("Write failed", failure.Message);
        }
    }

    // Decision 9: a failed write discards the upload; every later request on the handle but CLOSE
    // gets the same answer.
    private async Task<byte[]> DiscardAsync(SftpRequest request, SftpHandle handle, (string Message, string Reason) failure)
    {
        handle.Discarded = failure;
        await handle.Upload!.DisposeAsync();

        return Refuse(request, RenderPath(handle.Path), SftpStatusCode.Failure, failure.Message, failure.Reason);
    }

    private async ValueTask<byte[]> ReadUploadAsync(SftpRequest request, SftpHandle handle, ContentUploadSession upload, ulong offset, uint length, CancellationToken cancellationToken)
    {
        if (handle.Discarded is { } discarded)
        {
            return Refuse(request, RenderPath(handle.Path), SftpStatusCode.Failure, discarded.Message, discarded.Reason);
        }

        if (!handle.ReadsUpload)
        {
            return Refuse(request, RenderPath(handle.Path), SftpStatusCode.Failure, "Handle not open for reading");
        }

        if (offset >= (ulong)upload.Length)
        {
            return SftpReply.Status(request.Id, SftpStatusCode.EndOfFile, "End of file");
        }

        var buffer = new byte[Math.Min(Math.Min(length, (ulong)upload.Length - offset), MaxReadBytes)];
        var read = await upload.ReadAtAsync((long)offset, buffer, cancellationToken);

        return SftpReply.Data(request.Id, buffer.AsMemory(0, read));
    }

    // Decision 8: a write handle's size is the upload's, its times the clock's now.
    private byte[] UploadAttributes(SftpRequest request, SftpHandle handle, ContentUploadSession upload) =>
        handle.Discarded is { } discarded
            ? Refuse(request, RenderPath(handle.Path), SftpStatusCode.Failure, discarded.Message, discarded.Reason)
            : SftpReply.Attributes(request.Id, new ContentEntryStatus(ContentEntryKind.File, upload.Length, context.TimeProvider.GetUtcNow()));

    // Decision 9: CLOSE commits the upload, then applies any modification time an FSETSTAT set.
    private async Task<byte[]> CloseUploadAsync(SftpRequest request, SftpHandle handle, ContentUploadSession upload)
    {
        var path = RenderPath(handle.Path);
        var failure = handle.Discarded ?? await CommitFailureAsync(upload);
        if (failure is { } discarded)
        {
            context.Log.Note($"SFTP CLOSE {path}: upload discarded: {discarded.Reason}");

            return Refuse(request, path, SftpStatusCode.Failure, discarded.Message, discarded.Reason);
        }

        if (handle.LastWriteTimeAtClose is { } lastWrite)
        {
            store.SetLastWriteTime(handle.Mapping, lastWrite);
        }

        context.Log.Note($"SFTP CLOSE {path}: wrote {handle.Progress} bytes");

        return SftpReply.Status(request.Id, SftpStatusCode.Ok, "Success");
    }

    // The commit is not cancelled: a rename cut off half way would leave the temporary file behind.
    private static async Task<(string Message, string Reason)?> CommitFailureAsync(ContentUploadSession upload)
    {
        try
        {
            return await upload.CommitAsync(CancellationToken.None) == ContentUploadResult.Written
                ? null
                : ("Write failed", "the target can no longer take a file");
        }
        catch (Exception failure) when (IsStoreFailure(failure))
        {
            return ("Write failed", failure.Message);
        }
    }

    // Decision 10: SETSTAT on a path - SIZE through an upload committed at once, then ACMODTIME.
    private async ValueTask<byte[]> AnswerSetStatAsync(SftpRequest request, CancellationToken cancellationToken)
    {
        var path = request.ReadString();
        var attributes = request.ReadAttributes();
        request.RequireEnd();
        var subject = Render(path);
        var mapping = Map(path, out _);
        if (StatusAt(mapping) is not { } status)
        {
            return Absent(request, subject);
        }

        return SetStatRefusal(request, subject, attributes)
            ?? await ResizeFileAsync(request, subject, mapping!, status, attributes.Size, cancellationToken)
            ?? ChangeRefusal(request, subject, SetLastWriteTime(mapping!, attributes.ModificationTime))
            ?? Succeed(request, subject);
    }

    // Decision 10, in order: permissions and owners, nothing asked for, uploads off.
    private byte[]? SetStatRefusal(SftpRequest request, string subject, SftpAttributes attributes)
    {
        if (attributes.AsksForWhatIsNotKept)
        {
            return Refuse(request, subject, SftpStatusCode.OperationUnsupported, "Permissions and owners are not kept");
        }

        if (attributes.Flags == 0)
        {
            return Succeed(request, subject);
        }

        return store.ExposureOptions.AllowUploads ? null : UploadsOff(request, subject);
    }

    private async Task<byte[]?> ResizeFileAsync(SftpRequest request, string subject, ContentPathMapping mapping, ContentEntryStatus status, ulong? size, CancellationToken cancellationToken)
    {
        if (size is not { } length)
        {
            return null;
        }

        if (status.Kind == ContentEntryKind.Directory)
        {
            return Refuse(request, subject, SftpStatusCode.Failure, "Is a directory");
        }

        var outcome = await OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: true, CreatesMissingFile: false, RefusesExistingFile: false), cancellationToken);
        if (outcome.Session is not { } upload)
        {
            return RefuseOpening(request, subject, outcome);
        }

        var failure = await ResizeFailureAsync(upload, length) ?? await CommitFailureAsync(upload);

        return failure is { } refused ? Refuse(request, subject, SftpStatusCode.Failure, refused.Message, refused.Reason) : null;
    }

    private static async Task<(string Message, string Reason)?> ResizeFailureAsync(ContentUploadSession upload, ulong length)
    {
        if (length > long.MaxValue)
        {
            await upload.DisposeAsync();

            return TooLarge;
        }

        return await UploadFailureAsync(() => upload.SetLengthAsync((long)length));
    }

    private ContentChangeResult SetLastWriteTime(ContentPathMapping mapping, uint? modificationTime) =>
        modificationTime is { } seconds
            ? store.SetLastWriteTime(mapping, DateTimeOffset.FromUnixTimeSeconds(seconds))
            : ContentChangeResult.Done;

    // Decision 10: FSETSTAT - SIZE on a write handle resizes its upload; ACMODTIME on a write
    // handle waits for its CLOSE, on any other is set at once.
    private async ValueTask<byte[]> AnswerHandleSetStatAsync(SftpRequest request)
    {
        var handleBytes = request.ReadString();
        var attributes = request.ReadAttributes();
        request.RequireEnd();
        if (FindHandle(handleBytes) is not { Handle: { Discarded: null } handle })
        {
            return RefusalForHandle(request, handleBytes);
        }

        var subject = RenderPath(handle.Path);

        return SetStatRefusal(request, subject, attributes)
            ?? await ResizeThroughHandleAsync(request, handleBytes, handle, attributes.Size)
            ?? SetLastWriteTimeThroughHandle(request, subject, handle, attributes.ModificationTime)
            ?? Succeed(request, subject);
    }

    private async Task<byte[]?> ResizeThroughHandleAsync(SftpRequest request, ReadOnlyMemory<byte> handleBytes, SftpHandle handle, ulong? size)
    {
        if (size is not { } length)
        {
            return null;
        }

        if (handle.Upload is not { } upload)
        {
            return RefusalForHandle(request, handleBytes);
        }

        var failure = await ResizeFailureAsync(upload, length);

        return failure is { } discarded ? await DiscardAsync(request, handle, discarded) : null;
    }

    private byte[]? SetLastWriteTimeThroughHandle(SftpRequest request, string subject, SftpHandle handle, uint? modificationTime)
    {
        if (modificationTime is { } seconds && handle.Upload is not null)
        {
            handle.LastWriteTimeAtClose = DateTimeOffset.FromUnixTimeSeconds(seconds);

            return null;
        }

        return ChangeRefusal(request, subject, SetLastWriteTime(handle.Mapping, modificationTime));
    }

    // Decision 10: REMOVE deletes a file.
    private byte[] AnswerRemove(SftpRequest request)
    {
        var path = request.ReadString();
        request.RequireEnd();
        var subject = Render(path);
        if (!store.ExposureOptions.AllowUploads)
        {
            return UploadsOff(request, subject);
        }

        var mapping = Map(path, out _);

        return StatusAt(mapping) switch
        {
            null => Absent(request, subject),
            { Kind: ContentEntryKind.Directory } => Refuse(request, subject, SftpStatusCode.Failure, "Is a directory"),
            _ => ChangeRefusal(request, subject, store.DeleteFile(mapping!)) ?? Succeed(request, subject),
        };
    }

    // Decision 10: MKDIR creates a directory inside an existing one; its attributes are not kept.
    private byte[] AnswerMakeDirectory(SftpRequest request)
    {
        var path = request.ReadString();
        var attributes = request.ReadAttributes();
        request.RequireEnd();
        var subject = Render(path);
        if (!store.ExposureOptions.AllowUploads)
        {
            return UploadsOff(request, subject);
        }

        if (Map(path, out var canonical) is not { } mapping)
        {
            return Absent(request, subject);
        }

        if (ChangeRefusal(request, subject, store.CreateDirectory(mapping)) is { } refused)
        {
            return refused;
        }

        NotePermissionsNotKept(request, canonical!, attributes.Permissions);

        return Succeed(request, subject);
    }

    // Decision 10: RMDIR removes an empty directory.
    private byte[] AnswerRemoveDirectory(SftpRequest request)
    {
        var path = request.ReadString();
        request.RequireEnd();
        var subject = Render(path);
        if (!store.ExposureOptions.AllowUploads)
        {
            return UploadsOff(request, subject);
        }

        var mapping = Map(path, out _);

        return StatusAt(mapping) switch
        {
            null => Absent(request, subject),
            { Kind: ContentEntryKind.File } => Refuse(request, subject, SftpStatusCode.Failure, "Not a directory"),
            _ => ChangeRefusal(request, subject, store.RemoveEmptyDirectory(mapping!)) ?? Succeed(request, subject),
        };
    }

    // Decision 10: RENAME never replaces (draft-02 section 6.5).
    private byte[] AnswerRename(SftpRequest request)
    {
        var oldPath = request.ReadString();
        var newPath = request.ReadString();
        request.RequireEnd();
        var subject = $"{Render(oldPath)} {Render(newPath)}";
        if (!store.ExposureOptions.AllowUploads)
        {
            return UploadsOff(request, subject);
        }

        var from = Map(oldPath, out _);
        var to = Map(newPath, out _);
        if (from is null || to is null)
        {
            return Absent(request, subject);
        }

        return ChangeRefusal(request, subject, store.RenameEntryWithoutReplacing(from, to)) ?? Succeed(request, subject);
    }

    // What each change the store refused is answered with; null when it was made.
    private byte[]? ChangeRefusal(SftpRequest request, string subject, ContentChangeResult result)
    {
        if (result == ContentChangeResult.Done)
        {
            return null;
        }

        var (code, message, reason) = ChangeRefusals[result];

        return Refuse(request, subject, code, message, reason);
    }

    private byte[] UploadsOff(SftpRequest request, string subject) =>
        Refuse(request, subject, SftpStatusCode.PermissionDenied, "Permission denied", "uploads are off (--allow-uploads)");

    // Decision 13: a request that changes the tree is noted even when it succeeds.
    private byte[] Succeed(SftpRequest request, string subject)
    {
        context.Log.Note($"SFTP {request.Name} {subject} -> OK");

        return SftpReply.Status(request.Id, SftpStatusCode.Ok, "Success");
    }
}
