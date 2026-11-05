namespace Surl.Extended.Protocol.Redis;

/// <summary>A list value: elements in order, the head at index 0 (ADR-0106 decision 1).</summary>
internal sealed class RedisListValue : RedisCollectionValue
{
    private readonly List<byte[]> elements = [];

    /// <inheritdoc/>
    public override string TypeName => "list";

    /// <inheritdoc/>
    public override int Count => elements.Count;

    /// <summary>The element at <paramref name="index"/>, from 0 at the head.</summary>
    /// <param name="index">The index, within the list.</param>
    public byte[] this[int index] => elements[index];

    /// <summary>Adds <paramref name="element"/> at the head or the tail.</summary>
    /// <param name="element">The element.</param>
    /// <param name="atHead">Whether it goes at the head (<c>LPUSH</c>) rather than the tail (<c>RPUSH</c>).</param>
    public void Push(byte[] element, bool atHead)
    {
        elements.Insert(atHead ? 0 : elements.Count, element);
        CountBytes(element.Length);
    }

    /// <summary>Replaces the element at <paramref name="index"/>.</summary>
    /// <param name="index">The index, within the list.</param>
    /// <param name="element">The new element.</param>
    public void Replace(int index, byte[] element)
    {
        CountBytes(element.Length - elements[index].Length);
        elements[index] = element;
    }

    /// <summary>Removes and returns the element at <paramref name="index"/>.</summary>
    /// <param name="index">The index, within the list.</param>
    /// <returns>The element.</returns>
    public byte[] RemoveAt(int index)
    {
        var element = elements[index];
        elements.RemoveAt(index);
        CountBytes(-element.Length);
        return element;
    }

    /// <summary>The elements from <paramref name="start"/> to <paramref name="stop"/>, both included.</summary>
    /// <param name="start">The first index, within the list.</param>
    /// <param name="stop">The last index, within the list and at or after <paramref name="start"/>.</param>
    /// <returns>The elements, in order.</returns>
    public IReadOnlyList<byte[]> Range(int start, int stop) => elements.GetRange(start, stop - start + 1);

    /// <summary>Keeps only the elements from <paramref name="start"/> to <paramref name="stop"/>, both included.</summary>
    /// <param name="start">The first index kept, within the list.</param>
    /// <param name="stop">The last index kept, within the list and at or after <paramref name="start"/>.</param>
    public void Keep(int start, int stop)
    {
        RemoveRange(stop + 1, elements.Count - stop - 1);
        RemoveRange(0, start);
    }

    /// <summary>
    /// Removes elements equal to <paramref name="element"/>, as <c>LREM</c>'s page says: the first
    /// <paramref name="count"/> from the head when it is positive, the last -<paramref name="count"/>
    /// from the tail when negative, and every one when 0.
    /// </summary>
    /// <param name="element">The element to remove.</param>
    /// <param name="count">How many, and from which end.</param>
    /// <returns>How many were removed.</returns>
    public int RemoveMatching(byte[] element, long count)
    {
        var matching = Enumerable.Range(0, elements.Count).Where(index => elements[index].AsSpan().SequenceEqual(element));
        var limit = count == 0 ? int.MaxValue : (int)Math.Min(Math.Abs(Math.Max(count, -long.MaxValue)), int.MaxValue);
        var chosen = (count < 0 ? matching.Reverse() : matching).Take(limit).OrderDescending().ToList();
        foreach (var index in chosen)
        {
            RemoveAt(index);
        }

        return chosen.Count;
    }

    /// <summary>Removes every element.</summary>
    public void Clear() => RemoveRange(0, elements.Count);

    private void RemoveRange(int index, int count)
    {
        CountBytes(-elements.GetRange(index, count).Sum(element => (long)element.Length));
        elements.RemoveRange(index, count);
    }
}
