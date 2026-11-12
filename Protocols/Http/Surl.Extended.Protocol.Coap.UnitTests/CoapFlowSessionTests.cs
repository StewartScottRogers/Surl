using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapFlowSession"/>: separate responses, their retransmission, and the client's ACK
/// and RST (RFC 7252 sections 4.2, 4.5, 4.8 and 5.2.2; ADR-0097 decision 2), on a clock that moves
/// only when the test moves it.
/// </summary>
[TestClass]
public sealed class CoapFlowSessionTests
{
    private static readonly TimeSpan PatienceLimit = TimeSpan.FromSeconds(10);

    // CON GET, Message ID 1, token C0.
    private static readonly byte[] SlowRequest = [0x41, 0x01, 0x00, 0x01, 0xC0];

    // The separate response: CON 2.05, the flow's first Message ID 0xFFFF, token C0.
    private static readonly byte[] SeparateResponse = [0x41, 0x45, 0xFF, 0xFF, 0xC0];

    private static readonly byte[] EmptyAck = [0x60, 0x00, 0x00, 0x01];

    [TestMethod]
    public async Task ServeAsync_AnswerReadyBeforeTheSecond_IsPiggybacked()
    {
        await using var harness = new Harness(SlowRequest);

        await harness.WaitForTimersAsync(1);
        harness.Answers.Dequeue().SetResult(CoapResponse.Bare(CoapCode.Content));
        await harness.WaitForSendsAsync(1);

        CollectionAssert.AreEqual(new byte[] { 0x61, 0x45, 0x00, 0x01, 0xC0 }, harness.Flow.SentDatagrams.Single());
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task FirstMessageIdAndJitter_SameSeededIdentifiers_AreTheSameEveryRun()
    {
        var first = await SeparateResponseRunAsync();
        var second = await SeparateResponseRunAsync();

        Assert.HasCount(1, first.Random.Fractions);
        CollectionAssert.AreEqual(first.Random.Fractions, second.Random.Fractions);
        CollectionAssert.AreEqual(first.Random.MessageIds, second.Random.MessageIds);
        CollectionAssert.AreEqual(first.SeparateResponse, second.SeparateResponse);
        Assert.AreEqual(first.Random.MessageIds[0], (ushort)((first.SeparateResponse[2] << 8) | first.SeparateResponse[3]));
        Assert.AreEqual(
            CoapTransmissionParameters.InitialTimeout(first.Random.Fractions[0]),
            CoapTransmissionParameters.InitialTimeout(second.Random.Fractions[0]));
    }

    private static async Task<(RecordingCoapRandomSource Random, byte[] SeparateResponse)> SeparateResponseRunAsync()
    {
        var random = new RecordingCoapRandomSource(new IdentifierCoapRandomSource(new Surl.Core.SeededIdentifierRandom(2024).ForExchange(1)));
        await using var harness = new Harness(SlowRequest, random: random);
        await harness.WaitForTimersAsync(1);
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await harness.WaitForSendsAsync(1);
        harness.Answers.Dequeue().SetResult(CoapResponse.Bare(CoapCode.Content));
        await harness.WaitForSendsAsync(2);
        var response = harness.Flow.SentDatagrams[1];
        await harness.StopAsync();
        return (random, response);
    }

    [TestMethod]
    public async Task ServeAsync_SlowAnswer_IsAnEmptyAckThenASeparateConAndADuplicateGetsTheEmptyAckAgain()
    {
        await using var harness = new Harness(SlowRequest);

        await harness.WaitForTimersAsync(1);
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await harness.WaitForSendsAsync(1);
        harness.Flow.DeliverFromClient(SlowRequest);
        await harness.WaitForSendsAsync(2);
        harness.Answers.Dequeue().SetResult(CoapResponse.Bare(CoapCode.Content));
        await harness.WaitForSendsAsync(3);
        harness.Flow.DeliverFromClient(new byte[] { 0x60, 0x00, 0xFF, 0xFF });
        await harness.WaitForNoteAsync("The client's Acknowledgement with Message ID 65535 ends its retransmission.");
        harness.Clock.Advance(TimeSpan.FromMinutes(5));

        var sent = harness.Flow.SentDatagrams;
        Assert.HasCount(3, sent);
        CollectionAssert.AreEqual(EmptyAck, sent[0]);
        CollectionAssert.AreEqual(EmptyAck, sent[1]);
        CollectionAssert.AreEqual(SeparateResponse, sent[2]);
        Assert.HasCount(1, harness.AnswersRequested);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ConDroppedAtOnce_GetsNothingAndItsDuplicateGetsNothing()
    {
        await using var harness = new Harness(SlowRequest);

        await harness.WaitForTimersAsync(1);
        harness.Answers.Dequeue().SetResult(null);
        await harness.WaitForNoteAsync("The request with Message ID 1 is dropped: no answer is sent.");
        harness.Flow.DeliverFromClient(SlowRequest);
        await harness.WaitForNoteAsync("A duplicate Confirmable with Message ID 1: not served again.");

        Assert.IsEmpty(harness.Flow.SentDatagrams);
        Assert.HasCount(1, harness.AnswersRequested);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ConDroppedAfterTheEmptyAck_SendsNoSeparateResponse()
    {
        await using var harness = new Harness(SlowRequest);

        await harness.WaitForTimersAsync(1);
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await harness.WaitForSendsAsync(1);
        harness.Answers.Dequeue().SetResult(null);
        await harness.WaitForNoteAsync("The separate response is dropped: none is sent.");

        Assert.HasCount(1, harness.Flow.SentDatagrams);
        CollectionAssert.AreEqual(EmptyAck, harness.Flow.SentDatagrams[0]);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_NonDropped_GetsNothing()
    {
        await using var harness = new Harness([0x51, 0x01, 0x00, 0x01, 0xC0]);

        await WaitUntilQueuedAsync(harness);
        harness.Answers.Dequeue().SetResult(null);
        await harness.WaitForNoteAsync("The NonConfirmable with Message ID 1 is dropped: no answer is sent.");

        Assert.IsEmpty(harness.Flow.SentDatagrams);
        await harness.StopAsync();
    }

    private static async Task WaitUntilQueuedAsync(Harness harness)
    {
        var started = DateTime.UtcNow;

        while (harness.Answers.Count == 0)
        {
            Assert.IsLessThan(PatienceLimit, DateTime.UtcNow - started, "Waited for the answer to be asked for.");
            await Task.Delay(5);
        }
    }

    [TestMethod]
    public async Task ServeAsync_UnacknowledgedSeparateResponse_IsRetransmittedWithDoublingTimeoutsThenGivenUp()
    {
        await using var harness = new Harness(SlowRequest);
        await harness.SendSeparateResponseAsync();

        foreach (var (timeout, sends) in new[] { (2, 3), (4, 4), (8, 5), (16, 6) })
        {
            await harness.WaitForTimersAsync(1);
            harness.Clock.Advance(TimeSpan.FromSeconds(timeout - 0.001));
            Assert.HasCount(sends - 1, harness.Flow.SentDatagrams);
            harness.Clock.Advance(TimeSpan.FromSeconds(0.001));
            await harness.WaitForSendsAsync(sends);
        }

        await harness.WaitForTimersAsync(1);
        harness.Clock.Advance(TimeSpan.FromSeconds(32));
        await harness.WaitForNoteAsync("Gave up: the separate response with Message ID 65535 was not acknowledged after 4 retransmissions.");

        var sent = harness.Flow.SentDatagrams;
        Assert.HasCount(6, sent);
        foreach (var retransmission in sent.Skip(1))
        {
            CollectionAssert.AreEqual(SeparateResponse, retransmission);
        }

        await harness.StopAsync();
    }

    [TestMethod]
    [DataRow(new byte[] { 0x70, 0x00, 0xFF, 0xFF }, "Reset")]
    [DataRow(new byte[] { 0x60, 0x84, 0xFF, 0xFF }, "Acknowledgement")]
    [DataRow(new byte[] { 0x60, 0xA0, 0xFF, 0xFF }, "Acknowledgement")]
    [DataRow(new byte[] { 0x60, 0x45, 0xFF, 0xFF, 0xFF, 0x01 }, "Acknowledgement")]
    public async Task ServeAsync_MatchingAckOrEmptyRst_EndsTheRetransmission(byte[] answer, string type)
    {
        await using var harness = new Harness(SlowRequest);
        await harness.SendSeparateResponseAsync();

        harness.Flow.DeliverFromClient(answer);
        await harness.WaitForNoteAsync($"The client's {type} with Message ID 65535 ends its retransmission.");
        harness.Clock.Advance(TimeSpan.FromMinutes(5));

        Assert.HasCount(2, harness.Flow.SentDatagrams);
        await harness.StopAsync();
    }

    [TestMethod]
    [DataRow(new byte[] { 0x60, 0x01, 0xFF, 0xFF }, "Acknowledgement")]
    [DataRow(new byte[] { 0x60, 0x20, 0xFF, 0xFF }, "Acknowledgement")]
    [DataRow(new byte[] { 0x60, 0x00, 0xFF, 0xFE }, "Acknowledgement")]
    [DataRow(new byte[] { 0x70, 0x45, 0xFF, 0xFF }, "Reset")]
    public async Task ServeAsync_AckWithARequestOrReservedCodeOrAnotherIdAndRstNotEmpty_AreIgnored(byte[] answer, string type)
    {
        await using var harness = new Harness(SlowRequest);
        await harness.SendSeparateResponseAsync();

        harness.Flow.DeliverFromClient(answer);
        await harness.WaitForNoteAsync($"Ignored: an unmatched {type} with Message ID {(answer[2] << 8) | answer[3]}.");
        await harness.WaitForTimersAsync(1);
        harness.Clock.Advance(TimeSpan.FromSeconds(2));

        await harness.WaitForSendsAsync(3);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_SecondSeparateResponse_WaitsUntilTheFirstIsAcknowledged()
    {
        await using var harness = new Harness(SlowRequest);
        await harness.SendSeparateResponseAsync();

        harness.Flow.DeliverFromClient(new byte[] { 0x41, 0x01, 0x00, 0x02, 0xC1 });
        await harness.WaitForTimersAsync(2);
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await harness.WaitForSendsAsync(3);
        harness.Answers.Dequeue().SetResult(CoapResponse.Bare(CoapCode.Deleted));
        await Task.Delay(50);
        Assert.HasCount(3, harness.Flow.SentDatagrams);
        harness.Flow.DeliverFromClient(new byte[] { 0x60, 0x00, 0xFF, 0xFF });
        await harness.WaitForSendsAsync(4);

        CollectionAssert.AreEqual(new byte[] { 0x60, 0x00, 0x00, 0x02 }, harness.Flow.SentDatagrams[2]);
        CollectionAssert.AreEqual(new byte[] { 0x41, 0x42, 0x00, 0x00, 0xC1 }, harness.Flow.SentDatagrams[3]);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_CancelledWhileASeparateResponseIsUnderWay_EndsWithCancellation()
    {
        await using var harness = new Harness(SlowRequest);

        await harness.WaitForTimersAsync(1);
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await harness.WaitForSendsAsync(1);

        await harness.StopAsync();
        Assert.HasCount(1, harness.Flow.SentDatagrams);
    }

    [TestMethod]
    public async Task ServeAsync_CancelledWhileWaitingForTheAnswer_EndsWithCancellation()
    {
        await using var harness = new Harness(SlowRequest);

        await harness.WaitForTimersAsync(1);

        await harness.StopAsync();
        Assert.IsEmpty(harness.Flow.SentDatagrams);
    }

    [TestMethod]
    public async Task ServeAsync_NonDuplicate_IsIgnoredAndADuplicateCon_AfterExchangeLifetime_IsServedAgain()
    {
        byte[] non = [0x51, 0x01, 0x00, 0x05, 0xC5];
        await using var harness = new Harness(non, completeAnswers: true);

        await harness.WaitForSendsAsync(1);
        harness.Flow.DeliverFromClient(non);
        await harness.WaitForNoteAsync("A duplicate NonConfirmable with Message ID 5: not served again.");
        harness.Clock.Advance(TimeSpan.FromSeconds(145));
        harness.Flow.DeliverFromClient(non);
        await harness.WaitForSendsAsync(2);

        Assert.HasCount(2, harness.AnswersRequested);
        await harness.StopAsync();
    }

    // CON GET with Message ID <id> and token C0+id.
    private static byte[] SlowRequestWithId(byte id) => [0x41, 0x01, 0x00, id, (byte)(0xC0 + id)];

    private static async Task FillTheFlowWithEightRequestsInProgressAsync(Harness harness)
    {
        for (var sent = 1; sent <= 8; sent++)
        {
            await harness.WaitForTimersAsync(1);
            harness.Clock.Advance(TimeSpan.FromSeconds(1));
            await harness.WaitForSendsAsync(sent);

            if (sent < 8)
            {
                harness.Flow.DeliverFromClient(SlowRequestWithId((byte)(sent + 1)));
            }
        }
    }

    [TestMethod]
    public async Task ServeAsync_NinthRequestInProgress_IsAnAckWith503AndMaxAge5()
    {
        await using var harness = new Harness(SlowRequestWithId(1));
        await FillTheFlowWithEightRequestsInProgressAsync(harness);

        harness.Flow.DeliverFromClient(SlowRequestWithId(9));
        await harness.WaitForSendsAsync(9);

        CollectionAssert.AreEqual(new byte[] { 0x61, 0xA3, 0x00, 0x09, 0xC9, 0xD1, 0x01, 0x05 }, harness.Flow.SentDatagrams[8]);
        Assert.HasCount(8, harness.AnswersRequested);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_NonInPlaceOfTheNinthRequestInProgress_IsIgnored()
    {
        await using var harness = new Harness(SlowRequestWithId(1));
        await FillTheFlowWithEightRequestsInProgressAsync(harness);

        harness.Flow.DeliverFromClient(new byte[] { 0x51, 0x01, 0x00, 0x20, 0xC5 });
        await harness.WaitForNoteAsync("Ignored: a NonConfirmable with Message ID 32 while 8 requests are in progress.");

        Assert.HasCount(8, harness.Flow.SentDatagrams);
        Assert.HasCount(8, harness.AnswersRequested);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ADuplicateWhileEightRequestsAreInProgress_IsStillAnsweredFromTheCache()
    {
        await using var harness = new Harness(SlowRequestWithId(1));
        await FillTheFlowWithEightRequestsInProgressAsync(harness);

        harness.Flow.DeliverFromClient(SlowRequestWithId(1));
        await harness.WaitForSendsAsync(9);

        CollectionAssert.AreEqual(EmptyAck, harness.Flow.SentDatagrams[8]);
        await harness.StopAsync();
    }

    private static byte[] DatagramOf(byte[] header, int payloadLength) =>
        [.. header, 0xFF, .. Enumerable.Repeat((byte)'x', payloadLength)];

    [TestMethod]
    public async Task ServeAsync_ConOverMaxMessage_Is413WithSize1AndTheBlock1Hint()
    {
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 0, 0, maxMessageBytes: 200, maxUploadBytes: 1000);
        await using var harness = new Harness(DatagramOf(SlowRequest, 250), limits: limits);

        await harness.WaitForSendsAsync(1);

        // ACK 4.13, Block1 0/0/64 (SZX 2: 64 + 128 fits 200, 128 + 128 does not), Size1 1000.
        CollectionAssert.AreEqual(new byte[] { 0x61, 0x8D, 0x00, 0x01, 0xC0, 0xD1, 0x0E, 0x02, 0xD2, 0x14, 0x03, 0xE8 }, harness.Flow.SentDatagrams.Single());
        Assert.IsEmpty(harness.AnswersRequested);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ConOverAMaxMessageTooSmallForAnyBlockAndNoMaxFilesize_Is413WithNoOption()
    {
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 0, 0, maxMessageBytes: 100, maxUploadBytes: 0);
        await using var harness = new Harness(DatagramOf(SlowRequest, 150), limits: limits);

        await harness.WaitForSendsAsync(1);

        CollectionAssert.AreEqual(new byte[] { 0x61, 0x8D, 0x00, 0x01, 0xC0 }, harness.Flow.SentDatagrams.Single());
        await harness.StopAsync();
    }

    [TestMethod]
    [DataRow(new byte[] { 0x51, 0x01, 0x00, 0x05, 0xC5 }, "NonConfirmable")]
    [DataRow(new byte[] { 0x41, 0x45, 0x00, 0x05, 0xC5 }, "Confirmable")]
    public async Task ServeAsync_NonOrNonRequestOverMaxMessage_IsIgnored(byte[] header, string type)
    {
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 0, 0, maxMessageBytes: 100, maxUploadBytes: 0);
        await using var harness = new Harness(DatagramOf(header, 150), limits: limits);

        await harness.WaitForNoteAsync($"Ignored: a {type} with Message ID 5 of 156 bytes, past --max-message.");

        Assert.IsEmpty(harness.Flow.SentDatagrams);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ConOfAnySizeWithNoMaxMessage_IsServed()
    {
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 0, 0, maxMessageBytes: 0, maxUploadBytes: 0);
        await using var harness = new Harness(DatagramOf(SlowRequest, 5000), limits: limits, completeAnswers: true);

        await harness.WaitForSendsAsync(1);

        CollectionAssert.AreEqual(new byte[] { 0x61, 0x45, 0x00, 0x01, 0xC0 }, harness.Flow.SentDatagrams.Single());
        await harness.StopAsync();
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly CancellationTokenSource exchange = new();
        private readonly CoapFlowSession session;
        private readonly Task serving;
        private readonly bool completeAnswers;

        public Harness(byte[] firstDatagram, bool completeAnswers = false, ExchangeLimits? limits = null, ICoapRandomSource? random = null)
        {
            this.completeAnswers = completeAnswers;
            Flow = new InMemoryDatagramFlow(firstDatagram);
            var context = CoapProtocolServerTests.Context(Log, Clock, exchange.Token) with { Limits = limits ?? ExchangeLimits.Default };
            session = new CoapFlowSession(Flow, context, AnswerAsync, random ?? new UnitTestCoapRandomSource());
            serving = Task.Run(session.ServeAsync);
        }

        public InMemoryDatagramFlow Flow { get; }

        public ManualTimeProvider Clock { get; } = new();

        public RecordingExchangeLog Log { get; } = new();

        public Queue<TaskCompletionSource<CoapResponse?>> Answers { get; } = new();

        public List<CoapMessage> AnswersRequested { get; } = [];

        public async Task SendSeparateResponseAsync()
        {
            await WaitForTimersAsync(1);
            Clock.Advance(TimeSpan.FromSeconds(1));
            await WaitForSendsAsync(1);
            Answers.Dequeue().SetResult(CoapResponse.Bare(CoapCode.Content));
            await WaitForSendsAsync(2);
            CollectionAssert.AreEqual(SeparateResponse, Flow.SentDatagrams[1]);
        }

        public Task WaitForSendsAsync(int count) => WaitUntilAsync(() => Flow.SentDatagrams.Count >= count, $"{count} datagrams sent");

        public Task WaitForTimersAsync(int count) => WaitUntilAsync(() => Clock.PendingTimers >= count, $"{count} timers");

        public Task WaitForNoteAsync(string note) => WaitUntilAsync(() => Log.Notes.Contains(note), note);

        public async Task StopAsync()
        {
            await exchange.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        }

        public async ValueTask DisposeAsync()
        {
            await exchange.CancelAsync();

            try
            {
                await serving;
            }
            catch (OperationCanceledException)
            {
                // The exchange ended as every test ends it.
            }

            exchange.Dispose();
            await Flow.DisposeAsync();
        }

        private static async Task WaitUntilAsync(Func<bool> condition, string what)
        {
            var started = DateTime.UtcNow;

            while (!condition())
            {
                Assert.IsLessThan(PatienceLimit, DateTime.UtcNow - started, $"Waited for {what}.");
                await Task.Delay(5);
            }
        }

        private Task<CoapResponse?> AnswerAsync(CoapMessage request, CoapRequestOptions options, CancellationToken cancellationToken)
        {
            lock (AnswersRequested)
            {
                AnswersRequested.Add(request);
            }

            if (completeAnswers)
            {
                return Task.FromResult<CoapResponse?>(CoapResponse.Bare(CoapCode.Content));
            }

            var answer = new TaskCompletionSource<CoapResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => answer.TrySetCanceled(cancellationToken));
            Answers.Enqueue(answer);
            return answer.Task;
        }
    }
}
