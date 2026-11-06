namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A value of another type than string, named <c>hash</c> but not a collection, for the string and
/// key commands' <c>-WRONGTYPE</c> and <c>TYPE</c> tests written before the hash, list and set
/// types existed (BL-490).
/// </summary>
internal sealed class UnitTestRedisValue : RedisStoredValue
{
    public override string TypeName => "hash";

    public override long ByteCount => 4;
}
