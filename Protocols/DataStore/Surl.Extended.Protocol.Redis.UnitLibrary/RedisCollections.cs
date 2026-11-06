namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Reads and changes the hash, list or set at a key for a command (ADR-0106 decision 1): a key
/// holding another type is <c>-WRONGTYPE …</c>, and a collection a change empties loses its key.
/// </summary>
internal static class RedisCollections
{
    /// <summary>
    /// The collection at <paramref name="key"/>: <see langword="null"/> when absent, and
    /// <paramref name="isWrongType"/> set when the key holds another type.
    /// </summary>
    /// <typeparam name="TValue">The collection type the command works on.</typeparam>
    /// <param name="call">The command.</param>
    /// <param name="key">The key's bytes.</param>
    /// <param name="isWrongType">Whether the key holds another type.</param>
    /// <returns>The collection, or <see langword="null"/>.</returns>
    public static TValue? Find<TValue>(RedisCommandCall call, byte[] key, out bool isWrongType)
        where TValue : RedisCollectionValue
    {
        var value = call.Database.Find(RedisKeys.Name(key), call.NowMilliseconds)?.Value;
        isWrongType = value is not null and not TValue;
        return value as TValue;
    }

    /// <summary>
    /// Answers a command that reads the collection at the command's key, its first argument:
    /// <c>-WRONGTYPE …</c> for another type, else <paramref name="answer"/> of the collection or
    /// <see langword="null"/> when the key is absent.
    /// </summary>
    /// <typeparam name="TValue">The collection type.</typeparam>
    /// <param name="call">The command.</param>
    /// <param name="answer">Answers from the collection, or from <see langword="null"/>.</param>
    /// <returns>The reply.</returns>
    public static RespValue Read<TValue>(RedisCommandCall call, Func<TValue?, RespValue> answer)
        where TValue : RedisCollectionValue
    {
        var value = Find<TValue>(call, call.Arguments[1], out var isWrongType);
        return isWrongType ? RedisErrorReplies.WrongType : answer(value);
    }

    /// <summary>
    /// Answers a command that changes the collection at the command's key, its first argument:
    /// <c>-WRONGTYPE …</c> for another type; <paramref name="answerWhenAbsent"/> for an absent key,
    /// or, when that is <see langword="null"/>, a new empty collection created for the change.
    /// After the change the keyspace is recounted, and an emptied collection loses its key.
    /// </summary>
    /// <typeparam name="TValue">The collection type.</typeparam>
    /// <param name="call">The command.</param>
    /// <param name="answerWhenAbsent">The reply for an absent key, or <see langword="null"/> to create one.</param>
    /// <param name="change">Changes the collection and answers.</param>
    /// <returns>The reply.</returns>
    public static RespValue Change<TValue>(RedisCommandCall call, RespValue? answerWhenAbsent, Func<TValue, RespValue> change)
        where TValue : RedisCollectionValue, new()
    {
        var name = RedisKeys.Name(call.Arguments[1]);
        var entry = call.Database.Find(name, call.NowMilliseconds);
        if (entry is null)
        {
            if (answerWhenAbsent is not null)
            {
                return answerWhenAbsent;
            }

            entry = call.Database.Put(name, new TValue(), call.NowMilliseconds);
        }

        if (entry.Value is not TValue value)
        {
            return RedisErrorReplies.WrongType;
        }

        var previousByteCount = value.ByteCount;
        var reply = change(value);
        call.Database.Recount(entry, previousByteCount);
        return reply;
    }

    /// <summary>Bulk strings of <paramref name="items"/>, in order.</summary>
    /// <param name="items">The bytes.</param>
    /// <returns>The bulk strings.</returns>
    public static RespValue[] BulkStrings(IEnumerable<byte[]> items) => [.. items.Select(item => new RespBulkString(item))];
}
