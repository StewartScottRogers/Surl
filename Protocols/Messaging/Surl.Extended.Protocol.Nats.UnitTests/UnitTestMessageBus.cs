namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>Makes the bus a unit test's server shares, on the system clock unless one is given.</summary>
internal static class UnitTestMessageBus
{
    public static Surl.MessageBus.MessageBus Create(TimeProvider? clock = null) => new(clock ?? TimeProvider.System);
}
