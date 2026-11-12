using System.Globalization;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Asks the exchange's fault plan, for each request before it is answered, whether to answer it with
/// a chosen response code instead (ADR-0124, decisions 4 and 9; ADR-0144): the point names the scheme
/// (<c>coap</c> or <c>coaps</c>), the method as <c>command</c> (<c>GET</c>, <c>POST</c>, <c>PUT</c>,
/// <c>DELETE</c>, any other as its <c>0.dd</c> code) and the Uri-Path as <c>path</c> (<c>/a/b</c>,
/// <c>/</c> for none). CoAP has no account, so the point names none.
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Delay"/> or <see cref="FaultKind.Drop"/> rule is applied, and so is an
/// <see cref="FaultKind.Error"/> rule whose reply fits (<see cref="TryParse"/>); each publishes one
/// <see cref="FaultAppliedEvent"/> naming the path. A rule of any other kind, or a reply that does
/// not fit, is noted as not applied and the request is answered as without it.
/// </remarks>
internal static class CoapRequestFaults
{
    /// <summary>
    /// The Max-Age a <c>5.03</c> carries: the seconds after which the client may retry (RFC 7252
    /// section 5.9.3.4), short so a test that retries does not wait.
    /// </summary>
    public const uint ServiceUnavailableRetryAfterSeconds = 1;

    private static readonly CoapCode ServiceUnavailable = CoapCode.From(5, 3);

    /// <summary>
    /// Offers a request to the fault plan and applies what it finds, publishing the
    /// <see cref="FaultAppliedEvent"/>: a <see cref="FaultKind.Delay"/> rule holds the answer for its
    /// <c>ms</c> on the exchange's clock with the idle clock paused, a <see cref="FaultKind.Drop"/>
    /// rule leaves the request without any answer (CoAP has no connection to abort; ADR-0144
    /// amendment 1), and an <see cref="FaultKind.Error"/> rule gives its chosen response.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clock, event stream and listen URL.</param>
    /// <param name="request">The request: its code is a method.</param>
    /// <param name="options">Its options, read.</param>
    /// <returns>What the request is answered with.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async Task<CoapFaultOutcome> ApplyAsync(ExchangeContext context, CoapMessage request, CoapRequestOptions options)
    {
        var path = "/" + string.Join('/', options.UriPath);
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Path = path,
            Command = CommandOf(request.Code),
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, point, path);

        return rule?.Kind switch
        {
            null or FaultKind.Delay => CoapFaultOutcome.AnsweredAsUsual,
            FaultKind.Drop => CoapFaultOutcome.Dropped,
            _ => ChooseError(context, rule, path),
        };
    }

    private static CoapFaultOutcome ChooseError(ExchangeContext context, FaultRule rule, string path)
    {
        if (rule.Kind != FaultKind.Error || !TryParse(rule.Reply, out var response))
        {
            var why = rule.Kind == FaultKind.Error ? "its reply does not fit the coap error shape" : $"a {rule.Kind} rule is not applied at a request";
            context.Log.Note($"Fault rule {rule.Id} not applied to the request: {why}.");

            return CoapFaultOutcome.AnsweredAsUsual;
        }

        context.Log.Note($"The request was answered {response.Code} by fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, path));

        return new CoapFaultOutcome(response, false);
    }

    /// <summary>
    /// The method as a fault rule's <c>command</c> names it: <c>GET</c>, <c>POST</c>, <c>PUT</c> or
    /// <c>DELETE</c>, and any other method code as RFC 7252 writes it (<c>0.05</c>).
    /// </summary>
    /// <param name="method">A method code.</param>
    /// <returns>The command.</returns>
    public static string CommandOf(CoapCode method) => method.Value switch
    {
        0x01 => "GET",
        0x02 => "POST",
        0x03 => "PUT",
        0x04 => "DELETE",
        _ => method.ToString(),
    };

    /// <summary>
    /// Reads a reply in the <c>coap</c> error shape: a response code <c>4.00</c> to <c>5.05</c>
    /// written <c>c.dd</c>, then optionally a space and a diagnostic text, sent as the response's
    /// UTF-8 payload (RFC 7252 section 5.5.2).
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <param name="response">The response the reply names.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool TryParse(string? reply, out CoapResponse response)
    {
        response = CoapResponse.Bare(CoapCode.Empty);
        if (reply is null || reply.Length < 4 || reply[1] != '.' || (reply.Length > 4 && (reply[4] != ' ' || reply.Length == 5)))
        {
            return false;
        }

        if (!int.TryParse(reply.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var detail)
            || !IsErrorCode(reply[0], detail))
        {
            return false;
        }

        var code = CoapCode.From(reply[0] - '0', detail);
        response = ResponseOf(code, reply.Length > 4 ? Encoding.UTF8.GetBytes(reply[5..]) : ReadOnlyMemory<byte>.Empty);

        return true;
    }

    private static CoapResponse ResponseOf(CoapCode code, ReadOnlyMemory<byte> payload) =>
        new(code, code == ServiceUnavailable ? [CoapOption.FromUInt(CoapOptionNumber.MaxAge, ServiceUnavailableRetryAfterSeconds)] : [], payload);

    // 4.00 to 4.31, and 5.00 to 5.05.
    private static bool IsErrorCode(char codeClass, int detail) => codeClass switch
    {
        '4' => detail <= 31,
        '5' => detail <= 5,
        _ => false,
    };
}
