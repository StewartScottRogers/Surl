using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// SCP's source mode, <c>scp -f</c>: surl sends one file from the content store, as curl's SCP
/// download asks (ADR-0054, decisions 1, 3 and 13).
/// </summary>
/// <remarks>
/// <para>
/// Once the client's <c>\0</c> arrives surl sends, with <c>-p</c>, the <c>T</c> line, then the
/// <c>C</c> line, each waiting for the client's <c>\0</c>; then the file's bytes and a <c>\0</c>,
/// and it reads one more byte or <c>EOF</c> before ending with exit status 0. Anything but
/// <c>\0</c> where an acknowledgement is due ends the transfer with exit status 1.
/// </para>
/// <para>
/// Missing, hidden, <c>/.surl</c> and refused paths are all answered
/// <c>\x01scp: &lt;path&gt;: No such file or directory</c>, a directory
/// <c>not a regular file</c> and a store failure <c>read error</c>, each with exit status 1.
/// No line holds a host path or an exception message (ADR-0006 section 3): those go to the
/// verbose notes only. A file that shrinks while it is sent ends the channel after the bytes
/// there are, without the trailing <c>\0</c>, exit status 1.
/// </para>
/// </remarks>
/// <param name="store">The content store the file is read from.</param>
/// <param name="command">The accepted <c>scp -f</c> command.</param>
/// <param name="context">The exchange: its log.</param>
internal sealed class ScpDownloadHandler(ContentStore store, SshScpCommand command, ExchangeContext context) : ISshChannelHandler
{
    /// <summary>The most bytes read from the store for one channel write.</summary>
    public const int ChunkBytes = 262144;

    private readonly string path = SshLogText.Render(Encoding.UTF8.GetBytes(command.Path));

    /// <inheritdoc/>
    public async Task<uint> RunAsync(ISshChannelDataStream channel, CancellationToken cancellationToken)
    {
        var reader = new ScpChannelReader(channel);
        if (!await IsAcknowledgedAsync(reader, "the start", cancellationToken))
        {
            return 1;
        }

        try
        {
            return await SendFileAsync(channel, reader, cancellationToken);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return await RefuseAsync(channel, "read error", failure.Message, cancellationToken);
        }
    }

    private async Task<uint> SendFileAsync(ISshChannelDataStream channel, ScpChannelReader reader, CancellationToken cancellationToken)
    {
        var mapping = SshContentPath.Map(store, Encoding.UTF8.GetBytes(command.Path), out var canonical);
        var status = mapping is null ? null : store.GetEntryStatus(mapping);
        if (status is null)
        {
            return await RefuseAsync(channel, "No such file or directory", "answered as absent", cancellationToken);
        }

        if (status.Kind == ContentEntryKind.Directory)
        {
            return await RefuseAsync(channel, "not a regular file", null, cancellationToken);
        }

        var length = status.Length!.Value;
        if (!await SendHeadersAsync(channel, reader, status.LastModifiedUtc, length, canonical!, cancellationToken))
        {
            return 1;
        }

        return await SendBytesAsync(channel, reader, mapping!, length, cancellationToken);
    }

    // The T line with -p, then the C line, each answered \0.
    private async Task<bool> SendHeadersAsync(
        ISshChannelDataStream channel,
        ScpChannelReader reader,
        DateTimeOffset lastWriteTime,
        long length,
        string canonical,
        CancellationToken cancellationToken)
    {
        if (command.PreservesTimes)
        {
            await channel.WriteAsync(ScpControlLine.Times(lastWriteTime), cancellationToken);
            if (!await IsAcknowledgedAsync(reader, "the T line", cancellationToken))
            {
                return false;
            }
        }

        await channel.WriteAsync(ScpControlLine.File(length, canonical[(canonical.LastIndexOf('/') + 1)..]), cancellationToken);

        return await IsAcknowledgedAsync(reader, "the C line", cancellationToken);
    }

    private async Task<uint> SendBytesAsync(
        ISshChannelDataStream channel,
        ScpChannelReader reader,
        ContentPathMapping mapping,
        long length,
        CancellationToken cancellationToken)
    {
        long sent = 0;
        while (sent < length)
        {
            var count = Math.Min(ChunkBytes, length - sent);
            using var chunk = new MemoryStream((int)count);
            var copied = await store.CopyFileBytesAsync(mapping, ContentByteRange.Select(length, sent, sent + count - 1), chunk, cancellationToken);
            await channel.WriteAsync(chunk.GetBuffer().AsMemory(0, (int)chunk.Length), cancellationToken);
            sent += copied;
            if (copied < count)
            {
                context.Log.Note($"SCP download {path}: sent {sent} of {length} bytes, the file shrank");

                return 1;
            }
        }

        await channel.WriteAsync(ScpReply.Ok, cancellationToken);
        await reader.ReadByteAsync(cancellationToken);
        context.Log.Note($"SCP download {path}: sent {sent} bytes");

        return 0;
    }

    private async Task<bool> IsAcknowledgedAsync(ScpChannelReader reader, string what, CancellationToken cancellationToken)
    {
        var reply = await reader.ReadByteAsync(cancellationToken);
        if (reply == 0)
        {
            return true;
        }

        var answer = reply < 0 ? "EOF" : $"byte 0x{reply:X2}";
        context.Log.Note($"SCP download {path} ended: the client answered {what} with {answer}");

        return false;
    }

    private async Task<uint> RefuseAsync(ISshChannelDataStream channel, string reason, string? detail, CancellationToken cancellationToken)
    {
        var because = detail is null ? string.Empty : ": " + detail;
        context.Log.Note($"SCP download {path} refused: {reason}{because}");
        await channel.WriteAsync(ScpReply.Error($"{path}: {reason}"), cancellationToken);

        return 1;
    }
}
