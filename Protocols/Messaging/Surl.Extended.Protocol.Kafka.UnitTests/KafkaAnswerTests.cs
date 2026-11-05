namespace Surl.Extended.Protocol.Kafka.UnitTests;

[TestClass]
public sealed class KafkaAnswerTests
{
    [TestMethod]
    public void Respond_Body_IsWrittenAndKeepsTheConnectionOpen()
    {
        var answer = KafkaAnswer.Respond(new byte[] { 0x01 });

        CollectionAssert.AreEqual(new byte[] { 0x01 }, answer.ResponseBody!.Value.ToArray());
        Assert.IsNull(answer.CloseNote);
    }

    [TestMethod]
    public void NoResponse_WritesNothingAndKeepsTheConnectionOpen()
    {
        var answer = KafkaAnswer.NoResponse;

        Assert.IsNull(answer.ResponseBody);
        Assert.IsNull(answer.CloseNote);
    }

    [TestMethod]
    public void Close_Note_IsKeptAndNothingIsWritten()
    {
        var answer = KafkaAnswer.Close("why");

        Assert.AreEqual("why", answer.CloseNote);
        Assert.IsNull(answer.ResponseBody);
    }

    [TestMethod]
    public void Close_EmptyNote_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => KafkaAnswer.Close(string.Empty));

    [TestMethod]
    public void RespondThenClose_BodyAndNote_AreBothKept()
    {
        var answer = KafkaAnswer.RespondThenClose(new byte[] { 0x01 }, "why");

        CollectionAssert.AreEqual(new byte[] { 0x01 }, answer.ResponseBody!.Value.ToArray());
        Assert.AreEqual("why", answer.CloseNote);
        Assert.ThrowsExactly<ArgumentException>(() => KafkaAnswer.RespondThenClose(new byte[] { 0x01 }, string.Empty));
    }
}
