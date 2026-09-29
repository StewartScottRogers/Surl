namespace Surl.Content;

/// <summary>
/// A destination stream that keeps what is written to it and cancels a token source on the
/// first write, so a test can cancel a copy while it is under way.
/// </summary>
internal sealed class CancellingOnWriteStream(CancellationTokenSource cancellation) : MemoryStream
{
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await base.WriteAsync(buffer, cancellationToken);
        await cancellation.CancelAsync();
    }
}
