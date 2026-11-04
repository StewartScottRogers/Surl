using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Gopher;

/// <summary>
/// Writes the reply to one selector, as <see cref="GopherProtocolServer"/> describes, and
/// closes the connection after it.
/// </summary>
/// <param name="connection">Where the reply is written.</param>
/// <param name="context">The exchange: its log, listen URL and cancellation.</param>
/// <param name="contentStore">Where the selector is looked up.</param>
/// <param name="logLabel">How the log names the selector, already rendered safe.</param>
internal sealed class GopherSelectorResponder(IConnection connection, ExchangeContext context, ContentStore contentStore, string logLabel)
{
    /// <summary>
    /// Answers the selector whose request path is <paramref name="requestPath"/>.
    /// </summary>
    /// <param name="requestPath">The selector as a request path, from <see cref="GopherSelector.ToRequestPath"/>.</param>
    /// <returns>A task that completes once the reply is written and the connection half-closed, or aborted.</returns>
    public Task AnswerAsync(string requestPath)
    {
        var mapping = contentStore.MapRequestPath(requestPath);

        return mapping.EntryKind switch
        {
            ContentEntryKind.File => SendFileAsync(mapping),
            ContentEntryKind.Directory => SendMenuAsync(requestPath, mapping),
            _ when !mapping.IsMapped => SendNothingServedHereAsync($"refused by the content store ({mapping.Refusal})"),
            _ => SendNothingServedHereAsync($"nothing exists at {mapping.Location}"),
        };
    }

    private async Task SendFileAsync(ContentPathMapping mapping)
    {
        if (contentStore.GetFileStatus(mapping) is not { } status)
        {
            await SendNothingServedHereAsync($"{mapping.Location} vanished before it was read");
            return;
        }

        context.Log.Note($"{logLabel}: {status.Length} bytes of {mapping.Location}");
        await using var destination = new ConnectionWriteStream(connection);
        var copied = await contentStore.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(status.Length), destination, context.CancellationToken);
        if (copied < status.Length)
        {
            context.Log.Note($"{mapping.Location} shrank to {copied} bytes while it was sent; the connection was aborted.");
            connection.Abort();
            return;
        }

        await connection.CompleteWritesAsync(context.CancellationToken);
    }

    private async Task SendMenuAsync(string requestPath, ContentPathMapping mapping)
    {
        var listing = contentStore.ListDirectory(mapping, context.CancellationToken);
        if (!listing.IsListed)
        {
            await SendNothingServedHereAsync(contentStore.ExposureOptions.ListDirectories
                ? $"{mapping.Location} was no longer a directory when it was listed"
                : $"directory listings are off, so {mapping.Location} is answered as absent");
            return;
        }

        context.Log.Note($"{logLabel}: a menu of the {listing.Entries.Count} entries of {mapping.Location}");
        await SendAndCloseAsync(GopherMenu.ForDirectory(requestPath, listing.Entries, context));
    }

    private Task SendNothingServedHereAsync(string why)
    {
        context.Log.Note($"{logLabel}: error menu, {why}");

        return SendAndCloseAsync(GopherMenu.NothingServedHere);
    }

    private async Task SendAndCloseAsync(byte[] reply)
    {
        await connection.WriteAsync(reply, context.CancellationToken);
        await connection.CompleteWritesAsync(context.CancellationToken);
    }
}
