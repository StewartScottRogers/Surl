using System.Security.Cryptography;
using System.Text;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// A subscription the container keeps while no link is attached (ADR-0146 decision 3): a client receiver's
/// topic source asking <c>durable</c> above none with an <c>expiry-policy</c> other than <c>link-detach</c>.
/// It is a bus named queue bound to the topic filter; this is its name, its description and what the
/// container answers.
/// </summary>
/// <param name="QueueName">The bus queue: <see cref="QueueNamePrefix"/> and the hex SHA-256 of the key.</param>
/// <param name="Description">The subscription description the bus keeps beside the queue (ADR-0146 decision 3.2).</param>
/// <param name="TopicFilter">The canonical topic filter the queue is bound to.</param>
/// <param name="ClientAddress">The address the client's source named.</param>
/// <param name="Filters">The applied filter entries, as the container echoes them; <see langword="null"/> for none.</param>
/// <param name="ExpiryPolicy">The policy applied: <c>never</c>, <c>session-end</c> or <c>connection-close</c>.</param>
/// <param name="TimeoutSeconds">The <c>timeout</c> answered.</param>
/// <param name="IsShared">Whether several links share it.</param>
/// <param name="IsGlobal">Whether the sharing crosses containers.</param>
internal sealed record Amqp1KeptSubscription(
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
    /// <summary>The prefix of a subscription's queue name, before 64 hex digits.</summary>
    public const string QueueNamePrefix = "surl-subscription-";

    private const char SharedNameSeparator = '|';

    private static readonly Amqp1Symbol NeverPolicy = new("never");

    private static readonly Amqp1Symbol SessionEndPolicy = new("session-end");

    private static readonly Amqp1Symbol ConnectionClosePolicy = new("connection-close");

    private static readonly Amqp1Symbol SharedCapability = new("shared");

    private static readonly Amqp1Symbol GlobalCapability = new("global");

    /// <summary>Whether the terminus asks for a kept subscription: <c>durable</c> above none, and not <c>link-detach</c>.</summary>
    public static bool IsAskedFor(Amqp1Terminus terminus) =>
        terminus.Durable > 0 && terminus.ExpiryPolicy != "link-detach" && !terminus.IsDynamic;

    /// <summary>Whether the subscription lasts until the container or its files go (<c>never</c>), rather than to the end of a session or connection.</summary>
    public bool IsPersistent => ExpiryPolicy == NeverPolicy;

    /// <summary>Whether the subscription ends with the session that held it.</summary>
    public bool EndsWithSession => ExpiryPolicy == SessionEndPolicy;

    /// <summary>The <c>capabilities</c> the container applied.</summary>
    public IReadOnlyList<Amqp1Symbol> Capabilities => IsShared ? (IsGlobal ? [SharedCapability, GlobalCapability] : [SharedCapability]) : [];

    /// <summary>Plans the subscription a terminus asks for.</summary>
    /// <param name="clientContainerId">The <c>container-id</c> of the client's <c>open</c>.</param>
    /// <param name="linkName">The link's <c>name</c>.</param>
    /// <param name="terminus">What the client's source asked.</param>
    /// <param name="topicFilter">The canonical topic filter its address maps to.</param>
    /// <param name="filters">The applied filter entries.</param>
    public static Amqp1KeptSubscription Plan(string clientContainerId, string linkName, Amqp1Terminus terminus, string topicFilter, Amqp1Map? filters)
    {
        var policy = PolicyOf(terminus.ExpiryPolicy);
        var isGlobal = terminus.IsShared && terminus.IsGlobal;
        var plan = new Amqp1KeptSubscription(
            QueueNameFor(clientContainerId, linkName, terminus.IsShared, isGlobal),
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

    /// <summary>The name of the queue holding the subscription a link of this name makes.</summary>
    /// <param name="clientContainerId">The client's <c>container-id</c>.</param>
    /// <param name="linkName">The link's <c>name</c>; a shared one is cut before its first <c>|</c>.</param>
    /// <param name="isShared">Whether the subscription is shared.</param>
    /// <param name="isGlobal">Whether it is shared across containers, so the <c>container-id</c> is left out.</param>
    public static string QueueNameFor(string clientContainerId, string linkName, bool isShared, bool isGlobal)
    {
        var name = isShared && linkName.IndexOf(SharedNameSeparator, StringComparison.Ordinal) is var cut and >= 0 ? linkName[..cut] : linkName;
        var container = isShared && isGlobal ? string.Empty : clientContainerId;
        var key = Encoding.UTF8.GetBytes(container + '\0' + name);
        return QueueNamePrefix + Convert.ToHexStringLower(SHA256.HashData(key));
    }

    /// <summary>The queue names a link of this name could hold a subscription under, whichever way it was shared.</summary>
    public static IReadOnlyList<string> QueueNamesFor(string clientContainerId, string linkName) =>
        [.. new[] { (false, false), (true, false), (true, true) }
            .Select(sharing => QueueNameFor(clientContainerId, linkName, sharing.Item1, sharing.Item2))
            .Distinct(StringComparer.Ordinal)];

    /// <summary>Reads a subscription back from the description the bus kept.</summary>
    /// <param name="queueName">The queue the description was kept beside.</param>
    /// <param name="description">The description.</param>
    /// <returns>The subscription; <see langword="null"/> when the text is not one this container wrote.</returns>
    public static Amqp1KeptSubscription? FromDescription(string queueName, string? description)
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
