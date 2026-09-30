using System.Buffers.Binary;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The <c>sftp</c> subsystem on one session channel: SFTP version 3's read side answered through
/// the content store (draft-ietf-secsh-filexfer-02; ADR-0054, decisions 5 to 8 and 13).
/// </summary>
/// <remarks>
/// <para>
/// <b>Start and end.</b> The first packet must be <c>INIT</c> with version 3 or higher, answered
/// <c>VERSION</c> 3 with no extensions; any other first packet, or a version below 3, ends the
/// session with exit status 1 and no reply. The client's <c>EOF</c> ends it with exit status 0. A
/// packet past <c>--max-message</c> or one that cannot be framed ends it with exit status 1
/// (<see cref="SftpChannelFraming"/>). Requests are answered one at a time, in order.
/// </para>
/// <para>
/// <b>Answered:</b> <c>REALPATH</c> (the canonical path, existence unchecked), <c>STAT</c> and
/// <c>LSTAT</c> (alike: no link is ever reported as one), <c>FSTAT</c>, <c>OPEN</c> for reading,
/// <c>READ</c> (at most <see cref="MaxReadBytes"/> a reply, <c>EOF</c> at or past the end),
/// <c>OPENDIR</c> (only with <c>--list-directories</c>; <c>NO_SUCH_FILE</c> otherwise, whatever is
/// there), <c>READDIR</c> (at most <see cref="MaxNamesPerReply"/> names and
/// <see cref="MaxNamesPacketBytes"/> bytes a reply, then <c>EOF</c>), <c>READLINK</c> (<c>FAILURE</c>
/// <c>Not a symbolic link</c> for an entry that is there) and <c>CLOSE</c>. Hidden entries,
/// <c>/.surl</c> and refused mappings are answered exactly as missing: <c>NO_SUCH_FILE</c>.
/// </para>
/// <para>
/// <b>Not answered yet.</b> The write side - <c>OPEN</c> with any flag but <c>READ</c>,
/// <c>WRITE</c>, <c>REMOVE</c>, <c>RENAME</c>, <c>MKDIR</c>, <c>RMDIR</c>, <c>SETSTAT</c> and
/// <c>FSETSTAT</c> - is BL-166's; until then each is answered as decision 10 answers a type the
/// server does not serve, <c>OP_UNSUPPORTED</c> <c>Operation unsupported</c>. <c>SYMLINK</c> is
/// <c>OP_UNSUPPORTED</c> <c>Symbolic links cannot be created</c> and <c>EXTENDED</c>
/// <c>Extension not supported</c>, as decision 10 decides for good.
/// </para>
/// <para>
/// <b>Handles</b> are 4 bytes, a big-endian counter never reused; at most
/// <see cref="MaxOpenHandles"/> are open at once. An unknown or closed handle is <c>FAILURE</c>
/// <c>Invalid handle</c>. A packet that does not parse is <c>BAD_MESSAGE</c>, and a store failure
/// <c>FAILURE</c> <c>Read failed</c>; the session goes on. No status message holds a path or an
/// exception message (ADR-0006 section 3): those go to the verbose notes only.
/// </para>
/// </remarks>
/// <param name="store">The content store the session reads.</param>
/// <param name="context">The exchange: its log, its clock and <c>--max-message</c>.</param>
internal sealed class SftpSession(ContentStore store, ExchangeContext context) : ISshChannelHandler
{
    /// <summary>The most handles open at once in one session.</summary>
    public const int MaxOpenHandles = 100;

    /// <summary>The most bytes one <c>DATA</c> reply carries, keeping it under libssh2's 256 KiB packet bound.</summary>
    public const int MaxReadBytes = 261120;

    /// <summary>The most names one <c>READDIR</c> reply carries.</summary>
    public const int MaxNamesPerReply = 100;

    /// <summary>The most bytes one <c>READDIR</c> reply may hold, its length field included, unless one name alone is larger.</summary>
    public const int MaxNamesPacketBytes = 262144;

    private const uint OpenForReading = 0x00000001;

    // The read side's requests, each with its answer (decision 8).
    private static readonly Dictionary<byte, Func<SftpSession, SftpRequest, CancellationToken, ValueTask<byte[]>>> Answers = new()
    {
        [SftpPacketType.Open] = (session, request, _) => ValueTask.FromResult(session.AnswerOpen(request)),
        [SftpPacketType.Close] = (session, request, _) => ValueTask.FromResult(session.AnswerClose(request)),
        [SftpPacketType.Read] = (session, request, cancellationToken) => session.AnswerReadAsync(request, cancellationToken),
        [SftpPacketType.Stat] = (session, request, _) => ValueTask.FromResult(session.AnswerStat(request)),
        [SftpPacketType.LinkStat] = (session, request, _) => ValueTask.FromResult(session.AnswerStat(request)),
        [SftpPacketType.HandleStat] = (session, request, _) => ValueTask.FromResult(session.AnswerHandleStat(request)),
        [SftpPacketType.OpenDirectory] = (session, request, cancellationToken) => ValueTask.FromResult(session.AnswerOpenDirectory(request, cancellationToken)),
        [SftpPacketType.ReadDirectory] = (session, request, _) => ValueTask.FromResult(session.AnswerReadDirectory(request)),
        [SftpPacketType.RealPath] = (session, request, _) => ValueTask.FromResult(session.AnswerRealPath(request)),
        [SftpPacketType.ReadLink] = (session, request, _) => ValueTask.FromResult(session.AnswerReadLink(request)),
    };

    private readonly Dictionary<uint, SftpHandle> handles = [];
    private uint nextHandle;

    /// <summary>
    /// Serves the session until the client's <c>EOF</c> or a packet that ends it.
    /// </summary>
    /// <param name="channel">The channel's data both ways.</param>
    /// <param name="cancellationToken">Cancelled when the connection is cut off.</param>
    /// <returns>0 when the client ended the session, 1 when the server did.</returns>
    public async Task<uint> RunAsync(ISshChannelDataStream channel, CancellationToken cancellationToken)
    {
        var framing = new SftpChannelFraming(channel, context.Limits.MaxMessageBytes);
        try
        {
            var first = await framing.ReadPacketAsync(cancellationToken);
            if (first is not null)
            {
                await framing.WriteAsync(Start(first), cancellationToken);
                while (await framing.ReadPacketAsync(cancellationToken) is { } packet)
                {
                    await framing.WriteAsync(await AnswerAsync(new SftpRequest(packet), cancellationToken), cancellationToken);
                }
            }

            context.Log.Note("SFTP session ended: client EOF");

            return 0;
        }
        catch (SftpSessionEndedException ended)
        {
            context.Log.Note($"SFTP session ended: {ended.Message}");

            return 1;
        }
        finally
        {
            handles.Clear();
        }
    }

    // Decision 5: INIT with version 3 or higher, answered VERSION 3.
    private byte[] Start(byte[] packet)
    {
        if (packet[0] != SftpPacketType.Init)
        {
            throw new SftpSessionEndedException("malformed packet");
        }

        var version = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(1));
        if (version < 3)
        {
            throw new SftpSessionEndedException($"client version {version} below 3");
        }

        context.Log.Note($"SFTP session started: client version {version}, answering 3");

        return SftpReply.Version();
    }

    private async ValueTask<byte[]> AnswerAsync(SftpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await AnswerRequestAsync(request, cancellationToken);
        }
        catch (SshDisconnectRequiredException)
        {
            return Refuse(request, string.Empty, SftpStatusCode.BadMessage, "Bad message");
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return Refuse(request, string.Empty, SftpStatusCode.Failure, "Read failed", failure.Message);
        }
    }

    private ValueTask<byte[]> AnswerRequestAsync(SftpRequest request, CancellationToken cancellationToken) =>
        Answers.TryGetValue(request.Type, out var answer)
            ? answer(this, request, cancellationToken)
            : ValueTask.FromResult(AnswerUnsupported(request));

    // Decision 10's answers for what the read side does not serve; INIT after the start is a
    // malformed packet (decision 5).
    private byte[] AnswerUnsupported(SftpRequest request) => request.Type switch
    {
        SftpPacketType.Init => Refuse(request, string.Empty, SftpStatusCode.BadMessage, "Bad message"),
        SftpPacketType.SymbolicLink => Refuse(request, string.Empty, SftpStatusCode.OperationUnsupported, "Symbolic links cannot be created"),
        SftpPacketType.Extended => Refuse(request, string.Empty, SftpStatusCode.OperationUnsupported, "Extension not supported"),
        _ => Refuse(request, string.Empty, SftpStatusCode.OperationUnsupported, "Operation unsupported"),
    };

    private byte[] AnswerRealPath(SftpRequest request)
    {
        var path = request.ReadString();
        request.RequireEnd();
        var canonical = SftpPath.Canonicalise(path.Span);

        return canonical is null
            ? Absent(request, Render(path))
            : SftpReply.RealPath(request.Id, SftpPath.WithoutTrailingSlash(canonical));
    }

    private byte[] AnswerStat(SftpRequest request)
    {
        var path = request.ReadString();
        request.RequireEnd();
        var status = StatusAt(Map(path, out _));

        return status is null ? Absent(request, Render(path)) : SftpReply.Attributes(request.Id, status);
    }

    private byte[] AnswerReadLink(SftpRequest request)
    {
        var path = request.ReadString();
        request.RequireEnd();

        return StatusAt(Map(path, out _)) is null
            ? Absent(request, Render(path))
            : Refuse(request, Render(path), SftpStatusCode.Failure, "Not a symbolic link");
    }

    private byte[] AnswerOpen(SftpRequest request)
    {
        var path = request.ReadString();
        var flags = request.ReadUInt32();
        request.SkipAttributes();
        request.RequireEnd();
        var subject = $"{Render(path)} {RenderOpenFlags(flags)}";
        if ((flags & ~OpenForReading) != 0)
        {
            return Refuse(request, subject, SftpStatusCode.OperationUnsupported, "Operation unsupported", "only reads are served");
        }

        var mapping = Map(path, out var canonical);
        var status = StatusAt(mapping);
        if (status is null)
        {
            return Absent(request, subject);
        }

        return status.Kind == ContentEntryKind.Directory
            ? Refuse(request, subject, SftpStatusCode.Failure, "Is a directory")
            : Issue(request, subject, new SftpHandle(canonical!, mapping!, null));
    }

    private byte[] AnswerOpenDirectory(SftpRequest request, CancellationToken cancellationToken)
    {
        var path = request.ReadString();
        request.RequireEnd();
        if (!store.ExposureOptions.ListDirectories)
        {
            return Refuse(request, Render(path), SftpStatusCode.NoSuchFile, "No such file", "listings are off (--list-directories)");
        }

        return OpenListing(request, path, cancellationToken);
    }

    private byte[] OpenListing(SftpRequest request, ReadOnlyMemory<byte> path, CancellationToken cancellationToken)
    {
        var mapping = Map(path, out var canonical);
        var listing = mapping is null ? null : store.ListDirectory(mapping, cancellationToken);
        if (listing is { LocationKind: ContentEntryKind.File })
        {
            return Refuse(request, Render(path), SftpStatusCode.Failure, "Not a directory");
        }

        return listing is { IsListed: true }
            ? Issue(request, Render(path), new SftpHandle(canonical!, mapping!, listing.Entries))
            : Absent(request, Render(path));
    }

    private byte[] AnswerClose(SftpRequest request)
    {
        var handleBytes = request.ReadString();
        request.RequireEnd();
        if (FindHandle(handleBytes) is not { } found)
        {
            return InvalidHandle(request, handleBytes);
        }

        handles.Remove(found.Number);
        var what = found.Handle.Entries is null ? $"read {found.Handle.Progress} bytes" : $"listed {found.Handle.Progress} entries";
        context.Log.Note($"SFTP CLOSE {RenderPath(found.Handle.Path)}: {what}");

        return SftpReply.Status(request.Id, SftpStatusCode.Ok, "Success");
    }

    private async ValueTask<byte[]> AnswerReadAsync(SftpRequest request, CancellationToken cancellationToken)
    {
        var handleBytes = request.ReadString();
        var offset = request.ReadUInt64();
        var length = request.ReadUInt32();
        request.RequireEnd();
        if (FindHandle(handleBytes) is not { Handle: var handle })
        {
            return InvalidHandle(request, handleBytes);
        }

        if (handle.Entries is not null)
        {
            return Refuse(request, RenderPath(handle.Path), SftpStatusCode.Failure, "Is a directory");
        }

        if (store.GetFileStatus(handle.Mapping) is not { } file)
        {
            return Absent(request, RenderPath(handle.Path));
        }

        if (offset >= (ulong)file.Length)
        {
            return SftpReply.Status(request.Id, SftpStatusCode.EndOfFile, "End of file");
        }

        return await ReadBytesAsync(request.Id, handle, file.Length, (long)offset, (int)Math.Min(Math.Min(length, (ulong)file.Length - offset), MaxReadBytes), cancellationToken);
    }

    // Decision 8: the data is never shorter than asked for except at the end of the file.
    private async ValueTask<byte[]> ReadBytesAsync(uint id, SftpHandle handle, long fileLength, long offset, int count, CancellationToken cancellationToken)
    {
        if (count == 0)
        {
            return SftpReply.Data(id, ReadOnlyMemory<byte>.Empty);
        }

        using var buffer = new MemoryStream(count);
        var range = ContentByteRange.Select(fileLength, offset, offset + count - 1);
        var copied = await store.CopyFileBytesAsync(handle.Mapping, range, buffer, cancellationToken);
        handle.Progress += copied;

        // A file that shrank past the offset since its length was read has reached its end.
        return copied == 0
            ? SftpReply.Status(id, SftpStatusCode.EndOfFile, "End of file")
            : SftpReply.Data(id, buffer.GetBuffer().AsMemory(0, (int)buffer.Length));
    }

    private byte[] AnswerHandleStat(SftpRequest request)
    {
        var handleBytes = request.ReadString();
        request.RequireEnd();
        if (FindHandle(handleBytes) is not { Handle: var handle })
        {
            return InvalidHandle(request, handleBytes);
        }

        var status = StatusAt(handle.Mapping);

        return status is null ? Absent(request, RenderPath(handle.Path)) : SftpReply.Attributes(request.Id, status);
    }

    private byte[] AnswerReadDirectory(SftpRequest request)
    {
        var handleBytes = request.ReadString();
        request.RequireEnd();
        if (FindHandle(handleBytes) is not { Handle: var handle })
        {
            return InvalidHandle(request, handleBytes);
        }

        if (handle.Entries is not { } entries)
        {
            return Refuse(request, RenderPath(handle.Path), SftpStatusCode.Failure, "Not a directory");
        }

        return handle.Progress >= entries.Count
            ? SftpReply.Status(request.Id, SftpStatusCode.EndOfFile, "End of file")
            : SftpReply.Names(request.Id, NextNames(handle, entries));
    }

    // Decision 8: at most 100 names and 262144 bytes a reply, but always at least one name.
    private List<byte[]> NextNames(SftpHandle handle, IReadOnlyList<ContentDirectoryEntry> entries)
    {
        var now = context.TimeProvider.GetUtcNow();
        var names = new List<byte[]>();
        var packetBytes = SftpReply.NamesOverhead;
        while (handle.Progress < entries.Count && names.Count < MaxNamesPerReply)
        {
            var name = SftpReply.NameEntry(entries[(int)handle.Progress], now);
            if (names.Count > 0 && packetBytes + name.Length > MaxNamesPacketBytes)
            {
                break;
            }

            names.Add(name);
            packetBytes += name.Length;
            handle.Progress++;
        }

        return names;
    }

    private byte[] Issue(SftpRequest request, string subject, SftpHandle handle)
    {
        if (handles.Count >= MaxOpenHandles)
        {
            return Refuse(request, subject, SftpStatusCode.Failure, "Too many open handles");
        }

        var number = nextHandle++;
        handles.Add(number, handle);
        context.Log.Note($"SFTP {request.Name} {subject} -> HANDLE");

        return SftpReply.Handle(request.Id, number);
    }

    private (uint Number, SftpHandle Handle)? FindHandle(ReadOnlyMemory<byte> handleBytes)
    {
        if (handleBytes.Length != 4)
        {
            return null;
        }

        var number = BinaryPrimitives.ReadUInt32BigEndian(handleBytes.Span);

        return handles.TryGetValue(number, out var handle) ? (number, handle) : null;
    }

    // Decision 1: a path that is not UTF-8, holds a NUL or is refused by the store maps nowhere.
    private ContentPathMapping? Map(ReadOnlyMemory<byte> path, out string? canonical)
    {
        canonical = SftpPath.Canonicalise(path.Span);
        var mapping = canonical is null ? null : store.MapRequestPath(SftpPath.ToRequestPath(canonical));

        return mapping is { IsMapped: true } ? mapping : null;
    }

    private ContentEntryStatus? StatusAt(ContentPathMapping? mapping) =>
        mapping is null ? null : store.GetEntryStatus(mapping);

    private byte[] Absent(SftpRequest request, string subject) =>
        Refuse(request, subject, SftpStatusCode.NoSuchFile, "No such file", "answered as absent");

    private byte[] InvalidHandle(SftpRequest request, ReadOnlyMemory<byte> handleBytes) =>
        Refuse(request, $"handle {Render(handleBytes)}", SftpStatusCode.Failure, "Invalid handle");

    // Decision 13: SFTP <REQUEST> <path or handle's path> [<flags>] -> <STATUS NAME>[: <reason>].
    private byte[] Refuse(SftpRequest request, string subject, SftpStatusCode code, string message, string? reason = null)
    {
        var about = subject.Length == 0 ? string.Empty : " " + subject;
        var because = reason is null ? string.Empty : ": " + reason;
        context.Log.Note($"SFTP {request.Name}{about} -> {SftpReply.NameOf(code)}{because}");

        return SftpReply.Status(request.Id, code, message);
    }

    private static string Render(ReadOnlyMemory<byte> bytes) => SshLogText.Render(bytes.Span);

    private static string RenderPath(string path) => SshLogText.Render(Encoding.UTF8.GetBytes(path));

    private static string RenderOpenFlags(uint flags)
    {
        string[] names = ["READ", "WRITE", "APPEND", "CREAT", "TRUNC", "EXCL"];
        var set = names.Where((_, bit) => (flags & (1u << bit)) != 0).ToList();
        if ((flags & ~0x3Fu) != 0)
        {
            set.Add($"0x{flags & ~0x3Fu:X}");
        }

        return set.Count == 0 ? "0" : string.Join('|', set);
    }
}
