namespace Surl.Extended.Protocol.Coap;

/// <summary>An <see cref="ICoapRandomSource"/> that passes every draw through and remembers it.</summary>
internal sealed class RecordingCoapRandomSource(ICoapRandomSource inner) : ICoapRandomSource
{
    public List<ushort> MessageIds { get; } = [];

    public List<double> Fractions { get; } = [];

    public ushort DrawMessageId()
    {
        var id = inner.DrawMessageId();
        MessageIds.Add(id);
        return id;
    }

    public double DrawFraction()
    {
        var fraction = inner.DrawFraction();
        Fractions.Add(fraction);
        return fraction;
    }

    public void Fill(Span<byte> bytes) => inner.Fill(bytes);
}
