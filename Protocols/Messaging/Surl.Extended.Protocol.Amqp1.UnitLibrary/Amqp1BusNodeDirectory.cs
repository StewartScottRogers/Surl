using System.Security.Cryptography;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The container's nodes on the shared message bus (ADR-0107 decision 4; ADR-0100 decision 3): a
/// topic is the bus topic its address maps to through <see cref="MessageBus.MessageBus.Names"/>, a queue is the
/// bus queue of that name, declared durable on first attach, and a dynamic node is a queue exclusive to
/// the connection and deleted with its last consumer. Access is ADR-0102 decision 3.4's.
/// </summary>
/// <param name="bus">The process's message bus.</param>
/// <param name="connection">The connection's place on the bus; its account is the caller.</param>
public sealed class Amqp1BusNodeDirectory(MessageBus.MessageBus bus, BusConnection connection) : IAmqp1NodeDirectory
{
    /// <summary>The scheme whose names the bus maps for the container.</summary>
    public const string Scheme = "amqp1";

    /// <summary>The prefix of a dynamic node's queue name, before 32 hex digits.</summary>
    public const string DynamicQueuePrefix = "surl-dynamic-";

    private const string QueueAccessLevel = "$queue/";

    private static readonly BusQueueFlags DeclaredQueueFlags = new(Durable: true);

    private static readonly BusQueueFlags DynamicQueueFlags = new(Exclusive: true, AutoDelete: true);

    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    private readonly BusConnection connection = connection ?? throw new ArgumentNullException(nameof(connection));

    /// <inheritdoc/>
    /// <remarks>
    /// A client receiver's topic access is the subscription's own check; every other access is judged
    /// here, before anything that would tell whether the node exists.
    /// </remarks>
    public Amqp1NodeResolution Resolve(Amqp1NodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CanonicalAddress is not { } address)
        {
            return DeclareDynamicQueue();
        }

        return Amqp1NodeAddress.IsTopic(address)
            ? ResolveTopic(address, request.ClientIsReceiver)
            : ResolveQueue(address[Amqp1NodeAddress.QueuePrefix.Length..], request.ClientIsReceiver);
    }

    private Amqp1NodeResolution DeclareDynamicQueue()
    {
        var name = DynamicQueuePrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        return connection.DeclareQueue(name, DynamicQueueFlags).Outcome == MessageBusOutcome.Succeeded
            ? Amqp1NodeResolution.Found(Amqp1NodeAddress.QueuePrefix + name)
            : Amqp1NodeResolution.ResourceLimitExceeded;
    }

    private Amqp1NodeResolution ResolveTopic(string address, bool clientIsReceiver)
    {
        var mapping = clientIsReceiver ? bus.Names.ToCanonicalFilter(Scheme, address) : bus.Names.ToCanonicalTopic(Scheme, address);
        if (mapping.Name is not { } topic)
        {
            return Amqp1NodeResolution.NotFound;
        }

        return clientIsReceiver || bus.GetTopicAccess(connection.Account, topic).HasFlag(TopicAccessRights.Publish)
            ? Amqp1NodeResolution.Found(Amqp1NodeAddress.TopicPrefix + topic)
            : Amqp1NodeResolution.Unauthorized;
    }

    private Amqp1NodeResolution ResolveQueue(string queue, bool clientIsReceiver)
    {
        var right = clientIsReceiver ? TopicAccessRights.Subscribe : TopicAccessRights.Publish;
        if (!bus.GetTopicAccess(connection.Account, QueueAccessLevel + queue).HasFlag(right))
        {
            return Amqp1NodeResolution.Unauthorized;
        }

        // A queue declared with other flags, or exclusive to another connection, still exists: the
        // consume or the send meets its own refusal.
        return connection.DeclareQueue(queue, DeclaredQueueFlags).Outcome == MessageBusOutcome.TooManyQueues
            ? Amqp1NodeResolution.ResourceLimitExceeded
            : Amqp1NodeResolution.Found(Amqp1NodeAddress.QueuePrefix + queue);
    }
}
