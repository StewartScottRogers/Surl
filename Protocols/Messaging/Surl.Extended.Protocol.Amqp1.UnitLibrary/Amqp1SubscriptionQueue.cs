using System.Security.Cryptography;
using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// A topic subscription held in a bus named queue bound to the topic filter (ADR-0146 decision 3 and
/// Amendment 4): either one the container keeps while no link is attached - a client receiver's topic source
/// asking <c>durable</c> above none with an <c>expiry-policy</c> other than <c>link-detach</c> - or a
/// non-durable shared one, which goes with its last link. This is the queue's name, flags and description and
/// what the container answers.
/// </summary>
/// <param name="QueueName">The bus queue: <see cref="QueueNamePrefix"/>, or <see cref="NonDurableSharedQueueNamePrefix"/> for a non-durable shared one, and the hex SHA-256 of the key.</param>
/// <param name="Description">The subscription description the bus keeps beside the queue (ADR-0146 decision 3.2).</param>
/// <param name="TopicFilter">The canonical topic filter the queue is bound to.</param>
/// <param name="ClientAddress">The address the client's source named.</param>
/// <param name="Filters">The applied filter entries, as the container echoes them; <see langword="null"/> for none.</param>
/// <param name="ExpiryPolicy">The policy applied: <c>never</c>, <c>session-end</c> or <c>connection-close</c> for a kept one, <c>link-detach</c> for a non-durable shared one.</param>
/// <param name="TimeoutSeconds">The <c>timeout</c> answered.</param>
/// <param name="IsShared">Whether several links share it.</param>
/// <param name="IsGlobal">Whether the sharing crosses containers.</param>
internal sealed record Amqp1SubscriptionQueue(
    string QueueName,
    string Description,
    string TopicFilter,
    string ClientAddress,
    Amqp1Map? Filters,
    Amqp1Symbol ExpiryPolicy,
    uint TimeoutSeconds,
    bool IsShared,
    bool IsGlobal)
{
    /// <summary>The prefix of every subscription's queue name; a kept one's is this and 64 hex digits.</summary>
    public const string QueueNamePrefix = "surl-subscription-";

    /// <summary>The prefix of a non-durable shared subscription's queue name, before 64 hex digits.</summary>
    public const string NonDurableSharedQueueNamePrefix = QueueNamePrefix + "nondurable-";

    private const char SharedNameSeparator = '|';

    private static readonly Amqp1Symbol NeverPolicy = new("never");

    private static readonly Amqp1Symbol SessionEndPolicy = new("session-end");

    private static readonly Amqp1Symbol ConnectionClosePolicy = new("connection-close");

    private static readonly Amqp1Symbol LinkDetachPolicy = new("link-detach");

    private static readonly Amqp1Symbol SharedCapability = new("shared");

    private static readonly Amqp1Symbol GlobalCapability = new("global");

    /// <summary>Whether a topic source asks for a subscription held in a queue: a kept one, or a shared one.</summary>
    public static bool IsAskedFor(Amqp1Terminus terminus) =>
        terminus.IsShared || AsksToBeKept(terminus);

    /// <summary>Whether the container keeps the subscription while no link is attached; otherwise the bus removes it with its last link.</summary>
    public bool IsKept => ExpiryPolicy != LinkDetachPolicy;

    /// <summary>Whether the subscription lasts until the container or its files go (<c>never</c>), rather than to the end of a session, connection or last link.</summary>
    public bool IsPersistent => ExpiryPolicy == NeverPolicy;

    /// <summary>Whether the subscription ends with the session that held it.</summary>
    public bool EndsWithSession => ExpiryPolicy == SessionEndPolicy;

    /// <summary>The queue's flags: durable when persistent, otherwise exclusive when kept, and auto-delete when not.</summary>
    public BusQueueFlags QueueFlags =>
        !IsKept ? new BusQueueFlags(AutoDelete: true)
        : IsPersistent ? new BusQueueFlags(Durable: true)
        : new BusQueueFlags(Exclusive: true);

    /// <summary>The <c>capabilities</c> the container applied.</summary>
    public IReadOnlyList<Amqp1Symbol> Capabilities => IsShared ? (IsGlobal ? [SharedCapability, GlobalCapability] : [SharedCapability]) : [];

    /// <summary>Plans the subscription a terminus asks for.</summary>
    /// <param name="clientContainerId">The <c>container-id</c> of the client's <c>open</c>.</param>
    /// <param name="linkName">The link's <c>name</c>.</param>
    /// <param name="terminus">What the client's source asked.</param>
    /// <param name="topicFilter">The canonical topic filter its address maps to.</param>
    /// <param name="filters">The applied filter entries.</param>
    public static Amqp1SubscriptionQueue Plan(string clientContainerId, string linkName, Amqp1Terminus terminus, string topicFilter, Amqp1Map? filters)
    {
        var isKept = AsksToBeKept(terminus);
        var policy = isKept ? PolicyOf(terminus.ExpiryPolicy) : LinkDetachPolicy;
        var isGlobal = terminus.IsShared && terminus.IsGlobal;
        var plan = new Amqp1SubscriptionQueue(
            isKept
                ? QueueNameFor(clientContainerId, linkName, terminus.IsShared, isGlobal)
                : NonDurableSharedQueueNameFor(clientContainerId, linkName, isGlobal),
            string.Empty,
            topicFilter,
            terminus.Address!,
            filters,
            policy,
            policy == NeverPolicy ? terminus.TimeoutSeconds : 0,
            terminus.IsShared,
            isGlobal);
        return plan with { Description = plan.DescribeSubscription() };
    }

    /// <summary>The name of the queue holding the kept subscription a link of this name makes.</summary>
    /// <param name="clientContainerId">The client's <c>container-id</c>.</param>
    /// <param name="linkName">The link's <c>name</c>; a shared one is cut before its first <c>|</c>.</param>
    /// <param name="isShared">Whether the subscription is shared.</param>
    /// <param name="isGlobal">Whether it is shared across containers, so the <c>container-id</c> is left out.</param>
    public static string QueueNameFor(string clientContainerId, string linkName, bool isShared, bool isGlobal) =>
        QueueNamePrefix + HashKey(clientContainerId, linkName, isShared, isGlobal);

    // ADR-0146 Amendment 4 item 3: the shared key under another prefix, so no kept subscription's name equals it.
    private static string NonDurableSharedQueueNameFor(string clientContainerId, string linkName, bool isGlobal) =>
        NonDurableSharedQueueNamePrefix + HashKey(clientContainerId, linkName, isShared: true, isGlobal);

    /// <summary>The queue names a link of this name could hold a kept subscription under, whichever way it was shared.</summary>
    public static IReadOnlyList<string> QueueNamesFor(string clientContainerId, string linkName) =>
        [.. new[] { (false, false), (true, false), (true, true) }
            .Select(sharing => QueueNameFor(clientContainerId, linkName, sharing.Item1, sharing.Item2))
            .Distinct(StringComparer.Ordinal)];

    /// <summary>Reads a subscription back from the description the bus kept.</summary>
    /// <param name="queueName">The queue the description was kept beside.</param>
    /// <param name="description">The description.</param>
    /// <returns>The subscription; <see langword="null"/> when the text is not one this container wrote.</returns>
    public static Amqp1SubscriptionQueue? FromDescription(string queueName, string? description)
    {
        try
        {
            var bytes = Convert.FromHexString(description ?? string.Empty);
            return new Amqp1Decoder(bytes).ReadValue() is IReadOnlyList<object?> list
                && list is [string address, string filter, var filters, string policy, bool shared, bool global]
                && (filters is null or Amqp1Map)
                ? new(queueName, description!, filter, address, (Amqp1Map?)filters, new(policy), 0, shared, global)
                : null;
        }
        catch (Exception exception) when (exception is FormatException or Amqp1DecodeException)
        {
            return null;
        }
    }

    // ADR-0146 decision 3.1: durable above none, with a policy that outlasts the link.
    private static bool AsksToBeKept(Amqp1Terminus terminus) =>
        terminus.Durable > 0 && terminus.ExpiryPolicy != LinkDetachPolicy.Value;

    // ADR-0146 decisions 3.1 and 3.5: the hex SHA-256 of the container-id, a zero byte and the link name,
    // a shared name cut before its first "|" and a global one with no container-id.
    private static string HashKey(string clientContainerId, string linkName, bool isShared, bool isGlobal)
    {
        var name = isShared && linkName.IndexOf(SharedNameSeparator, StringComparison.Ordinal) is var cut and >= 0 ? linkName[..cut] : linkName;
        var container = isShared && isGlobal ? string.Empty : clientContainerId;
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(container + '\0' + name)));
    }

    // The client's address, the canonical filter, the applied filters and the policy and sharing applied:
    // a change of any is a different subscription.
    private string DescribeSubscription() => Convert.ToHexStringLower(
        Amqp1Encoder.Encode(new object?[] { ClientAddress, TopicFilter, Filters, ExpiryPolicy.Value, IsShared, IsGlobal }));

    private static Amqp1Symbol PolicyOf(string? asked) => asked switch
    {
        "never" => NeverPolicy,
        "connection-close" => ConnectionClosePolicy,
        _ => SessionEndPolicy,
    };
}
