using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The server's side of the <c>ssh-userauth</c> service (RFC 4252; ADR-0051, decision 6) on one
/// connection: the <c>SERVICE_REQUEST</c> that starts it, and every login request until one
/// succeeds. It owns the framing, the method list, the attempt count, the fixed user and service
/// and the public-key signature check; <see cref="ISshAuthenticationPolicy"/> decides every
/// credential and supplies the login note, which is written before the answer (ADR-0038).
/// </summary>
/// <remarks>
/// <para>
/// Every <c>USERAUTH_FAILURE</c> names <see cref="MethodList"/> with partial success false; no
/// banner is sent and <c>USERAUTH_PASSWD_CHANGEREQ</c> never is. <c>none</c> succeeds only when
/// the policy accepts it, and a refused <c>none</c> is not counted. <c>password</c> and the one
/// <c>Password: </c> prompt of <c>keyboard-interactive</c> are checked as passwords; a password
/// change request is refused without being checked. A <c>publickey</c> query is answered
/// <c>PK_OK</c> only when the policy finds the key acceptable; a signed request is verified here
/// over the session identifier and the request, then judged by the policy. A <c>KeyAcceptable</c>
/// answer to anything but a query, and any outcome the server does not know, is a refusal.
/// </para>
/// <para>
/// The first request fixes the user and the service: a later one naming another is
/// <c>DISCONNECT</c> 2, and a service other than <c>ssh-connection</c> is <c>DISCONNECT</c> 7.
/// The sixth refused request of a method other than <c>none</c> is answered
/// <c>USERAUTH_FAILURE</c> and then <c>DISCONNECT</c> 14. Requests after the login succeeded are
/// ignored (RFC 4252, section 5.1).
/// </para>
/// </remarks>
/// <param name="transport">The connection's transport, which protects what is written.</param>
/// <param name="policy">Who may log in.</param>
/// <param name="log">Where the login requests and the login notes are written.</param>
/// <param name="sessionIdentifier">The first key exchange's hash, which a public-key signature covers.</param>
/// <param name="stopHeadTimeout">Called once a login succeeds, before <c>USERAUTH_SUCCESS</c> is written (ADR-0051, decision 9).</param>
/// <param name="allowWeakAlgorithms">Whether <c>ssh-rsa</c> and <c>ssh-dss</c> signatures and RSA keys shorter than 2048 bits are accepted (<c>--allow-weak-ssh-algorithms</c>).</param>
internal sealed class SshUserAuthentication(
    SshTransportHandshake transport,
    ISshAuthenticationPolicy policy,
    IExchangeLog log,
    byte[] sessionIdentifier,
    Action stopHeadTimeout,
    bool allowWeakAlgorithms)
{
    /// <summary>The service the client asks for before it logs in.</summary>
    public const string UserAuthService = "ssh-userauth";

    /// <summary>The one service a login may be for.</summary>
    public const string ConnectionService = "ssh-connection";

    /// <summary>How many refused requests end the connection, as OpenSSH's <c>MaxAuthTries</c>.</summary>
    public const int MaxRefusals = 6;

    /// <summary>
    /// The methods every <c>USERAUTH_FAILURE</c> names, in this order, whatever is configured.
    /// </summary>
    public static readonly IReadOnlyList<string> MethodList = Array.AsReadOnly(["publickey", "password", "keyboard-interactive"]);

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private bool isServiceAccepted;
    private byte[]? fixedUser;
    private byte[]? fixedService;
    private bool isAwaitingInfoResponse;
    private int refusals;

    private enum Answer
    {
        Success,
        Failure,
        UncountedFailure,
        Written,
    }

    /// <summary>
    /// Whether a login has succeeded.
    /// </summary>
    public bool IsLoggedIn { get; private set; }

    /// <summary>
    /// Whether <see cref="AnswerAsync"/> answers the message numbered <paramref name="messageNumber"/>:
    /// <c>SERVICE_REQUEST</c>, <c>USERAUTH_REQUEST</c> or <c>USERAUTH_INFO_RESPONSE</c>.
    /// </summary>
    /// <param name="messageNumber">The client's message number.</param>
    /// <returns>Whether the message is this service's.</returns>
    public static bool Answers(byte messageNumber) =>
        messageNumber is SshMessageNumber.ServiceRequest or SshMessageNumber.UserAuthRequest or SshMessageNumber.UserAuthInfoResponse;

    /// <summary>
    /// Answers one of the messages <see cref="Answers"/> names.
    /// </summary>
    /// <param name="payload">The message, message number first.</param>
    /// <param name="cancellationToken">Cuts the check and the write off.</param>
    /// <returns>A task that completes when the answer is written.</returns>
    /// <exception cref="SshDisconnectRequiredException">The message ends the connection, as each answer says.</exception>
    public ValueTask AnswerAsync(byte[] payload, CancellationToken cancellationToken) => payload[0] switch
    {
        SshMessageNumber.ServiceRequest => AnswerServiceRequestAsync(payload, cancellationToken),
        SshMessageNumber.UserAuthRequest => AnswerLoginRequestAsync(payload, cancellationToken),
        _ => AnswerInfoResponseAsync(payload, cancellationToken),
    };

    /// <summary>
    /// Answers a <c>SERVICE_REQUEST</c> (RFC 4253, section 10): <c>SERVICE_ACCEPT</c> for the
    /// first <c>ssh-userauth</c>.
    /// </summary>
    /// <param name="payload">The request, message number first.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the answer is written.</returns>
    /// <exception cref="SshDisconnectRequiredException">
    /// Another service is <c>DISCONNECT</c> 7; <c>ssh-userauth</c> asked for again is <c>DISCONNECT</c> 2.
    /// </exception>
    public async ValueTask AnswerServiceRequestAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var reader = new SshWireReader(payload);
        reader.ReadByte();
        var service = reader.ReadString();
        if (!service.Span.SequenceEqual(Encoding.ASCII.GetBytes(UserAuthService)))
        {
            throw ServiceNotAvailable($"The client asked for the SSH service {SshLogText.Render(service.Span)}; only ssh-userauth is offered.");
        }

        if (isServiceAccepted)
        {
            throw SshDisconnectRequiredException.ProtocolError("The client asked for the ssh-userauth service a second time.");
        }

        isServiceAccepted = true;
        var accept = new SshWireWriter();
        accept.WriteByte(SshMessageNumber.ServiceAccept);
        accept.WriteString(service.Span);
        await transport.WriteAsync(accept.ToArray(), cancellationToken);
    }

    /// <summary>
    /// Answers a <c>USERAUTH_REQUEST</c>.
    /// </summary>
    /// <param name="payload">The request, message number first.</param>
    /// <param name="cancellationToken">Cuts the check and the write off.</param>
    /// <returns>A task that completes when the answer is written.</returns>
    /// <exception cref="SshDisconnectRequiredException">
    /// The request came before <c>ssh-userauth</c> was accepted, changed the user or service,
    /// named a service other than <c>ssh-connection</c>, or was the sixth refused.
    /// </exception>
    public async ValueTask AnswerLoginRequestAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (IsLoggedIn)
        {
            return;
        }

        if (!isServiceAccepted)
        {
            throw SshDisconnectRequiredException.ProtocolError("The client sent a login request before asking for the ssh-userauth service.");
        }

        var reader = new SshWireReader(payload);
        reader.ReadByte();
        FixUserAndService(reader.ReadString(), reader.ReadString());
        var method = Encoding.Latin1.GetString(reader.ReadString().Span);
        isAwaitingInfoResponse = false;
        var answer = await AnswerMethodAsync(method, payload, reader, cancellationToken);

        await FinishAsync(answer, cancellationToken);
    }

    /// <summary>
    /// Answers a <c>USERAUTH_INFO_RESPONSE</c> to the <c>keyboard-interactive</c> prompt: its one
    /// response is checked as a password (RFC 4256, section 3.4).
    /// </summary>
    /// <param name="payload">The response, message number first.</param>
    /// <param name="cancellationToken">Cuts the check and the write off.</param>
    /// <returns>A task that completes when the answer is written.</returns>
    /// <exception cref="SshDisconnectRequiredException">No prompt is awaiting a response, or it was the sixth refused.</exception>
    public async ValueTask AnswerInfoResponseAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (!isAwaitingInfoResponse)
        {
            throw SshDisconnectRequiredException.ProtocolError("The client sent a keyboard-interactive response no prompt asked for.");
        }

        isAwaitingInfoResponse = false;
        var reader = new SshWireReader(payload);
        reader.ReadByte();
        var answer = reader.ReadUInt32() == 1
            ? await CheckPasswordAsync("keyboard-interactive", reader.ReadString(), cancellationToken)
            : Answer.Failure;

        await FinishAsync(answer, cancellationToken);
    }

    private static SshDisconnectRequiredException ServiceNotAvailable(string note) =>
        new(SshDisconnectReason.ServiceNotAvailable, "Service not available", note);

    private static string? Utf8OrNull(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static Answer Judge(SshLoginVerdict verdict, Answer refusal) =>
        verdict.Outcome is SshLoginOutcome.Accepted or SshLoginOutcome.AcceptedUnchecked ? Answer.Success : refusal;

    private void FixUserAndService(ReadOnlyMemory<byte> user, ReadOnlyMemory<byte> service)
    {
        if (fixedUser is not null && (!user.Span.SequenceEqual(fixedUser) || !service.Span.SequenceEqual(fixedService)))
        {
            throw SshDisconnectRequiredException.ProtocolError("The client changed the user or the service between login requests.");
        }

        if (!service.Span.SequenceEqual(Encoding.ASCII.GetBytes(ConnectionService)))
        {
            throw ServiceNotAvailable($"The client asked to log in to the SSH service {SshLogText.Render(service.Span)}; only ssh-connection is offered.");
        }

        fixedUser = user.ToArray();
        fixedService = service.ToArray();
    }

    private ValueTask<Answer> AnswerMethodAsync(string method, byte[] payload, SshWireReader reader, CancellationToken cancellationToken) => method switch
    {
        "none" => ValueTask.FromResult(AnswerNone()),
        "password" => CheckPasswordAsync(reader, cancellationToken),
        "keyboard-interactive" => PromptForPasswordAsync(reader, cancellationToken),
        "publickey" => CheckPublicKeyAsync(payload, reader, cancellationToken),
        _ => ValueTask.FromResult(RefuseUnknownMethod(method)),
    };

    private Answer AnswerNone() => Judge(policy.CheckSshNoneLogin(new SshNoneLogin(Utf8OrNull(fixedUser!))), Answer.UncountedFailure);

    // "SSH login request: <method> for <user>[, key ...]" (ADR-0051, decision 10).
    private void NoteRequest(string method, string keyDescription) =>
        log.Note($"SSH login request: {SshLogText.Render(Encoding.Latin1.GetBytes(method))} for {SshLogText.Render(fixedUser)}{keyDescription}");

    // hostbased (not offered, ADR-0051 decision 6) and any method not known: refused, and counted.
    private Answer RefuseUnknownMethod(string method)
    {
        NoteRequest(method, string.Empty);

        return Answer.Failure;
    }

    // boolean FALSE, string password (RFC 4252, section 8); TRUE is a change request, refused unchecked.
    private async ValueTask<Answer> CheckPasswordAsync(SshWireReader reader, CancellationToken cancellationToken)
    {
        NoteRequest("password", string.Empty);
        if (reader.ReadBoolean())
        {
            return Answer.Failure;
        }

        return await CheckPasswordAsync("password", reader.ReadString(), cancellationToken);
    }

    private async ValueTask<Answer> CheckPasswordAsync(string method, ReadOnlyMemory<byte> password, CancellationToken cancellationToken)
    {
        var verdict = await policy.CheckSshPasswordLoginAsync(new SshPasswordLogin(method, Utf8OrNull(fixedUser!), password), cancellationToken);
        WriteLoginNote(verdict);

        return Judge(verdict, Answer.Failure);
    }

    // string language tag, string submethods (RFC 4256, section 3.1), both ignored; one prompt,
    // "Password: " with echo off, under an empty name and instruction (ADR-0051, decision 6).
    private async ValueTask<Answer> PromptForPasswordAsync(SshWireReader reader, CancellationToken cancellationToken)
    {
        reader.ReadString();
        reader.ReadString();
        NoteRequest("keyboard-interactive", string.Empty);
        var prompt = new SshWireWriter();
        prompt.WriteByte(SshMessageNumber.UserAuthInfoRequest);
        prompt.WriteString(string.Empty);
        prompt.WriteString(string.Empty);
        prompt.WriteString(string.Empty);
        prompt.WriteUInt32(1);
        prompt.WriteString("Password: ");
        prompt.WriteBoolean(false);
        await transport.WriteAsync(prompt.ToArray(), cancellationToken);
        isAwaitingInfoResponse = true;

        return Answer.Written;
    }

    // boolean has-signature, string algorithm, string key blob[, string signature] (RFC 4252, section 7).
    private async ValueTask<Answer> CheckPublicKeyAsync(byte[] payload, SshWireReader reader, CancellationToken cancellationToken)
    {
        var isSigned = reader.ReadBoolean();
        var algorithmBytes = reader.ReadString();
        var keyBlob = reader.ReadString();
        var algorithm = Encoding.Latin1.GetString(algorithmBytes.Span);
        var keyType = new SshWireReader(keyBlob).ReadString();
        var keyDescription = $", key {SshLogText.Render(keyType.Span)} SHA-256 {Convert.ToBase64String(SHA256.HashData(keyBlob.Span))}";
        NoteRequest("publickey", keyDescription);
        if (!SshUserKeySignature.AcceptsKey(algorithm, keyBlob, allowWeakAlgorithms))
        {
            return Answer.Failure;
        }

        if (!isSigned)
        {
            return await QueryPublicKeyAsync(algorithmBytes, keyBlob, cancellationToken);
        }

        var signedLength = reader.Position;
        var proof = SshUserKeySignature.Verifies(algorithm, keyBlob, reader.ReadString(), SignedData(payload.AsSpan(0, signedLength)))
            ? SshPublicKeyProof.ValidSignature
            : SshPublicKeyProof.InvalidSignature;
        var verdict = await policy.CheckSshPublicKeyLoginAsync(new SshPublicKeyLogin(Utf8OrNull(fixedUser!), algorithm, keyBlob, proof), cancellationToken);
        WriteLoginNote(verdict);

        return Judge(verdict, Answer.Failure);
    }

    private async ValueTask<Answer> QueryPublicKeyAsync(ReadOnlyMemory<byte> algorithm, ReadOnlyMemory<byte> keyBlob, CancellationToken cancellationToken)
    {
        var login = new SshPublicKeyLogin(Utf8OrNull(fixedUser!), Encoding.Latin1.GetString(algorithm.Span), keyBlob, SshPublicKeyProof.None);
        var verdict = await policy.CheckSshPublicKeyLoginAsync(login, cancellationToken);
        WriteLoginNote(verdict);
        if (verdict.Outcome != SshLoginOutcome.KeyAcceptable)
        {
            return Answer.Failure;
        }

        var keyAcceptable = new SshWireWriter();
        keyAcceptable.WriteByte(SshMessageNumber.UserAuthPublicKeyOk);
        keyAcceptable.WriteString(algorithm.Span);
        keyAcceptable.WriteString(keyBlob.Span);
        await transport.WriteAsync(keyAcceptable.ToArray(), cancellationToken);

        return Answer.Written;
    }

    // string session identifier, then the request up to its signature (RFC 4252, section 7).
    private byte[] SignedData(ReadOnlySpan<byte> requestWithoutSignature)
    {
        var signed = new SshWireWriter();
        signed.WriteString(sessionIdentifier);
        signed.WriteBytes(requestWithoutSignature);

        return signed.ToArray();
    }

    private void WriteLoginNote(SshLoginVerdict verdict)
    {
        if (verdict.CheckedLogin is { } checkedLogin)
        {
            log.Note(checkedLogin.Note);
        }
    }

    private async ValueTask FinishAsync(Answer answer, CancellationToken cancellationToken)
    {
        switch (answer)
        {
            case Answer.Success:
                IsLoggedIn = true;
                stopHeadTimeout();
                await transport.WriteAsync([SshMessageNumber.UserAuthSuccess], cancellationToken);
                transport.StartDelayedCompression();
                return;
            case Answer.Written:
                return;
        }

        var failure = new SshWireWriter();
        failure.WriteByte(SshMessageNumber.UserAuthFailure);
        failure.WriteNameList(MethodList);
        failure.WriteBoolean(false);
        await transport.WriteAsync(failure.ToArray(), cancellationToken);
        if (answer == Answer.Failure && ++refusals == MaxRefusals)
        {
            throw new SshDisconnectRequiredException(
                SshDisconnectReason.NoMoreAuthMethodsAvailable,
                "Too many authentication failures",
                $"The client was refused {MaxRefusals} SSH logins; the connection was ended.");
        }
    }
}
