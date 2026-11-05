namespace Surl.Extended.Protocol.Redis;

/// <summary>A set value: distinct members, in the order they were added (ADR-0106 decision 1).</summary>
internal sealed class RedisSetValue : RedisCollectionValue
{
    private readonly OrderedDictionary<string, byte[]> members = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public override string TypeName => "set";

    /// <inheritdoc/>
    public override int Count => members.Count;

    /// <summary>Every member, in the order added.</summary>
    public IEnumerable<byte[]> Members => members.Values;

    /// <summary>Whether <paramref name="member"/> is in the set.</summary>
    /// <param name="member">The member's bytes.</param>
    /// <returns>Whether it is a member.</returns>
    public bool Contains(byte[] member) => members.ContainsKey(RedisKeys.Name(member));

    /// <summary>Adds <paramref name="member"/>.</summary>
    /// <param name="member">The member's bytes.</param>
    /// <returns>Whether it was not already a member.</returns>
    public bool Add(byte[] member)
    {
        if (!members.TryAdd(RedisKeys.Name(member), member))
        {
            return false;
        }

        CountBytes(member.Length);
        return true;
    }

    /// <summary>Removes <paramref name="member"/>.</summary>
    /// <param name="member">The member's bytes.</param>
    /// <returns>Whether it was a member.</returns>
    public bool Remove(byte[] member)
    {
        if (!members.Remove(RedisKeys.Name(member)))
        {
            return false;
        }

        CountBytes(-member.Length);
        return true;
    }

    /// <summary>
    /// Removes and returns the member added first: the <c>SPOP</c> page picks members at random,
    /// which no client can tell from this fixed choice, and a fixed one lets tests pin bytes.
    /// </summary>
    /// <returns>The member; the set must not be empty.</returns>
    public byte[] RemoveFirst()
    {
        var member = members.GetAt(0).Value;
        Remove(member);
        return member;
    }
}
