namespace Surl.Extended.Protocol.Redis;

/// <summary>A hash value: fields, each with a value, in the order they were added (ADR-0106 decision 1).</summary>
internal sealed class RedisHashValue : RedisCollectionValue
{
    private readonly OrderedDictionary<string, byte[]> fields = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public override string TypeName => "hash";

    /// <inheritdoc/>
    public override int Count => fields.Count;

    /// <summary>Every field (one character per byte) and its value, in the order the fields were added.</summary>
    public IEnumerable<KeyValuePair<string, byte[]>> Fields => fields;

    /// <summary>The value of <paramref name="field"/>, or <see langword="null"/>.</summary>
    /// <param name="field">The field, one character per byte.</param>
    /// <returns>The value, or <see langword="null"/> when the field is absent.</returns>
    public byte[]? Get(string field) => fields.GetValueOrDefault(field);

    /// <summary>Sets <paramref name="field"/> to <paramref name="value"/>; a present field keeps its place.</summary>
    /// <param name="field">The field, one character per byte.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the field was added rather than replaced.</returns>
    public bool Set(string field, byte[] value)
    {
        if (fields.TryGetValue(field, out var old))
        {
            CountBytes(value.Length - old.Length);
            fields[field] = value;
            return false;
        }

        CountBytes(field.Length + value.Length);
        fields.Add(field, value);
        return true;
    }

    /// <summary>Removes <paramref name="field"/>.</summary>
    /// <param name="field">The field, one character per byte.</param>
    /// <returns>Whether the field was present.</returns>
    public bool Remove(string field)
    {
        if (!fields.Remove(field, out var old))
        {
            return false;
        }

        CountBytes(-field.Length - old.Length);
        return true;
    }
}
