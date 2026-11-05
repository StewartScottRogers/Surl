using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The CoAP server for the <c>coap</c> scheme (ADR-0097 decision 0): one flow is one client
/// endpoint, and every message the endpoint sends is answered from the listen port until the
/// serving engine ends the flow.
/// </summary>
/// <remarks>
/// <para>
/// The message layer's rejections (ADR-0097 decision 2; RFC 7252 sections 3, 4.2 and 4.3): a
/// datagram shorter than the header or of a version other than 1 is ignored whatever its type; a
/// CON the server lacks context for - a message format error, an Empty CON (a CoAP ping), a
/// response code or a reserved class - is answered with an Empty RST echoing its Message ID; the
/// same NON is silently ignored, as is every ACK and RST from the client. Each is noted in the
/// exchange's log.
/// </para>
/// <para>
/// Until resources are served (BL-472), every well-formed request is answered <c>4.04 Not
/// Found</c> with its token: a CON in a piggybacked ACK carrying its Message ID (section 5.2.1),
/// a NON in a NON with the flow's next Message ID (section 5.2.3). The flow's Message IDs start at
/// a random 16-bit value and increase by one per message (section 4.4).
/// </para>
/// </remarks>
public sealed class CoapProtocolServer : IDatagramProtocolServer
{
    private readonly Func<ushort> drawFirstMessageId;

    /// <summary>
    /// Creates a CoAP server.
    /// </summary>
    /// <param name="drawFirstMessageId">
    /// Draws each flow's first Message ID; a uniformly random 16-bit value from
    /// <see cref="RandomNumberGenerator"/> when <see langword="null"/>.
    /// </param>
    public CoapProtocolServer(Func<ushort>? drawFirstMessageId = null)
    {
        this.drawFirstMessageId = drawFirstMessageId ?? DrawRandomMessageId;
    }

    /// <summary>The one scheme answered: <c>coap</c>.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["coap"]);

    /// <summary>
    /// Answers the message that opened <paramref name="flow"/> and every later one, until the
    /// exchange is cancelled, which ends the call with <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="flow">The flow opened by its first datagram.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(context);

        var nextMessageId = drawFirstMessageId();
        var datagram = flow.FirstDatagram;

        while (true)
        {
            var answer = Answer(CoapMessageReader.Read(datagram.Span), ref nextMessageId, context.Log);

            if (answer is not null)
            {
                await flow.SendAsync(CoapMessageWriter.Write(answer), context.CancellationToken);
            }

            datagram = await flow.ReceiveAsync(context.CancellationToken);
        }
    }

    private static CoapMessage? Answer(CoapReadOutcome outcome, ref ushort nextMessageId, IExchangeLog log)
    {
        switch (outcome.Kind)
        {
            case CoapReadOutcomeKind.NotCoap:
                log.Note($"Ignored: {outcome.Fault}");
                return null;
            case CoapReadOutcomeKind.FormatError:
                return Reject(outcome.Type, outcome.MessageId, $"a message format error: {outcome.Fault}", log);
            default:
                return AnswerMessage(outcome.Message!, ref nextMessageId, log);
        }
    }

    private static CoapMessage? AnswerMessage(CoapMessage message, ref ushort nextMessageId, IExchangeLog log)
    {
        if (message.Type is CoapMessageType.Acknowledgement or CoapMessageType.Reset)
        {
            log.Note($"Ignored: an unmatched {message.Type} with Message ID {message.MessageId}.");
            return null;
        }

        if (!message.Code.IsRequest)
        {
            return Reject(message.Type, message.MessageId, $"code {message.Code}, which is no request.", log);
        }

        return AnswerRequest(message, ref nextMessageId);
    }

    private static CoapMessage AnswerRequest(CoapMessage request, ref ushort nextMessageId)
    {
        if (request.Type == CoapMessageType.Confirmable)
        {
            return new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.NotFound, request.MessageId, request.Token);
        }

        return new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.NotFound, nextMessageId++, request.Token);
    }

    private static CoapMessage? Reject(CoapMessageType type, ushort messageId, string reason, IExchangeLog log)
    {
        if (type == CoapMessageType.Confirmable)
        {
            log.Note($"Rejected with RST: a CON with Message ID {messageId} and {reason}");
            return CoapMessage.EmptyReset(messageId);
        }

        log.Note($"Ignored: a {type} with Message ID {messageId} and {reason}");
        return null;
    }

    private static ushort DrawRandomMessageId() => (ushort)RandomNumberGenerator.GetInt32(ushort.MaxValue + 1);
}
