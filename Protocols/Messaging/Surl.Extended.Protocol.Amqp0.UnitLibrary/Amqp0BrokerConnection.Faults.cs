using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9): each method on an open channel
/// that names an exchange or a queue - <c>exchange.declare</c>, <c>exchange.delete</c>,
/// <c>queue.declare</c>, <c>queue.bind</c>, <c>queue.unbind</c>, <c>queue.purge</c>,
/// <c>queue.delete</c>, <c>basic.consume</c>, <c>basic.get</c> and <c>basic.publish</c> - is asked
/// about at a <see cref="FaultStage.Command"/> point before it is answered: the method's name as its
/// command, the exchange or queue it names as its path (for <c>basic.publish</c> to the default
/// exchange, the routing key, which is the queue), the account the login proved. A matching
/// <see cref="FaultKind.Error"/> rule's reply code and text are sent as the specification's
/// exception list says the code is sent: a channel exception (311 to 313, 403 to 406) as
/// <c>channel.close</c>, a connection exception as <c>connection.close</c>. The method is not
/// answered, and a publish's content is discarded with the closing channel. A
/// <see cref="FaultKind.Delay"/> or <see cref="FaultKind.Drop"/> rule at the same point is applied by
/// <see cref="CommandStageFaults"/> (ADR-0141): a delay holds the method's handling, a drop aborts the
/// connection with nothing sent.
/// </summary>
internal sealed partial class Amqp0BrokerConnection
{
    // AMQP 0-9-1 section 1.2's reply codes the specification calls channel exceptions.
    private static readonly HashSet<ushort> ChannelExceptionCodes = [311, 312, 313, 403, 404, 405, 406];

    // AMQP 0-9-1 section 1.2's reply codes the specification calls connection exceptions.
    private static readonly HashSet<ushort> ConnectionExceptionCodes = [320, 402, 501, 502, 503, 504, 505, 506, 530, 540, 541];

    private static readonly Dictionary<(ushort ClassId, ushort MethodId), string> FaultCommands = new()
    {
        [(Amqp0ExchangeMethodId.ClassId, Amqp0ExchangeMethodId.Declare)] = "exchange.declare",
        [(Amqp0ExchangeMethodId.ClassId, Amqp0ExchangeMethodId.Delete)] = "exchange.delete",
        [(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Declare)] = "queue.declare",
        [(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Bind)] = "queue.bind",
        [(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Unbind)] = "queue.unbind",
        [(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Purge)] = "queue.purge",
        [(Amqp0QueueMethodId.ClassId, Amqp0QueueMethodId.Delete)] = "queue.delete",
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Consume)] = "basic.consume",
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Get)] = "basic.get",
        [(Amqp0BasicMethodId.ClassId, Amqp0BasicMethodId.Publish)] = "basic.publish",
    };

    /// <summary>
    /// Reads a rule's reply as an AMQP 0-9-1 exception: a reply code from the specification's list and
    /// text of 1 to 255 bytes, separated by the first space.
    /// </summary>
    internal static bool TryReadException(string? reply, out ushort replyCode, out string replyText)
    {
        var space = reply?.IndexOf(' ', StringComparison.Ordinal) ?? -1;
        replyText = space < 0 ? string.Empty : reply![(space + 1)..];
        replyCode = 0;
        return space > 0
            && ushort.TryParse(reply.AsSpan(0, space), NumberStyles.None, CultureInfo.InvariantCulture, out replyCode)
            && (ChannelExceptionCodes.Contains(replyCode) || ConnectionExceptionCodes.Contains(replyCode))
            && System.Text.Encoding.UTF8.GetByteCount(replyText) is >= 1 and <= byte.MaxValue;
    }

    // Answers the frame with a chosen error when a rule matches it; null when it is answered as usual,
    // otherwise whether the connection stays open.
    private async ValueTask<bool?> AnswerByAFaultAsync(Amqp0Frame frame)
    {
        if (FaultMethodOf(frame) is not { } faulted)
        {
            return null;
        }

        var (channel, method, command, path) = faulted;
        var (dropped, exception) = await FindChosenExceptionAsync(command, path);
        if (dropped)
        {
            return false;
        }

        if (exception is not { } chosen)
        {
            return null;
        }

        if (ChannelExceptionCodes.Contains(chosen.ReplyCode))
        {
            CloseChannel(channel, (Amqp0ReplyCode)chosen.ReplyCode, chosen.ReplyText, method.ClassId, method.MethodId);
            return true;
        }

        await CloseAsync((Amqp0ReplyCode)chosen.ReplyCode, chosen.ReplyText, method.ClassId, method.MethodId);
        return false;
    }

    // The method a fault may answer: on an open channel with no content pending, one that names an
    // exchange or a queue and whose name decodes. Anything else is answered as usual.
    private (Amqp0Channel Channel, Amqp0MethodPayload Method, string Command, string Path)? FaultMethodOf(Amqp0Frame frame)
    {
        if (frame.Type != Amqp0FrameType.Method
            || !channels.TryGetValue(frame.Channel, out var channel)
            || channel.Closing
            || channel.Publish is not null)
        {
            return null;
        }

        return MethodNamingAPath(frame) is { } named ? (channel, named.Method, named.Command, named.Path) : null;
    }

    private static (Amqp0MethodPayload Method, string Command, string Path)? MethodNamingAPath(Amqp0Frame frame)
    {
        try
        {
            var method = Amqp0MethodPayload.Read(frame.Payload);
            return FaultCommands.TryGetValue((method.ClassId, method.MethodId), out var command)
                ? (method, command, PathOf(method))
                : null;
        }
        catch (Amqp0SyntaxException)
        {
            return null;
        }
    }

    // Every method above starts with reserved-1 and the exchange or queue name; basic.publish to
    // the default exchange names its queue in the routing key that follows.
    private static string PathOf(Amqp0MethodPayload method)
    {
        var arguments = new Amqp0WireReader(method.Arguments);
        arguments.ReadShort();
        var name = arguments.ReadShortString();
        return name.Length == 0 && method.ClassId == Amqp0BasicMethodId.ClassId && method.MethodId == Amqp0BasicMethodId.Publish
            ? arguments.ReadShortString()
            : name;
    }

    // Finds the rule for the method and applies a delay or a drop (ADR-0141). A drop has aborted the
    // connection and is reported by dropped; the exception is a chosen error to answer with.
    private async ValueTask<(bool Dropped, (ushort ReplyCode, string ReplyText)? Exception)> FindChosenExceptionAsync(string command, string path)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = AccountName,
            Path = path,
            Command = command,
        };
        var target = $"{command} {path}";
        var rule = await CommandStageFaults.FindAndApplyAsync(context, point, target);
        if (rule is null or { Kind: FaultKind.Delay })
        {
            return (false, null);
        }

        if (rule.Kind == FaultKind.Drop)
        {
            // What was answered before the dropped method is written out first, so the cut is
            // at the method and not somewhere in the queue behind it.
            closeSent = true;
            outbound!.Writer.TryComplete();
            await outboundPumping;
            connection.Abort();
            return (true, null);
        }

        return (false, ChosenException(rule, target));
    }

    private (ushort ReplyCode, string ReplyText)? ChosenException(FaultRule rule, string target)
    {
        if (rule.Kind != FaultKind.Error || !TryReadException(rule.Reply, out var replyCode, out var replyText))
        {
            context.Log.Note($"{target}: fault rule {rule.Id} is not an error with an AMQP 0-9-1 reply code and text; not applied.");
            return null;
        }

        context.Log.Note($"{target} answered by fault rule {rule.Id} with reply code {replyCode}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, target));
        return (replyCode, replyText);
    }
}
