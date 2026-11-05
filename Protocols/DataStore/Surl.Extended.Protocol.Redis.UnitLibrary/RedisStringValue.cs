namespace Surl.Extended.Protocol.Redis;

/// <summary>A string value: any bytes (the string commands' pages).</summary>
/// <param name="bytes">The bytes, kept as given and never changed.</param>
internal sealed class RedisStringValue(byte[] bytes) : RedisStoredValue
{
    /// <summary>The bytes.</summary>
    public byte[] Bytes { get; } = bytes;

    /// <inheritdoc/>
    public override string TypeName => "string";

    /// <inheritdoc/>
    public override long ByteCount => Bytes.Length;
}
