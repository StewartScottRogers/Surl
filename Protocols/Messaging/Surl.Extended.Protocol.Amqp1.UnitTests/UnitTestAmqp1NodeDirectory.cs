using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// A node directory that records each request and answers with the resolution a test gives; by
/// default every node is found, a dynamic one named <see cref="DynamicAddress"/>. A queue it finds is
/// declared on the connection's bus, so a link from it can consume; a resolution the test gives declares nothing.
/// </summary>
internal sealed class UnitTestAmqp1NodeDirectory(Func<Amqp1NodeRequest, Amqp1NodeResolution>? resolve = null) : IAmqp1NodeDirectory
{
    public const string DynamicAddress = "/queue/surl-dynamic-00112233445566778899aabbccddeeff";

    private BusConnection? busConnection;

    public List<Amqp1NodeRequest> Requests { get; } = [];

    /// <summary>Binds the directory to the connection's place on the bus, as the container's factory does.</summary>
    public IAmqp1NodeDirectory On(BusConnection connection)
    {
        busConnection = connection;
        return this;
    }

    public Amqp1NodeResolution Resolve(Amqp1NodeRequest request)
    {
        Requests.Add(request);
        if (resolve is not null)
        {
            return resolve(request);
        }

        var address = request.CanonicalAddress ?? DynamicAddress;
        if (!Amqp1NodeAddress.IsTopic(address))
        {
            busConnection?.DeclareQueue(address[Amqp1NodeAddress.QueuePrefix.Length..]);
        }

        return Amqp1NodeResolution.Found(address);
    }
}
