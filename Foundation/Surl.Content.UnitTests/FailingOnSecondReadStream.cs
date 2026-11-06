namespace Surl.Content;

/// <summary>
/// An upload source that yields its bytes on the first read and throws
/// <see cref="IOException"/> on the next, as a peer that drops the connection mid-upload.
/// </summary>
internal sealed class FailingOnSecondReadStream(byte[] firstReadBytes) : MemoryStream(firstReadBytes)
{
    private bool hasBeenRead;

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (hasBeenRead)
        {
            throw new IOException("The peer went away.");
        }

        hasBeenRead = true;
        return await base.ReadAsync(buffer, cancellationToken);
    }
}
