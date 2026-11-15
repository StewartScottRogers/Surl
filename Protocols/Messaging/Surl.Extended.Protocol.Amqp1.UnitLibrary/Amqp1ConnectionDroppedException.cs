namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Thrown inside <see cref="Amqp1ContainerConnection"/> when a <c>drop</c> fault rule has aborted the
/// connection: whatever the performative was going to answer is not sent, and serving ends.
/// </summary>
internal sealed class Amqp1ConnectionDroppedException : Exception
{
    /// <summary>Creates the exception for a connection a fault rule dropped.</summary>
    public Amqp1ConnectionDroppedException()
        : base("A fault rule dropped the connection.")
    {
    }
}
