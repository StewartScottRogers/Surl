namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The fields <c>connection.tune</c> (10.30) and <c>connection.tune-ok</c> (10.31) share
/// (ADR-0112 decision 3).
/// </summary>
/// <param name="ChannelMax">The highest channel number; 0 means no limit.</param>
/// <param name="FrameMax">The largest frame, header and frame-end included; 0 means no limit.</param>
/// <param name="Heartbeat">The heartbeat interval in seconds; 0 means no heartbeats.</param>
public sealed record Amqp0ConnectionTuning(ushort ChannelMax, uint FrameMax, ushort Heartbeat)
{
    /// <summary>Reads <c>tune</c> or <c>tune-ok</c> arguments.</summary>
    /// <param name="arguments">The arguments after the class and method IDs.</param>
    /// <returns>The fields.</returns>
    /// <exception cref="Amqp0SyntaxException">The arguments do not decode.</exception>
    public static Amqp0ConnectionTuning Read(ReadOnlyMemory<byte> arguments)
    {
        var reader = new Amqp0WireReader(arguments);
        var channelMax = reader.ReadShort();
        var frameMax = reader.ReadLong();
        return new(channelMax, frameMax, reader.ReadShort());
    }

    /// <summary>The fields as <c>connection.tune</c>.</summary>
    /// <returns>The method.</returns>
    public Amqp0MethodPayload ToTuneMethod()
    {
        var writer = new Amqp0WireWriter();
        writer.WriteShort(ChannelMax);
        writer.WriteLong(FrameMax);
        writer.WriteShort(Heartbeat);
        return new(Amqp0ConnectionMethodId.ClassId, Amqp0ConnectionMethodId.Tune, writer.ToArray());
    }
}
