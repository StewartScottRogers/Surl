using System.Buffers.Text;
using System.Security.Cryptography;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The <c>exchange</c> and <c>queue</c> classes (ADR-0112 decision 5), and the answer each bus
/// refusal gets (decision 7's table).
/// </summary>
internal sealed partial class Amqp0BrokerConnection
{
    private const string ReservedPrefix = "amq.";

    private static readonly Dictionary<MessageBusOutcome, (Amqp0ReplyCode ReplyCode, string ReplyText)> ChannelRefusals = new()
    {
        [MessageBusOutcome.UnknownQueue] = (Amqp0ReplyCode.NotFound, "NOT_FOUND - no queue"),
        [MessageBusOutcome.QueueLocked] = (Amqp0ReplyCode.ResourceLocked, "RESOURCE_LOCKED - exclusive queue of another connection"),
        [MessageBusOutcome.AccessDenied] = (Amqp0ReplyCode.AccessRefused, "ACCESS_REFUSED - access refused"),
        [MessageBusOutcome.QueueFlagsDiffer] = (Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - inequivalent declaration"),
        [MessageBusOutcome.QueueInUse] = (Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - queue in use"),
        [MessageBusOutcome.QueueNotEmpty] = (Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - queue not empty"),
        [MessageBusOutcome.ExclusiveConsumeRefused] = (Amqp0ReplyCode.AccessRefused, "ACCESS_REFUSED - queue consumed exclusively"),
        [MessageBusOutcome.MessageTooLarge] = (Amqp0ReplyCode.ContentTooLarge, "CONTENT_TOO_LARGE - body above the limit"),
    };

    private static readonly Amqp0MethodPayload ExchangeDeclareOk = new(Amqp0ExchangeMethodId.ClassId, Amqp0ExchangeMethodId.DeclareOk, ReadOnlyMemory<byte>.Empty);
    private static readonly Amqp0MethodPayload ExchangeDeleteOk = new(Amqp0ExchangeMethodId.ClassId, Amqp0ExchangeMethodId.DeleteOk, ReadOnlyMemory<byte>.Empty);
    private static readonly Amqp0MethodPayload QueueBindOk = new(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.BindOk, ReadOnlyMemory<byte>.Empty);
    private static readonly Amqp0MethodPayload QueueUnbindOk = new(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.UnbindOk, ReadOnlyMemory<byte>.Empty);

    // Succeeded passes; every other outcome is its channel exception, or connection exception 506.
    private static void Refuse(MessageBusOutcome outcome)
    {
        if (outcome != MessageBusOutcome.Succeeded)
        {
            throw Refusal(outcome);
        }
    }

    private static Exception Refusal(MessageBusOutcome outcome) =>
        ChannelRefusals.TryGetValue(outcome, out var refusal)
            ? new Amqp0ChannelException(refusal.ReplyCode, refusal.ReplyText)
            : new Amqp0ConnectionException(Amqp0ReplyCode.ResourceError, "RESOURCE_ERROR - broker bound reached");

    private static Amqp0ChannelException NoExchange() => new(Amqp0ReplyCode.NotFound, "NOT_FOUND - no exchange");

    private static Amqp0ChannelException DefaultExchangeRefused() => new(Amqp0ReplyCode.AccessRefused, "ACCESS_REFUSED - default exchange");

    private static Amqp0ChannelException ReservedNameRefused() => new(Amqp0ReplyCode.AccessRefused, "ACCESS_REFUSED - reserved name");

    // Rule declare/syntax: a run of letters, digits, '-', '_', '.' and ':'. Its callers have already
    // answered the empty name, and a shortstr holds at most 255 bytes.
    private static bool IsValidName(string name) =>
        name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':');

    // A server-made name: the prefix and 22 base64url characters of 16 random bytes.
    private static string GenerateName(string prefix) => prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    private void DeclareExchange(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        arguments.ReadShort();
        var name = arguments.ReadShortString();
        var typeName = arguments.ReadShortString();
        var bits = arguments.ReadBits(5);
        var table = arguments.ReadFieldTable();
        if (bits[0])
        {
            CheckExchangeExists(name);
        }
        else
        {
            DeclareExchange(name, typeName, durable: bits[1], table);
        }

        if (!bits[4])
        {
            SendMethod(channel.Number, ExchangeDeclareOk);
        }
    }

    private void CheckExchangeExists(string name)
    {
        if (name.Length != 0 && exchanges.Find(name) is null)
        {
            throw NoExchange();
        }
    }

    private void DeclareExchange(string name, string typeName, bool durable, Amqp0FieldTable table)
    {
        if (name.Length == 0)
        {
            throw DefaultExchangeRefused();
        }

        if (!IsValidName(name))
        {
            throw new Amqp0ChannelException(Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - invalid name");
        }

        if (name.StartsWith(ReservedPrefix, StringComparison.Ordinal) && exchanges.Find(name) is null)
        {
            throw ReservedNameRefused();
        }

        var type = ExchangeTypeOf(typeName);
        var encoded = new Amqp0WireWriter();
        encoded.WriteFieldTable(table);
        AnswerExchangeDeclareOutcome(exchanges.Declare(name, type, durable, encoded.ToArray()));
    }

    private static Amqp0ExchangeType ExchangeTypeOf(string typeName) => typeName switch
    {
        "direct" => Amqp0ExchangeType.Direct,
        "fanout" => Amqp0ExchangeType.Fanout,
        "topic" => Amqp0ExchangeType.Topic,
        _ => throw new Amqp0ConnectionException(Amqp0ReplyCode.CommandInvalid, "COMMAND_INVALID - unknown exchange type"),
    };

    private static void AnswerExchangeDeclareOutcome(Amqp0ExchangeDeclareOutcome outcome)
    {
        switch (outcome)
        {
            case Amqp0ExchangeDeclareOutcome.TypeDiffers:
                throw new Amqp0ConnectionException(Amqp0ReplyCode.NotAllowed, "NOT_ALLOWED - exchange type differs");
            case Amqp0ExchangeDeclareOutcome.FlagsDiffer:
                throw new Amqp0ChannelException(Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - inequivalent declaration");
            case Amqp0ExchangeDeclareOutcome.TooManyExchanges:
                throw new Amqp0ConnectionException(Amqp0ReplyCode.ResourceError, "RESOURCE_ERROR - broker bound reached");
        }
    }

    private void DeleteExchange(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        arguments.ReadShort();
        var name = arguments.ReadShortString();
        var bits = arguments.ReadBits(2);
        if (name.Length == 0)
        {
            throw DefaultExchangeRefused();
        }

        var exchange = exchanges.Find(name) ?? throw NoExchange();
        if (exchange.IsPredeclared)
        {
            throw ReservedNameRefused();
        }

        if (!exchanges.Delete(exchange, ifUnused: bits[0]))
        {
            throw new Amqp0ChannelException(Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - exchange in use");
        }

        if (!bits[1])
        {
            SendMethod(channel.Number, ExchangeDeleteOk);
        }
    }

    // A queue name "" is the channel's last declared queue (rule bind/queue-known).
    private static string ResolveQueue(Amqp0Channel channel, string name) =>
        name.Length != 0 ? name
            : channel.LastQueue ?? throw new Amqp0ChannelException(Amqp0ReplyCode.NotFound, "NOT_FOUND - no queue declared on the channel");

    private void DeclareQueue(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        arguments.ReadShort();
        var name = arguments.ReadShortString();
        var bits = arguments.ReadBits(5);
        arguments.ReadFieldTable();
        var (queue, ready, consumers) = bits[0]
            ? InspectQueue(ResolveQueue(channel, name))
            : DeclareQueue(name, new BusQueueFlags(Durable: bits[1], Exclusive: bits[2], AutoDelete: bits[3]));
        channel.LastQueue = queue;
        if (!bits[4])
        {
            SendMethod(channel.Number, MethodOf(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.DeclareOk, writer =>
            {
                writer.WriteShortString(queue);
                writer.WriteLong((uint)ready);
                writer.WriteLong((uint)consumers);
            }));
        }
    }

    private (string Queue, int Ready, int Consumers) InspectQueue(string queue)
    {
        var inspected = Bus.InspectQueue(queue);
        Refuse(inspected.Outcome);
        return (queue, inspected.ReadyMessageCount, inspected.ConsumerCount);
    }

    private (string Queue, int Ready, int Consumers) DeclareQueue(string name, BusQueueFlags flags)
    {
        var queue = name.Length == 0 ? GenerateName("amq.gen-") : NewQueueNameChecked(name);
        var declared = Bus.DeclareQueue(queue, flags);
        Refuse(declared.Outcome);
        return (queue, declared.ReadyMessageCount, declared.ConsumerCount);
    }

    // A name the declare would create must be well formed and not amq.; an existing queue is
    // reachable by whatever name it has (made over stomp or by --queue).
    private string NewQueueNameChecked(string name)
    {
        if (Bus.InspectQueue(name).Outcome != MessageBusOutcome.UnknownQueue)
        {
            return name;
        }

        if (!IsValidName(name))
        {
            throw new Amqp0ChannelException(Amqp0ReplyCode.PreconditionFailed, "PRECONDITION_FAILED - invalid name");
        }

        return name.StartsWith(ReservedPrefix, StringComparison.Ordinal) ? throw ReservedNameRefused() : name;
    }

    private void BindQueue(Amqp0Channel channel, Amqp0WireReader arguments, bool bind)
    {
        arguments.ReadShort();
        var queue = ResolveQueue(channel, arguments.ReadShortString());
        var exchangeName = arguments.ReadShortString();
        var key = arguments.ReadShortString();
        var noWait = bind && arguments.ReadBits(1)[0];
        arguments.ReadFieldTable();
        if (exchangeName.Length == 0)
        {
            throw DefaultExchangeRefused();
        }

        var exchange = exchanges.Find(exchangeName) ?? throw NoExchange();
        Refuse(Bus.InspectQueue(queue).Outcome);
        if (exchange.Name == Amqp0ExchangeTable.TopicSpace)
        {
            BindOnTopicSpace(queue, key, bind);
        }
        else
        {
            BindOnExchange(exchange, queue, key, bind);
        }

        if (!noWait)
        {
            SendMethod(channel.Number, bind ? QueueBindOk : QueueUnbindOk);
        }
    }

    // amq.topic's bindings are the bus's; a key with no canonical form binds nothing, since every
    // routing key it could match is unroutable itself.
    private void BindOnTopicSpace(string queue, string key, bool bind)
    {
        var filter = AmqpTopicExchangeNames.ToCanonicalFilter(key);
        if (!filter.IsMapped)
        {
            context.Log.Note("A binding key on amq.topic has no canonical form; nothing was bound.");
            return;
        }

        Refuse(bind ? Bus.BindQueue(queue, filter.Name) : Bus.UnbindQueue(queue, filter.Name));
    }

    private void BindOnExchange(Amqp0Exchange exchange, string queue, string key, bool bind)
    {
        if (!bind)
        {
            exchanges.Unbind(exchange, queue, key);
        }
        else if (!exchanges.Bind(exchange, queue, key))
        {
            throw new Amqp0ConnectionException(Amqp0ReplyCode.ResourceError, "RESOURCE_ERROR - broker bound reached");
        }
    }

    private void PurgeQueue(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        arguments.ReadShort();
        var queue = ResolveQueue(channel, arguments.ReadShortString());
        var noWait = arguments.ReadBits(1)[0];
        var purged = Bus.PurgeQueue(queue);
        Refuse(purged.Outcome);
        SendCountUnlessNoWait(channel, Amqp0QueueMethodId.PurgeOk, purged.RemovedMessageCount, noWait);
    }

    private void DeleteQueue(Amqp0Channel channel, Amqp0WireReader arguments)
    {
        arguments.ReadShort();
        var queue = ResolveQueue(channel, arguments.ReadShortString());
        var bits = arguments.ReadBits(3);
        var deleted = Bus.DeleteQueue(queue, ifUnused: bits[0], ifEmpty: bits[1]);
        Refuse(deleted.Outcome);
        exchanges.RemoveQueue(queue);
        SendCountUnlessNoWait(channel, Amqp0QueueMethodId.DeleteOk, deleted.RemovedMessageCount, bits[2]);
    }

    private void SendCountUnlessNoWait(Amqp0Channel channel, ushort methodId, int count, bool noWait)
    {
        if (!noWait)
        {
            SendMethod(channel.Number, MethodOf(Amqp0QueueMethodId.ClassId, methodId, writer => writer.WriteLong((uint)count)));
        }
    }
}
