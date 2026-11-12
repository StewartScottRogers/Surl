using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The CoAP server for the <c>coap</c> scheme (ADR-0097 decision 0): one flow is one client
/// endpoint, and every message the endpoint sends is answered from the listen port until the
/// serving engine ends the flow.
/// </summary>
/// <remarks>
/// <para>
/// The message layer (ADR-0097 decision 2; RFC 7252 sections 3, 4 and 5.2): a datagram shorter
/// than the header or of a version other than 1 is ignored; a CON the server lacks context for - a
/// message format error, an Empty CON, a response code or a reserved class - gets an Empty RST
/// echoing its Message ID, and the same NON is ignored; a client's ACK or RST ends the
/// retransmission of the separate response it matches and is otherwise ignored. A CON request is
/// answered piggybacked in its ACK, or, when the answer takes longer than a second, with an Empty
/// ACK and then a separate CON response retransmitted until acknowledged or given up after
/// MAX_RETRANSMIT; a NON request with a NON. Duplicates get the same answer and are not served
/// twice. The flow's Message IDs start at a random 16-bit value and increase by one per message.
/// </para>
/// <para>
/// Requests (ADR-0097 decisions 1, 3 and 6) are answered from the content store: <c>GET</c>,
/// <c>PUT</c>, <c>POST</c> and <c>DELETE</c> on files with Content-Format, ETag and Max-Age, the
/// conditional options, <c>/.well-known/core</c> under the listing switch, and the access rules of
/// NoSec: every peer over <c>coap</c> is unauthenticated.
/// </para>
/// </remarks>
public sealed class CoapProtocolServer : IDatagramProtocolServer, IDatagramRefusalWriter
{
    private readonly CoapResourceAnswerer answerer;
    private readonly ICoapRandomSource random;
    private readonly ContentStore contentStore;
    private readonly CoapObservations observations;

    /// <summary>
    /// Creates a CoAP server that serves <paramref name="contentStore"/> under
    /// <paramref name="accessRules"/>, drawing every random value from
    /// <see cref="System.Security.Cryptography.RandomNumberGenerator"/>.
    /// </summary>
    /// <param name="contentStore">The resources: every visible file of the store.</param>
    /// <param name="accessRules">Who may read and write.</param>
    public CoapProtocolServer(ContentStore contentStore, CoapAccessRules accessRules)
        : this(contentStore, accessRules, new CoapObservations())
    {
    }

    /// <summary>
    /// Creates a CoAP server that serves <paramref name="contentStore"/> under
    /// <paramref name="accessRules"/> and shares <paramref name="observations"/> with every other
    /// CoAP server of the process, drawing every random value from
    /// <see cref="System.Security.Cryptography.RandomNumberGenerator"/>.
    /// </summary>
    /// <param name="contentStore">The resources: every visible file of the store.</param>
    /// <param name="accessRules">Who may read and write.</param>
    /// <param name="observations">Every observer of the process (ADR-0097 decision 0).</param>
    public CoapProtocolServer(ContentStore contentStore, CoapAccessRules accessRules, CoapObservations observations)
        : this(contentStore, accessRules, CryptographicCoapRandomSource.Instance, observations: observations)
    {
    }

    internal CoapProtocolServer(
        ContentStore contentStore,
        CoapAccessRules accessRules,
        ICoapRandomSource random,
        int preferredBlockSizeExponent = CoapBlockOption.LargestSizeExponent,
        CoapObservations? observations = null)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(accessRules);

        answerer = new CoapResourceAnswerer(contentStore, accessRules, random, preferredBlockSizeExponent);
        this.contentStore = contentStore;
        this.random = random;
        this.observations = observations ?? new CoapObservations();
    }

    /// <summary>The one scheme answered: <c>coap</c>.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["coap"]);

    /// <summary>
    /// Refuses a flow past a connection limit in CoAP's words (ADR-0097 decision 7): a first datagram
    /// that is a CON request gets an ACK with <c>5.03</c> and Max-Age 5, a NON request a NON
    /// <c>5.03</c>, and anything else nothing.
    /// </summary>
    /// <param name="flow">The flow past the limit.</param>
    /// <param name="refusal">Which limit it is past.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the refusal is sent, or at once when there is none.</returns>
    public async ValueTask WriteRefusalAsync(IDatagramFlow flow, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var outcome = CoapMessageReader.Read(flow.FirstDatagram.Span);

        CoapMessage? answer = outcome.Message switch
        {
            { Code.IsRequest: true, Type: CoapMessageType.Confirmable } request => CoapRefusals.ServiceUnavailableAck(request),
            { Code.IsRequest: true, Type: CoapMessageType.NonConfirmable } request => CoapRefusals.ServiceUnavailableNon(request, random.DrawMessageId()),
            _ => null,
        };

        if (answer is not null)
        {
            await flow.SendAsync(CoapMessageWriter.Write(answer), cancellationToken);
        }
    }

    /// <summary>
    /// Answers the message that opened <paramref name="flow"/> and every later one, until the
    /// exchange is cancelled, which ends the call with <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="flow">The flow opened by its first datagram.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(context);

        return CoapFlowServing.ServeAsync(flow, context, answerer, contentStore, observations, random, peerIsAuthenticated: false);
    }
}
