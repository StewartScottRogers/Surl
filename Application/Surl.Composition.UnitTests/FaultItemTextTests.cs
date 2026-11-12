using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>A fault plan's rule written back as one <c>--fault</c> item, as <c>GET /v1/faults</c> lists it.</summary>
[TestClass]
public sealed class FaultItemTextTests
{
    [TestMethod]
    [DataRow(FaultKind.Delay, "delay")]
    [DataRow(FaultKind.Drop, "drop")]
    [DataRow(FaultKind.Error, "error")]
    [DataRow(FaultKind.OutOfSpace, "out-of-space")]
    [DataRow(FaultKind.RefuseLogin, "refuse-login")]
    public void Format_ARuleWithNoKey_IsItsKindWord(FaultKind kind, string expected)
    {
        Assert.AreEqual(expected, FaultItemText.Format(new FaultRule("f1", kind)));
    }

    [TestMethod]
    public void Format_EveryKey_WritesEachInTheReadersOrder()
    {
        var rule = new FaultRule("f1", FaultKind.Drop)
        {
            Listener = "http://127.0.0.1:80",
            Scheme = "http",
            Account = "alice",
            Path = "/up",
            Command = "STOR",
            Stage = FaultStage.Command,
            DelayMilliseconds = 5,
            AfterBytes = 10,
            Reply = "550 no",
            Times = 3,
        };

        Assert.AreEqual(
            "drop;listener=http://127.0.0.1:80;scheme=http;account=alice;path=/up;command=STOR;stage=command;ms=5;after-bytes=10;reply=550 no;times=3",
            FaultItemText.Format(rule));
    }

    [TestMethod]
    public void Format_AValueTheGrammarWouldSplit_PercentEncodesSeparatorsPercentAndControls()
    {
        var rule = new FaultRule("f1", FaultKind.RefuseLogin) { Account = "a;b,c=d%e\tf\u0085g é" };

        Assert.AreEqual("refuse-login;account=a%3Bb%2Cc%3Dd%25e%09f%C2%85g é", FaultItemText.Format(rule));
    }
}
