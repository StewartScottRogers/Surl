namespace Surl.Protocol.Smtp;

[TestClass]
public sealed class SmtpMailParametersTests
{
    [TestMethod]
    [DataRow("", DisplayName = "no parameter")]
    [DataRow("SIZE=53", DisplayName = "SIZE")]
    [DataRow("size=104857600", DisplayName = "SIZE at the limit, lower case")]
    [DataRow("BODY=7BIT", DisplayName = "BODY=7BIT")]
    [DataRow("body=8bitmime", DisplayName = "BODY=8BITMIME, lower case")]
    [DataRow("SMTPUTF8", DisplayName = "SMTPUTF8")]
    [DataRow("AUTH=<a@x>", DisplayName = "AUTH with a mailbox")]
    [DataRow("AUTH=<>", DisplayName = "AUTH with none")]
    [DataRow("SIZE=53 BODY=8BITMIME SMTPUTF8 AUTH=<>", DisplayName = "every parameter once")]
    public void FindRefusal_AcceptedParameters_ReturnsNull(string parameters)
    {
        Assert.IsNull(SmtpMailParameters.FindRefusal(Split(parameters), 104857600));
    }

    [TestMethod]
    [DataRow("SIZE=104857601", "552 5.3.4 Message size exceeds the size limit", DisplayName = "SIZE past the limit")]
    [DataRow("SIZE=99999999999999999999", "552 5.3.4 Message size exceeds the size limit", DisplayName = "SIZE of twenty digits")]
    [DataRow("SIZE=999999999999999999999", "501 5.5.4 Invalid SIZE parameter", DisplayName = "SIZE of twenty-one digits")]
    [DataRow("SIZE=", "501 5.5.4 Invalid SIZE parameter", DisplayName = "SIZE with no value")]
    [DataRow("SIZE", "501 5.5.4 Invalid SIZE parameter", DisplayName = "SIZE with no equals sign")]
    [DataRow("SIZE=-1", "501 5.5.4 Invalid SIZE parameter", DisplayName = "SIZE negative")]
    [DataRow("SIZE=1x", "501 5.5.4 Invalid SIZE parameter", DisplayName = "SIZE not a number")]
    [DataRow("BODY=BINARYMIME", "501 5.5.4 Invalid BODY parameter", DisplayName = "BODY=BINARYMIME")]
    [DataRow("BODY", "501 5.5.4 Invalid BODY parameter", DisplayName = "BODY with no value")]
    [DataRow("SMTPUTF8=yes", "555 5.5.4 Unsupported parameter", DisplayName = "SMTPUTF8 with a value")]
    [DataRow("AUTH", "555 5.5.4 Unsupported parameter", DisplayName = "AUTH with no value")]
    [DataRow("RET=HDRS", "555 5.5.4 Unsupported parameter", DisplayName = "an unknown parameter")]
    [DataRow("SIZE=1 size=2", "555 5.5.4 Unsupported parameter", DisplayName = "SIZE twice")]
    [DataRow("FOO=1 SIZE=x", "555 5.5.4 Unsupported parameter", DisplayName = "the first refusal wins")]
    public void FindRefusal_RefusedParameter_ReturnsItsReply(string parameters, string reply)
    {
        Assert.AreEqual(reply, SmtpMailParameters.FindRefusal(Split(parameters), 104857600));
    }

    [TestMethod]
    public void FindRefusal_AnySizeWithNoLimit_ReturnsNull()
    {
        Assert.IsNull(SmtpMailParameters.FindRefusal(["SIZE=99999999999999999999"], 0));
    }

    private static string[] Split(string parameters) => parameters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
