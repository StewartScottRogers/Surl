using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// RFC 1350's lock step on one flow: send a packet, wait for the client's answer to it, and
/// send the packet again when the answer does not come within the retransmission timeout.
/// </summary>
/// <param name="flow">The flow, already moved to the server's transfer port.</param>
/// <param name="context">The exchange: its log, clock and cancellation.</param>
/// <param name="retransmissionTimeout">How long to wait for an answer before sending the packet again.</param>
internal sealed class TftpLockStep(IDatagramFlow flow, ExchangeContext context, TimeSpan retransmissionTimeout)
{
    /// <summary>
    /// How many times a packet is sent again before the server gives up on the client.
    /// </summary>
    public const int MaximumRetransmissions = 5;

    private enum AnswerOutcome
    {
        Answered,
        TimedOut,
        ClientError,
        IllegalPacket,
    }

    /// <summary>
    /// Sends <paramref name="packet"/>, again after each retransmission timeout, until the
    /// client answers it with a packet of <paramref name="answerOpcode"/> and
    /// <paramref name="blockNumber"/>. A packet of that opcode with another block number, such
    /// as a duplicate of the one before, is ignored, so it never triggers a second copy of a
    /// packet (RFC 1123 section 4.2.3.1).
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="answerOpcode">The opcode of the answer: ACK for a read, DATA for a write.</param>
    /// <param name="blockNumber">The block number the answer carries.</param>
    /// <param name="awaited">What is awaited, for the log, e.g. <c>block 1 awaited its ACK</c>.</param>
    /// <returns>
    /// The answer; or <see langword="null"/> when the transfer is over: the client sent an
    /// ERROR, sent any other packet (answered with ERROR 4), or stayed silent through
    /// <see cref="MaximumRetransmissions"/> retransmissions. Each is logged.
    /// </returns>
    public async Task<ReadOnlyMemory<byte>?> SendUntilAnsweredAsync(byte[] packet, int answerOpcode, ushort blockNumber, string awaited)
    {
        for (var sends = 0; sends <= MaximumRetransmissions; sends++)
        {
            await SendAsync(packet);
            var (outcome, answer) = await AwaitAnswerAsync(answerOpcode, blockNumber);
            if (outcome != AnswerOutcome.TimedOut)
            {
                return await ConcludeAsync(outcome, answer, awaited);
            }
        }

        context.Log.Note($"Nothing came while {awaited}, through {MaximumRetransmissions} retransmissions {retransmissionTimeout.TotalSeconds} seconds apart; the transfer was abandoned.");
        return null;
    }

    private async Task<ReadOnlyMemory<byte>?> ConcludeAsync(AnswerOutcome outcome, ReadOnlyMemory<byte> answer, string awaited)
    {
        if (outcome == AnswerOutcome.Answered)
        {
            return answer;
        }

        if (outcome == AnswerOutcome.ClientError)
        {
            context.Log.Note($"The client ended the transfer with an ERROR while {awaited}.");
            return null;
        }

        context.Log.Note($"The client sent a packet that does not belong to the transfer while {awaited}; the transfer was ended with ERROR 4.");
        await SendAsync(TftpPacket.ForError(TftpErrorCode.IllegalOperation, "Illegal TFTP operation"));
        return null;
    }

    /// <summary>
    /// Sends one packet on the flow.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>A task that completes once it is sent.</returns>
    public ValueTask SendAsync(byte[] packet) => flow.SendAsync(packet, context.CancellationToken);

    private async Task<(AnswerOutcome Outcome, ReadOnlyMemory<byte> Answer)> AwaitAnswerAsync(int answerOpcode, ushort blockNumber)
    {
        using var timeout = new CancellationTokenSource(retransmissionTimeout, context.TimeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeout.Token);
        try
        {
            while (true)
            {
                var datagram = await flow.ReceiveAsync(linked.Token);
                if (Classify(datagram.Span, answerOpcode, blockNumber) is { } outcome)
                {
                    return (outcome, datagram);
                }
            }
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return (AnswerOutcome.TimedOut, ReadOnlyMemory<byte>.Empty);
        }
    }

    /// <summary>
    /// What a datagram from the client means while the answer of <paramref name="answerOpcode"/>
    /// and <paramref name="blockNumber"/> is awaited; <see langword="null"/> for a packet of
    /// that opcode with another block number, which is ignored.
    /// </summary>
    private static AnswerOutcome? Classify(ReadOnlySpan<byte> datagram, int answerOpcode, ushort blockNumber) =>
        (TftpPacket.ReadOpcode(datagram), TftpPacket.ReadBlockNumber(datagram)) switch
        {
            (var opcode, var block) when opcode == answerOpcode && block == blockNumber => AnswerOutcome.Answered,
            (var opcode, >= 0) when opcode == answerOpcode => null,
            (TftpPacket.Error, _) => AnswerOutcome.ClientError,
            _ => AnswerOutcome.IllegalPacket,
        };
}
