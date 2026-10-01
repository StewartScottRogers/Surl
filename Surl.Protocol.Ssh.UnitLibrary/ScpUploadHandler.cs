using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// SCP's sink mode, <c>scp -t</c>: surl receives one file into the content store, as curl's SCP
/// upload sends it (ADR-0054, decisions 1, 4, 12 and 13).
/// </summary>
/// <remarks>
/// <para>
/// surl sends <c>\0</c> at once, then reads control lines of at most <c>--max-line</c> bytes: a
/// <c>T</c> line is kept for the file and answered <c>\0</c>; a <c>C</c> line names the file. The
/// target is <c>&lt;path&gt;/&lt;name&gt;</c> when the path names an existing directory, else the
/// path itself.
/// </para>
/// <para>
/// Every check is made on the <c>C</c> line, before any data is read, each refusal one
/// <c>\x01scp: ...</c> line and exit status 1: a malformed line (<c>protocol error: ...</c>),
/// uploads off (<c>Permission denied</c>), a refused path or a missing directory (<c>No such file
/// or directory</c>), <c>-d</c> and a path that is not a directory (<c>Not a directory</c>), a
/// hidden or <c>/.surl</c> target (<c>Permission denied</c>), a directory target (<c>Is a
/// directory</c>), and a size past <c>--max-filesize</c> (<c>File too large</c>). Otherwise surl
/// sends <c>\0</c> and streams exactly the size's bytes into a <see cref="ContentUploadSession"/>,
/// committed once the last arrives: libssh2 sends no <c>\0</c> after the data. A <c>\0</c> or
/// <c>EOF</c> after it is answered <c>\0</c>, exit status 0.
/// </para>
/// <para>
/// Data that ends early is discarded with the temporary file (ADR-0006 section 5), the target
/// untouched. A store failure is <c>write error</c>, its message in the verbose note only
/// (ADR-0006 section 3). The <c>C</c> line's mode is never kept.
/// </para>
/// </remarks>
/// <param name="store">The content store the file is written to.</param>
/// <param name="command">The accepted <c>scp -t</c> command.</param>
/// <param name="context">The exchange: its log and <c>--max-line</c>.</param>
internal sealed class ScpUploadHandler(ContentStore store, SshScpCommand command, ExchangeContext context) : ISshChannelHandler
{
    /// <summary>The most bytes read from the channel for one write to the store.</summary>
    public const int ChunkBytes = 32768;

    private static readonly ContentUploadOpening ReplacingOrCreating = new(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false);

    private readonly string path = SshLogText.Render(Encoding.UTF8.GetBytes(command.Path));

    /// <inheritdoc/>
    public async Task<uint> RunAsync(ISshChannelDataStream channel, CancellationToken cancellationToken)
    {
        var reader = new ScpChannelReader(channel);
        await channel.WriteAsync(ScpReply.Ok, cancellationToken);
        var (header, lastWriteTime, error) = await ReadHeaderAsync(channel, reader, cancellationToken);
        if (header is not null)
        {
            return await ReceiveAsync(channel, reader, header, lastWriteTime, cancellationToken);
        }

        if (error is not null)
        {
            return await RefuseAsync(channel, path, $"protocol error: {error}", null, cancellationToken);
        }

        context.Log.Note($"SCP upload {path} ended: the client sent no file");

        return 1;
    }

    // Reads T lines, answering each \0, up to the C line; the header, or the protocol error, or
    // neither when the client ended its data first.
    private async Task<(ScpFileHeader? Header, DateTimeOffset? LastWriteTime, string? Error)> ReadHeaderAsync(
        ISshChannelDataStream channel,
        ScpChannelReader reader,
        CancellationToken cancellationToken)
    {
        DateTimeOffset? lastWriteTime = null;
        while (true)
        {
            var (line, tooLong) = await reader.ReadLineAsync(context.Limits.MaxLineBytes, cancellationToken);
            if (line is null)
            {
                return (null, null, tooLong ? "line too long" : null);
            }

            if (!ScpControlLine.TryReadTimes(line, out var time))
            {
                var error = ScpControlLine.ReadFile(line, out var header);

                return (header, lastWriteTime, error.Length == 0 ? null : error);
            }

            lastWriteTime = time;
            await channel.WriteAsync(ScpReply.Ok, cancellationToken);
        }
    }

    private async Task<uint> ReceiveAsync(
        ISshChannelDataStream channel,
        ScpChannelReader reader,
        ScpFileHeader header,
        DateTimeOffset? lastWriteTime,
        CancellationToken cancellationToken)
    {
        var target = ResolveTarget(header.Name);
        try
        {
            var refusal = RefusalBeforeOpening(target);
            if (refusal is not null)
            {
                return await RefuseAsync(channel, target.Rendered, refusal.Value.Line, refusal.Value.Reason, cancellationToken);
            }

            return await OpenAndReceiveAsync(channel, reader, header, lastWriteTime, target, cancellationToken);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return await RefuseAsync(channel, target.Rendered, $"{target.Rendered}: write error", failure.Message, cancellationToken);
        }
    }

    // Decision 4's checks that need no upload opened, in its order.
    private (string Line, string? Reason)? RefusalBeforeOpening(ScpTarget target)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return ($"{target.Rendered}: Permission denied", "uploads are off (--allow-uploads)");
        }

        if (target.Mapping is null)
        {
            return ($"{target.Rendered}: No such file or directory", "answered as absent");
        }

        return command.TargetIsDirectory && !target.PathIsDirectory ? ($"{path}: Not a directory", null) : null;
    }

    private async Task<uint> OpenAndReceiveAsync(
        ISshChannelDataStream channel,
        ScpChannelReader reader,
        ScpFileHeader header,
        DateTimeOffset? lastWriteTime,
        ScpTarget target,
        CancellationToken cancellationToken)
    {
        var opening = await store.OpenUploadAsync(target.Mapping!, ReplacingOrCreating, cancellationToken);
        await using var session = opening.Session;
        if (session is null)
        {
            return await RefuseAsync(channel, target.Rendered, $"{target.Rendered}: {Refusal(opening.Result)}", null, cancellationToken);
        }

        var maxUploadBytes = store.ExposureOptions.MaxUploadBytes;
        if (maxUploadBytes > 0 && header.Size > maxUploadBytes)
        {
            return await RefuseAsync(channel, target.Rendered, $"{target.Rendered}: File too large", "past --max-filesize", cancellationToken);
        }

        await channel.WriteAsync(ScpReply.Ok, cancellationToken);
        if (!await ReceiveBytesAsync(reader, session, header.Size, target, cancellationToken))
        {
            return 1;
        }

        return await CommitAsync(channel, reader, session, header, lastWriteTime, target, cancellationToken);
    }

    private static string Refusal(ContentUploadOpeningResult result) => result switch
    {
        ContentUploadOpeningResult.NoSuchDirectory => "No such file or directory",
        ContentUploadOpeningResult.IsADirectory => "Is a directory",
        _ => "Permission denied",
    };

    private async Task<bool> ReceiveBytesAsync(ScpChannelReader reader, ContentUploadSession session, long size, ScpTarget target, CancellationToken cancellationToken)
    {
        var buffer = new byte[ChunkBytes];
        long received = 0;
        while (received < size)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(0, (int)Math.Min(ChunkBytes, size - received)), cancellationToken);
            if (count == 0)
            {
                context.Log.Note($"SCP upload {target.Rendered} abandoned after {received} of {size} bytes");

                return false;
            }

            // The size is within --max-filesize, so the session never ends as too large.
            await session.WriteAtAsync(received, buffer.AsMemory(0, count), cancellationToken);
            received += count;
        }

        return true;
    }

    private async Task<uint> CommitAsync(
        ISshChannelDataStream channel,
        ScpChannelReader reader,
        ContentUploadSession session,
        ScpFileHeader header,
        DateTimeOffset? lastWriteTime,
        ScpTarget target,
        CancellationToken cancellationToken)
    {
        if (await session.CommitAsync(cancellationToken) != ContentUploadResult.Written)
        {
            return await RefuseAsync(channel, target.Rendered, $"{target.Rendered}: write error", "the target can no longer take a file", cancellationToken);
        }

        var timeSet = command.PreservesTimes && lastWriteTime is not null;
        if (timeSet)
        {
            // The file was committed a moment ago, so it is there to take the time.
            store.SetLastWriteTime(target.Mapping!, lastWriteTime!.Value);
        }

        context.Log.Note($"SCP upload {target.Rendered}: received {header.Size} bytes, mode {header.Mode} not kept{(timeSet ? ", modification time set" : string.Empty)}");

        // libssh2 sends only EOF after the data; OpenSSH's client sends a \0.
        var next = await reader.ReadByteAsync(cancellationToken);
        if (next > 0)
        {
            context.Log.Note($"SCP upload {target.Rendered} ended: the client sent byte 0x{next:X2} after the data");

            return 1;
        }

        await channel.WriteAsync(ScpReply.Ok, cancellationToken);

        return 0;
    }

    // The file is <path>/<name> when the path names an existing directory, else the path itself.
    private ScpTarget ResolveTarget(string name)
    {
        var mapping = SshContentPath.Map(store, Encoding.UTF8.GetBytes(command.Path), out var canonical);
        if (mapping is null || store.GetEntryKind(mapping) != ContentEntryKind.Directory)
        {
            return new ScpTarget(path, mapping, PathIsDirectory: false);
        }

        var joined = canonical!.TrimEnd('/') + "/" + name;
        var separator = command.Path.EndsWith('/') ? string.Empty : "/";
        var rendered = path + separator + SshLogText.Render(Encoding.UTF8.GetBytes(name));

        return new ScpTarget(rendered, SshContentPath.Map(store, Encoding.UTF8.GetBytes(joined), out _), PathIsDirectory: true);
    }

    private async Task<uint> RefuseAsync(ISshChannelDataStream channel, string subject, string line, string? detail, CancellationToken cancellationToken)
    {
        var because = detail is null ? string.Empty : ": " + detail;
        context.Log.Note($"SCP upload {subject} refused: {line}{because}");
        await channel.WriteAsync(ScpReply.Error(line), cancellationToken);

        return 1;
    }

    private sealed record ScpTarget(string Rendered, ContentPathMapping? Mapping, bool PathIsDirectory);
}
