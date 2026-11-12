using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>The login: <c>connection.start-ok</c>, <c>secure</c> and <c>secure-ok</c> (ADR-0112 decision 2).</summary>
internal sealed partial class Amqp0BrokerConnection
{
    private const string PlainMechanism = "PLAIN";
    private const string AmqplainMechanism = "AMQPLAIN";
    private const string AnonymousMechanism = "ANONYMOUS";

    private static readonly Amqp0FieldTable ServerProperties = new(
    [
        new("product", Amqp0FieldValue.LongString("surl")),
        new("capabilities", Amqp0FieldValue.Table(new(
        [
            new("basic.nack", Amqp0FieldValue.Boolean(true)),
            new("consumer_cancel_notify", Amqp0FieldValue.Boolean(true)),
            new("authentication_failure_close", Amqp0FieldValue.Boolean(true)),
            new("publisher_confirms", Amqp0FieldValue.Boolean(true)),
        ]))),
    ]);

    // The method the refused login's connection.close names: start-ok, or secure-ok once one came.
    private ushort loginMethodId = Amqp0ConnectionMethodId.StartOk;

    private enum LoginEnd
    {
        Accepted,
        Refused,
        Abandoned,
    }

    private List<string> OfferedMechanisms()
    {
        var offered = new List<string>();
        foreach (var name in policy.GetSaslMechanisms(new SaslOfferRequest(context.Scheme, connection.TlsSession)))
        {
            offered.Add(name);
            if (name == PlainMechanism)
            {
                offered.Add(AmqplainMechanism);
            }
        }

        if (options.OffersAnonymous && !offered.Contains(AnonymousMechanism))
        {
            offered.Add(AnonymousMechanism);
        }

        return offered;
    }

    private async ValueTask<bool> LogInAsync(List<string> offered, CancellationToken cancellationToken)
    {
        if (await ReadConnectionMethodAsync(Amqp0ConnectionMethodId.StartOk, Amqp0ConnectionStartOk.Read, cancellationToken) is not { } startOk)
        {
            return false;
        }

        clientAcceptsConsumerCancel = startOk.AnnouncesConsumerCancelNotify;
        if (!offered.Contains(startOk.Mechanism))
        {
            // Rule start-ok/mechanism: close "without sending any further data".
            context.Log.Note($"connection.start-ok chose {startOk.Mechanism}, which was not offered; closed with no reply.");
            return false;
        }

        context.Log.Note($"connection.start-ok chose {startOk.Mechanism}.");
        var end = await JudgeLoginAsync(startOk, cancellationToken);
        if (end == LoginEnd.Refused)
        {
            await CloseAsync(Amqp0ReplyCode.AccessRefused, "ACCESS_REFUSED - login refused", Amqp0ConnectionMethodId.ClassId, loginMethodId);
        }

        return end == LoginEnd.Accepted;
    }

    private async ValueTask<LoginEnd> JudgeLoginAsync(Amqp0ConnectionStartOk startOk, CancellationToken cancellationToken)
    {
        switch (startOk.Mechanism)
        {
            case AnonymousMechanism:
                return LoginEnd.Accepted;
            case AmqplainMechanism:
                if (Amqp0ConnectionStartOk.ToPlainResponse(startOk.Response) is not { } plain)
                {
                    context.Log.Note("The AMQPLAIN response was not a table of LOGIN and PASSWORD long strings.");
                    return LoginEnd.Refused;
                }

                return await RunSaslExchangeAsync(PlainMechanism, plain, cancellationToken);
            default:
                return await RunSaslExchangeAsync(startOk.Mechanism, startOk.Response, cancellationToken);
        }
    }

    // Each challenge is a connection.secure and each answer a secure-ok, until the policy decides.
    private async ValueTask<LoginEnd> RunSaslExchangeAsync(string mechanism, ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
    {
        var initialResponse = response.IsEmpty ? (ReadOnlyMemory<byte>?)null : response;
        var exchange = policy.StartSaslExchange(new SaslExchangeStart(context.Scheme, mechanism, initialResponse, connection.TlsSession));
        var step = await exchange.BeginAsync(cancellationToken);
        while (step.Outcome == SaslLoginOutcome.Challenge)
        {
            await WriteMethodAsync(SecureMethod(step.Challenge), cancellationToken);
            if (await ReadConnectionMethodAsync(Amqp0ConnectionMethodId.SecureOk, ReadSecureOkResponse, cancellationToken) is not { } answer)
            {
                return LoginEnd.Abandoned;
            }

            loginMethodId = Amqp0ConnectionMethodId.SecureOk;
            step = await exchange.ContinueAsync(answer, cancellationToken);
        }

        NoteLoginStep(step);
        if (step.Outcome is SaslLoginOutcome.Accepted or SaslLoginOutcome.AcceptedUnchecked)
        {
            AccountName = step.AccountName;
            return LoginEnd.Accepted;
        }

        return LoginEnd.Refused;
    }

    private static Amqp0MethodPayload SecureMethod(ReadOnlyMemory<byte> challenge)
    {
        var writer = new Amqp0WireWriter();
        writer.WriteLongString(challenge.Span);
        return new(Amqp0ConnectionMethodId.ClassId, Amqp0ConnectionMethodId.Secure, writer.ToArray());
    }

    private static byte[] ReadSecureOkResponse(ReadOnlyMemory<byte> arguments) => new Amqp0WireReader(arguments).ReadLongString();

    private void NoteLoginStep(SaslLoginStep step)
    {
        if (step.CheckedLogin is { } checkedLogin)
        {
            context.Log.Note(checkedLogin.Note);
        }

        if (step.RefusalNote is { } refusalNote)
        {
            context.Log.Note(refusalNote);
        }
    }
}
