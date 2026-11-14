namespace Surl.Networking;

/// <summary>A readable stream whose reads fail the test: the transport must not be touched.</summary>
internal sealed class NeverReadyStream : MemoryStream
{
    public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("The transport was read.");

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The transport was read.");
}
