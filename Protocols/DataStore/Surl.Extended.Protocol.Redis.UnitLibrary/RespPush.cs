using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>&gt;</c> push (RESP3): out-of-band data such as a pub/sub message, its first element the kind.
/// </summary>
public sealed class RespPush : RespValue
{
    private readonly RespValue[] elements;

    /// <summary>Creates a push of <paramref name="elements"/>, in order.</summary>
    /// <param name="elements">The elements; none may be null.</param>
    public RespPush(params IReadOnlyList<RespValue> elements)
    {
        this.elements = RespEncoding.RequireElements(elements, nameof(elements));
    }

    /// <summary>The elements, in the order they are written.</summary>
    public IReadOnlyList<RespValue> Elements => elements;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) => RespEncoding.WriteAggregate(output, (byte)'>', elements);
}
