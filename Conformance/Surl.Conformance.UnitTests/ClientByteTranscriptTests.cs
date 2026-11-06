namespace Surl.Conformance;

[TestClass]
public sealed class ClientByteTranscriptTests
{
    [TestMethod]
    public void ToConnections_BytesRecordedOnInterleavedConnections_AreKeptPerConnectionInConnectionOrder()
    {
        var transcript = new ClientByteTranscript();
        var first = transcript.BeginConnection();
        var second = transcript.BeginConnection();

        transcript.Record(second, "B1"u8);
        transcript.Record(first, "A1"u8);
        transcript.Record(second, "B2"u8);
        transcript.Record(first, "A2"u8);

        Assert.AreEqual(0, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual(2, transcript.ConnectionCount);
        var connections = transcript.ToConnections();
        CollectionAssert.AreEqual("A1A2"u8.ToArray(), connections[0]);
        CollectionAssert.AreEqual("B1B2"u8.ToArray(), connections[1]);
    }

    [TestMethod]
    public void ToConnections_NothingBegun_IsEmpty_AndAConnectionWithNothingSentIsEmptyBytes()
    {
        var transcript = new ClientByteTranscript();

        Assert.IsEmpty(transcript.ToConnections());
        transcript.BeginConnection();
        Assert.IsEmpty(transcript.ToConnections().Single());
    }

    [TestMethod]
    public void ToConnections_IsACopy_ThatLaterRecordingDoesNotChange()
    {
        var transcript = new ClientByteTranscript();
        var connection = transcript.BeginConnection();
        transcript.Record(connection, "A"u8);

        var before = transcript.ToConnections();
        transcript.Record(connection, "B"u8);

        CollectionAssert.AreEqual("A"u8.ToArray(), before[0]);
        CollectionAssert.AreEqual("AB"u8.ToArray(), transcript.ToConnections()[0]);
    }

    [TestMethod]
    public void Record_NoSuchConnection_ThrowsArgumentOutOfRangeException()
    {
        var transcript = new ClientByteTranscript();
        transcript.BeginConnection();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => transcript.Record(-1, "A"u8));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => transcript.Record(1, "A"u8));
    }
}
