using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>~</c> set (RESP3): unordered distinct elements, written in the order given.
/// </summary>
public sealed class RespSet : RespValue
{
    private readonly RespValue[] elements;

    /// <summary>Creates a set of <paramref name="elements"/>, in order.</summary>
    /// <param name="elements">The elements; none may be null.</param>
    public RespSet(params IReadOnlyList<RespValue> elements)
    {
        this.elements = RespEncoding.RequireElements(elements, nameof(elements));
    }

    /// <summary>The elements, in the order they are written.</summary>
    public IReadOnlyList<RespValue> Elements => elements;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) => RespEncoding.WriteAggregate(output, (byte)'~', elements);
}
