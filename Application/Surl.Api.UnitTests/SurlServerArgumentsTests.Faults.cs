using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-808: each SurlFault is a spelling of one --fault item, read back by surl's own reader (ADR-0124, decision 4).
public sealed partial class SurlServerArgumentsTests
{
    [TestMethod]
    public void Compose_ADelayOnAListener_IsTheDelayItemWithItsListenerAndMilliseconds()
    {
        var fault = SurlFault.Delay(TimeSpan.FromMilliseconds(250)) with { Listener = HttpListenUrl };

        var actual = AssertFaultComposesAs(fault, "delay;listener=http%3A%2F%2F127.0.0.1%3A0;ms=250");

        Assert.AreEqual(new ConfiguredFault(FaultKind.Delay) { Listener = HttpListenUrl, DelayMilliseconds = 250 }, actual);
    }

    [TestMethod]
    public void Compose_ADropAtACommandTwice_IsTheDropItemWithItsCommandStageAndTimes()
    {
        var fault = SurlFault.DropConnection() with { Command = "RETR", Stage = FaultStage.Command, Times = 2 };

        var actual = AssertFaultComposesAs(fault, "drop;command=RETR;stage=command;times=2");

        Assert.AreEqual(new ConfiguredFault(FaultKind.Drop) { Command = "RETR", Stage = FaultStage.Command, Times = 2 }, actual);
    }

    [TestMethod]
    public void Compose_ADropAfterBytes_IsTheDropItemWithAfterBytes()
    {
        var fault = SurlFault.DropConnection() with { AfterBytes = 10 };

        var actual = AssertFaultComposesAs(fault, "drop;after-bytes=10");

        Assert.AreEqual(new ConfiguredFault(FaultKind.Drop) { AfterBytes = 10 }, actual);
    }

    [TestMethod]
    public void Compose_ARefusedLoginForAnAccount_IsTheRefuseLoginItemWithItsSchemeAndAccount()
    {
        var fault = SurlFault.RefuseLogin() with { Scheme = "smtp", Account = "alice" };

        var actual = AssertFaultComposesAs(fault, "refuse-login;scheme=smtp;account=alice");

        Assert.AreEqual(new ConfiguredFault(FaultKind.RefuseLogin) { Scheme = "smtp", Account = "alice" }, actual);
    }

    [TestMethod]
    public void Compose_AProtocolErrorOnAPath_IsTheErrorItemWithItsValuesPercentEncoded()
    {
        var fault = SurlFault.ProtocolError("503 Busy; later") with { Scheme = "http", Path = "/a b=c" };

        var actual = AssertFaultComposesAs(fault, "error;scheme=http;path=%2Fa%20b%3Dc;reply=503%20Busy%3B%20later");

        Assert.AreEqual(new ConfiguredFault(FaultKind.Error) { Scheme = "http", Path = "/a b=c", Reply = "503 Busy; later" }, actual);
    }

    [TestMethod]
    public void Compose_OutOfSpaceOnAPath_IsTheOutOfSpaceItemWithItsPath()
    {
        var fault = SurlFault.OutOfSpace() with { Path = "INBOX" };

        var actual = AssertFaultComposesAs(fault, "out-of-space;path=INBOX");

        Assert.AreEqual(new ConfiguredFault(FaultKind.OutOfSpace) { Path = "INBOX" }, actual);
    }

    [TestMethod]
    public void Compose_TwoFaults_GivesOneFaultOptionEachInOrderAfterTheLimits()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.MaxConnections = 5;
        options.Faults.Add(SurlFault.DropConnection());
        options.Faults.Add(SurlFault.RefuseLogin());

        var actual = SurlServerArguments.Compose(options);

        CollectionAssert.AreEqual(
            new[] { HttpListenUrl, "--max-connections", "5", "--fault", "drop", "--fault", "refuse-login" }, actual.ToArray());
    }

    [TestMethod]
    public void Compose_NoFault_AddsNoWord()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);

        var actual = SurlServerArguments.Compose(options);

        CollectionAssert.AreEqual(new[] { HttpListenUrl }, actual.ToArray());
        Assert.IsEmpty(options.Faults);
    }

    [TestMethod]
    public void Parse_AFaultSurlRefuses_ThrowsSurlsRefusal()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Faults.Add(SurlFault.ProtocolError("503 Busy"));

        var exception = Assert.ThrowsExactly<SurlServerStartException>(() => SurlServerArguments.Parse(options, []));

        Assert.AreEqual(SurlExitCode.FailedInit, exception.ExitCode);
        Assert.AreEqual("option --fault: kind error needs key scheme or key listener", exception.Lines[0]);
    }

    [TestMethod]
    public void ThrowIfMisused_ANullFault_ThrowsArgumentException()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Faults.Add(null!);

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    private static ConfiguredFault AssertFaultComposesAs(SurlFault fault, string item)
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Faults.Add(fault);

        CollectionAssert.AreEqual(new[] { HttpListenUrl, "--fault", item }, SurlServerArguments.Compose(options).ToArray());
        var expected = CommandLineParser.Parse([HttpListenUrl, "--fault", item]).CommandLine!.Faults.Single();
        var actual = SurlServerArguments.Parse(options, []).Faults.Single();
        Assert.AreEqual(expected, actual);
        return actual;
    }
}
