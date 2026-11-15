namespace Surl.Extended.Protocol.Redis;

/// <summary>One database's keys as a snapshot holds them (ADR-0158 decision 5).</summary>
/// <param name="Entries">The live keys in creation order, each with a copy of its value and its expiry as an absolute deadline.</param>
/// <param name="LastSequenceNumber">The last <c>SCAN</c> sequence number the database gave, so a restored database goes on from it.</param>
internal sealed record RedisDatabaseSnapshot(IReadOnlyList<RedisKeyEntry> Entries, long LastSequenceNumber)
{
    /// <summary>The database holding nothing, as a reset leaves it.</summary>
    public static RedisDatabaseSnapshot Empty { get; } = new([], 0);
}
