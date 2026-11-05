using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>*</c> array (RESP2 and RESP3): elements of any type, nested arrays included.
/// </summary>
public sealed class RespArray : RespValue
{
    private readonly RespValue[] elements;

    /// <summary>Creates a array of <paramref name="elements"/>, in order.</summary>
    /// <param name="elements">The elements; none may be null.</param>
    public RespArray(params IReadOnlyList<RespValue> elements)
    {
        this.elements = RespEncoding.RequireElements(elements, nameof(elements));
    }

    /// <summary>The elements, in the order they are written.</summary>
    public IReadOnlyList<RespValue> Elements => elements;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) => RespEncoding.WriteAggregate(output, (byte)'*', elements);
}
