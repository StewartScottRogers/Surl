using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The server's side of the SSH connection protocol after the login (RFC 4254; ADR-0051
/// decision 9, ADR-0054 decisions 2 and 5): it opens <c>session</c> channels, at most
/// <see cref="MaxOpenChannels"/> at once, feeds each its client's data within the window, hands
/// an <c>exec</c> of an SCP command or the <c>sftp</c> subsystem to its handler, and when the
/// handler returns sends <c>exit-status</c>, <c>EOF</c> and <c>CLOSE</c>.
/// </summary>
/// <remarks>
/// <para>
/// Handlers run beside the message loop and write on the connection too, so every write after
/// the login passes one gate, which a key re-exchange holds from its <c>KEXINIT</c> to its
/// <c>NEWKEYS</c> (RFC 4253, section 7.1: nothing else is sent in between).
/// </para>
/// <para>
/// Refused: channel types other than <c>session</c> (<c>OPEN_FAILURE</c> 1 for
/// <c>direct-tcpip</c>, <c>forwarded-tcpip</c> and <c>x11</c>, 3 for the rest), a
/// <c>session</c> past the limit (4), every global request (<c>REQUEST_FAILURE</c> when a reply is
/// wanted), and every channel request but <c>exec</c> and <c>subsystem</c>
/// (<c>CHANNEL_FAILURE</c> when a reply is wanted). An <c>exec</c> that is not an SCP command
/// surl serves is <c>CHANNEL_FAILURE</c> and the channel is closed; a subsystem other than a
/// served <c>sftp</c> is <c>CHANNEL_FAILURE</c>; a second <c>exec</c> or <c>subsystem</c> on a
/// channel is <c>CHANNEL_FAILURE</c>. A message naming a channel that is not open, data past the
/// window or after the client's <c>EOF</c>, and a window widened past 2^32 - 1 are
/// <c>DISCONNECT</c> 2.
/// </para>
/// </remarks>
/// <param name="transport">The transport the messages travel on.</param>
/// <param name="handlers">The handlers an accepted <c>exec</c> or <c>subsystem</c> goes to.</param>
/// <param name="log">Where the channel notes go.</param>
/// <param name="maxLineBytes">The most bytes an <c>exec</c> command may hold (<c>--max-line</c>); 0 means no limit.</param>
internal sealed class SshConnectionProtocol(
    SshTransportHandshake transport,
    ISshChannelHandlers handlers,
    IExchangeLog log,
    long maxLineBytes) : IDisposable
{
    /// <summary>
    /// The most channels open at once on one connection (OpenSSH's <c>MaxSessions</c>).
    /// </summary>
    public const int MaxOpenChannels = 10;

    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly Dictionary<uint, SshSessionChannel> channels = [];
    private readonly List<Task> handlerRuns = [];
    private uint nextChannelNumber;

    /// <summary>
    /// Whether <paramref name="messageNumber"/> is a connection-protocol message the server answers.
    /// </summary>
    /// <param name="messageNumber">The message number.</param>
    /// <returns><see langword="true"/> for a global request, a channel open, and the messages on an open channel.</returns>
    public static bool Answers(byte messageNumber) =>
        ((ReadOnlySpan<byte>)
        [
            SshMessageNumber.GlobalRequest,
            SshMessageNumber.ChannelOpen,
            SshMessageNumber.ChannelWindowAdjust,
            SshMessageNumber.ChannelData,
            SshMessageNumber.ChannelExtendedData,
            SshMessageNumber.ChannelEof,
            SshMessageNumber.ChannelClose,
            SshMessageNumber.ChannelRequest,
        ]).Contains(messageNumber);

    /// <summary>
    /// Answers one message <see cref="Answers"/> accepts.
    /// </summary>
    /// <param name="payload">The message, number first.</param>
    /// <param name="cancellationToken">Cuts the answer off, and a handler started by it.</param>
    /// <returns>A task that completes when the message is answered.</returns>
    /// <exception cref="SshDisconnectRequiredException">The message is refused with <c>DISCONNECT</c> 2.</exception>
    public ValueTask AnswerAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var reader = new SshWireReader(payload);
        var messageNumber = reader.ReadByte();

        return messageNumber switch
        {
            SshMessageNumber.GlobalRequest => AnswerGlobalRequestAsync(reader, cancellationToken),
            SshMessageNumber.ChannelOpen => AnswerOpenAsync(reader, cancellationToken),
            _ => AnswerOnChannelAsync(messageNumber, OpenChannel(reader.ReadUInt32()), reader, cancellationToken),
        };
    }

    /// <summary>
    /// Writes <paramref name="payload"/> through the write gate.
    /// </summary>
    /// <param name="payload">The message.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the message is written.</returns>
    public async ValueTask WriteAsync(byte[] payload, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            await transport.WriteAsync(payload, cancellationToken);
        }
        finally
        {
            writeGate.Release();
        }
    }

    /// <summary>
    /// Writes <paramref name="payload"/> on <paramref name="channel"/> through the write gate,
    /// unless the server has already sent the channel's <c>CLOSE</c>, or the channel has ended
    /// and the message is not that <c>CLOSE</c>.
    /// </summary>
    /// <param name="channel">The channel the message is on.</param>
    /// <param name="payload">The message.</param>
    /// <param name="closes">Whether the message is the channel's <c>CLOSE</c>.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>Whether the message was written.</returns>
    public async ValueTask<bool> WriteOnChannelAsync(SshSessionChannel channel, byte[] payload, bool closes, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            if (channel.CloseSent || (!closes && channel.IsEnded))
            {
                return false;
            }

            channel.CloseSent = closes;
            await transport.WriteAsync(payload, cancellationToken);

            return true;
        }
        finally
        {
            writeGate.Release();
        }
    }

    /// <summary>
    /// Runs a key re-exchange holding the write gate, so no handler writes in the middle of it.
    /// </summary>
    /// <param name="clientKexInit">The client's <c>KEXINIT</c> when it started the re-exchange, else <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cuts the exchange off.</param>
    /// <returns>A task that completes when both directions use the new keys.</returns>
    public async Task ReExchangeAsync(byte[]? clientKexInit, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            await transport.ReExchangeAsync(clientKexInit, cancellationToken);
        }
        finally
        {
            writeGate.Release();
        }
    }

    /// <summary>
    /// Ends every channel because the connection is ending, and waits for their handlers to return.
    /// Nothing more is written on any channel.
    /// </summary>
    /// <returns>A task that completes when every handler has returned.</returns>
    public async Task EndChannelsAsync()
    {
        foreach (var channel in channels.Values)
        {
            channel.End();
        }

        await Task.WhenAll(handlerRuns);
    }

    /// <inheritdoc/>
    public void Dispose() => writeGate.Dispose();

    private static string Render(ReadOnlyMemory<byte> bytes) => SshLogText.Render(bytes.Span);

    private SshSessionChannel OpenChannel(uint number) =>
        channels.TryGetValue(number, out var channel)
            ? channel
            : throw SshDisconnectRequiredException.ProtocolError($"The client named SSH channel {number}, which is not open.");

    private async ValueTask AnswerGlobalRequestAsync(SshWireReader reader, CancellationToken cancellationToken)
    {
        var name = reader.ReadString();
        var wantsReply = reader.ReadBoolean();
        log.Note($"SSH global request refused: {Render(name)}");
        if (wantsReply)
        {
            await WriteAsync([SshMessageNumber.RequestFailure], cancellationToken);
        }
    }

    private async ValueTask AnswerOpenAsync(SshWireReader reader, CancellationToken cancellationToken)
    {
        var type = reader.ReadString();
        var clientNumber = reader.ReadUInt32();
        var clientWindow = reader.ReadUInt32();
        var clientMaxPacket = reader.ReadUInt32();
        var refusal = RefusalOfOpen(Encoding.Latin1.GetString(type.Span));
        if (refusal is { } reason)
        {
            var description = OpenFailureDescription(reason);
            log.Note($"SSH channel open refused: {Render(type)}: {description}");
            var failure = new SshWireWriter();
            failure.WriteByte(SshMessageNumber.ChannelOpenFailure);
            failure.WriteUInt32(clientNumber);
            failure.WriteUInt32((uint)reason);
            failure.WriteString(description);
            failure.WriteString(string.Empty);
            await WriteAsync(failure.ToArray(), cancellationToken);

            return;
        }

        var channel = new SshSessionChannel(this, nextChannelNumber++, clientNumber, clientWindow, clientMaxPacket);
        channels.Add(channel.Number, channel);
        log.Note($"SSH session channel {channel.Number} opened");
        var confirmation = new SshWireWriter();
        confirmation.WriteByte(SshMessageNumber.ChannelOpenConfirmation);
        confirmation.WriteUInt32(clientNumber);
        confirmation.WriteUInt32(channel.Number);
        confirmation.WriteUInt32(SshSessionChannel.WindowBytes);
        confirmation.WriteUInt32(SshSessionChannel.MaxPacketBytes);
        await WriteAsync(confirmation.ToArray(), cancellationToken);
    }

    private SshChannelOpenFailureReason? RefusalOfOpen(string type) => type switch
    {
        "session" when channels.Count < MaxOpenChannels => null,
        "session" => SshChannelOpenFailureReason.ResourceShortage,
        "direct-tcpip" or "forwarded-tcpip" or "x11" => SshChannelOpenFailureReason.AdministrativelyProhibited,
        _ => SshChannelOpenFailureReason.UnknownChannelType,
    };

    private static string OpenFailureDescription(SshChannelOpenFailureReason reason) => reason switch
    {
        SshChannelOpenFailureReason.ResourceShortage => "Too many channels",
        SshChannelOpenFailureReason.AdministrativelyProhibited => "Administratively prohibited",
        _ => "Unknown channel type",
    };

    private async ValueTask AnswerOnChannelAsync(byte messageNumber, SshSessionChannel channel, SshWireReader reader, CancellationToken cancellationToken)
    {
        switch (messageNumber)
        {
            case SshMessageNumber.ChannelWindowAdjust:
                channel.AdjustClientWindow(reader.ReadUInt32());
                break;
            case SshMessageNumber.ChannelData:
                channel.ReceiveData(reader.ReadString());
                break;
            case SshMessageNumber.ChannelExtendedData:
                reader.ReadUInt32();
                await channel.GrantAsync(channel.ReceiveDiscardedData(reader.ReadString().Length), cancellationToken);
                break;
            case SshMessageNumber.ChannelEof:
                channel.ReceiveEof();
                break;
            case SshMessageNumber.ChannelClose:
                await AnswerCloseAsync(channel, cancellationToken);
                break;
            default:
                await AnswerChannelRequestAsync(channel, reader, cancellationToken);
                break;
        }
    }

    // The client's CLOSE: its data ends, the server's CLOSE goes back unless it went already,
    // and the channel's number is free (RFC 4254, section 5.3).
    private async ValueTask AnswerCloseAsync(SshSessionChannel channel, CancellationToken cancellationToken)
    {
        channel.End();
        await WriteOnChannelAsync(channel, channel.Message(SshMessageNumber.ChannelClose), closes: true, cancellationToken);
        channels.Remove(channel.Number);
    }

    private async ValueTask AnswerChannelRequestAsync(SshSessionChannel channel, SshWireReader reader, CancellationToken cancellationToken)
    {
        var type = reader.ReadString();
        var wantsReply = reader.ReadBoolean();
        var typeName = Encoding.Latin1.GetString(type.Span);
        if (typeName is not ("exec" or "subsystem"))
        {
            log.Note($"SSH channel request refused: {Render(type)}");
            await ReplyAsync(channel, false, wantsReply, cancellationToken);
        }
        else if (channel.HasStarted)
        {
            log.Note($"SSH {typeName} refused: channel {channel.Number} already runs one");
            await ReplyAsync(channel, false, wantsReply, cancellationToken);
        }
        else if (typeName == "exec")
        {
            await AnswerExecAsync(channel, reader.ReadString(), wantsReply, cancellationToken);
        }
        else
        {
            await AnswerSubsystemAsync(channel, reader.ReadString(), wantsReply, cancellationToken);
        }
    }

    // ADR-0054 decision 2: an exec that is not an SCP command surl serves is refused and its
    // channel closed.
    private async ValueTask AnswerExecAsync(SshSessionChannel channel, ReadOnlyMemory<byte> command, bool wantsReply, CancellationToken cancellationToken)
    {
        var scpCommand = SshScpCommand.Parse(command.Span, maxLineBytes, out var refusal);
        var handler = scpCommand is null ? null : handlers.ForScp(scpCommand);
        if (handler is null)
        {
            var reason = scpCommand is null ? SshLogText.Render(Encoding.UTF8.GetBytes(refusal)) : "SCP is not served";
            log.Note($"SCP command refused: {reason}: {Render(command)}");
            await ReplyAsync(channel, false, wantsReply, cancellationToken);
            await WriteOnChannelAsync(channel, channel.Message(SshMessageNumber.ChannelClose), closes: true, cancellationToken);

            return;
        }

        await StartAsync(channel, handler, $"SSH exec started on channel {channel.Number}: {Render(command)}", wantsReply, cancellationToken);
    }

    // ADR-0054 decision 5: the subsystem named exactly sftp, when SFTP is served.
    private async ValueTask AnswerSubsystemAsync(SshSessionChannel channel, ReadOnlyMemory<byte> name, bool wantsReply, CancellationToken cancellationToken)
    {
        var handler = name.Span.SequenceEqual("sftp"u8) ? handlers.ForSftp() : null;
        if (handler is null)
        {
            log.Note($"SSH subsystem refused: {Render(name)}");
            await ReplyAsync(channel, false, wantsReply, cancellationToken);

            return;
        }

        await StartAsync(channel, handler, $"SSH subsystem started on channel {channel.Number}: sftp", wantsReply, cancellationToken);
    }

    private async ValueTask ReplyAsync(SshSessionChannel channel, bool success, bool wantsReply, CancellationToken cancellationToken)
    {
        if (wantsReply)
        {
            var reply = success ? SshMessageNumber.ChannelSuccess : SshMessageNumber.ChannelFailure;
            await WriteOnChannelAsync(channel, channel.Message(reply), closes: false, cancellationToken);
        }
    }

    // The reply goes before the handler starts, so it precedes the handler's first data.
    private async ValueTask StartAsync(SshSessionChannel channel, ISshChannelHandler handler, string note, bool wantsReply, CancellationToken cancellationToken)
    {
        channel.HasStarted = true;
        log.Note(note);
        await ReplyAsync(channel, true, wantsReply, cancellationToken);
        handlerRuns.Add(RunHandlerAsync(channel, handler, cancellationToken));
    }

    // Runs the handler, then sends exit-status, EOF and CLOSE (ADR-0054, decision 2). A handler
    // that throws ends with exit status 1; a connection that is cut off meanwhile ends it silently.
    private async Task RunHandlerAsync(SshSessionChannel channel, ISshChannelHandler handler, CancellationToken cancellationToken)
    {
        uint exitStatus;
        try
        {
            exitStatus = await handler.RunAsync(channel, cancellationToken);
        }
        catch (Exception failure)
        {
            log.Note($"SSH channel {channel.Number} handler failed: {failure.Message}");
            exitStatus = 1;
        }

        try
        {
            await FinishAsync(channel, exitStatus, cancellationToken);
        }
        catch (Exception)
        {
            // The connection was cut off; nothing more can be sent on it.
        }
    }

    private async Task FinishAsync(SshSessionChannel channel, uint exitStatus, CancellationToken cancellationToken)
    {
        var status = new SshWireWriter();
        status.WriteByte(SshMessageNumber.ChannelRequest);
        status.WriteUInt32(channel.ClientNumber);
        status.WriteString("exit-status");
        status.WriteBoolean(false);
        status.WriteUInt32(exitStatus);
        if (await WriteOnChannelAsync(channel, status.ToArray(), closes: false, cancellationToken))
        {
            log.Note($"SSH channel {channel.Number} ended: exit status {exitStatus}");
            await WriteOnChannelAsync(channel, channel.Message(SshMessageNumber.ChannelEof), closes: false, cancellationToken);
            await WriteOnChannelAsync(channel, channel.Message(SshMessageNumber.ChannelClose), closes: true, cancellationToken);
        }
    }
}
