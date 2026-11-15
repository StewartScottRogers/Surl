using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// One change a command made to the keyspace, journalled while the command runs and published as a
/// <see cref="StoreChangedEvent"/> once it has answered (ADR-0158 decision 2).
/// </summary>
/// <param name="Database">The database, 0 to 15.</param>
/// <param name="Key">The key, or <see langword="null"/> when the change is a whole database emptied (<c>FLUSHDB</c>).</param>
/// <param name="Change"><see cref="StoreChange.Written"/> or <see cref="StoreChange.Deleted"/>.</param>
/// <param name="ByteCount">The key's value bytes after a write, or <see langword="null"/> for a delete.</param>
internal sealed record RedisKeyspaceChange(int Database, string? Key, StoreChange Change, long? ByteCount)
{
    private const int MaxNameLength = 256;

    /// <summary>
    /// The text ADR-0158 decision 2 calls <c>Name</c>, and a fault rule's <c>path</c>:
    /// <c>&lt;database&gt;/&lt;key&gt;</c> with the key as UTF-8, each byte that is not valid UTF-8
    /// written <c>\xHH</c>, truncated at 256 characters; a whole database is its number alone.
    /// </summary>
    public string Name => Key is null ? Database.ToString(System.Globalization.CultureInfo.InvariantCulture) : NameOf(Database, Key);

    /// <summary>The name of the key <paramref name="key"/> in database <paramref name="database"/>.</summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="key">The key as the keyspace names it (Latin-1).</param>
    /// <returns>The name.</returns>
    public static string NameOf(int database, string key)
    {
        var bytes = RedisKeys.Bytes(key);
        var name = new StringBuilder().Append(database).Append('/');
        var remaining = (ReadOnlySpan<byte>)bytes;
        while (!remaining.IsEmpty && name.Length < MaxNameLength)
        {
            if (Rune.DecodeFromUtf8(remaining, out var rune, out var consumed) == System.Buffers.OperationStatus.Done)
            {
                name.Append(rune.ToString());
                remaining = remaining[consumed..];
            }
            else
            {
                name.Append("\\x").Append(remaining[0].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                remaining = remaining[1..];
            }
        }

        return name.Length > MaxNameLength ? name.ToString(0, MaxNameLength) : name.ToString();
    }
}
