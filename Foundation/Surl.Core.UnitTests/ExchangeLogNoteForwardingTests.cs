using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed class ExchangeLogNoteForwardingTests
{
    [TestMethod]
    public void RedactingExchangeLog_Note_PassesTheTextToTheInnerLogUnchanged()
    {
        var inner = new RecordingExchangeLog();
        var log = new RedactingExchangeLog(inner, new NoOpRedactor());

        log.Note("AUTH PLAIN secret-looking note");

        CollectionAssert.AreEqual(new[] { "AUTH PLAIN secret-looking note" }, inner.Notes.ToArray());
    }

    [TestMethod]
    public void EventPublishingExchangeLog_Note_PassesTheTextToTheInnerLogAndPublishesNothing()
    {
        var inner = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var log = new EventPublishingExchangeLog(inner, events, 7, new RecordingExchangeLog());

        log.Note("a note");

        CollectionAssert.AreEqual(new[] { "a note" }, inner.Notes.ToArray());
        Assert.AreEqual(0, events.Events.Count);
    }

    private sealed class NoOpRedactor : IReceivedBytesRedactor
    {
        public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
        {
        }

        public void ObserveSent(ReadOnlySpan<byte> sent)
        {
        }

        public void Flush(ArrayBufferWriter<byte> output)
        {
        }
    }
}
