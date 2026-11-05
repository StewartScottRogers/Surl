namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A value of another type than string, for the string and key commands' <c>-WRONGTYPE</c> and
/// <c>TYPE</c> answers before the hash, list and set types exist (BL-491).
/// </summary>
internal sealed class UnitTestRedisValue : RedisStoredValue
{
    public override string TypeName => "hash";

    public override long ByteCount => 4;
}
