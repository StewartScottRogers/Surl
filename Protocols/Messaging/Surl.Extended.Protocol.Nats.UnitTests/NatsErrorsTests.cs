namespace Surl.Extended.Protocol.Nats.UnitTests;

[TestClass]
public sealed class NatsErrorsTests
{
    [TestMethod]
    public void Line_Text_IsTheMinusErrLineWithTheTextInSingleQuotes()
    {
        CollectionAssert.AreEqual("-ERR 'Parser Error'\r\n"u8.ToArray(), NatsErrors.Line(NatsErrors.ParserError));
    }
}
