using System.Net;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class DataConnectionContractTests
{
    private static readonly IPEndPoint ControlLocal = new(IPAddress.Loopback, 21);
    private static readonly IPEndPoint ControlRemote = new(IPAddress.Loopback, 50000);

    [TestMethod]
    public void DataConnectionException_CarriesItsFailureAndMessage()
    {
        var exception = new DataConnectionException(DataConnectionFailure.Refused, "not your address");

        Assert.AreEqual(DataConnectionFailure.Refused, exception.Failure);
        Assert.AreEqual("not your address", exception.Message);
    }

    [TestMethod]
    public void DataConnectionFailure_HasTheAdrsFourValuesWithUnavailableFirstAtZero()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                DataConnectionFailure.Unavailable,
                DataConnectionFailure.Refused,
                DataConnectionFailure.Unreachable,
                DataConnectionFailure.TimedOut,
            },
            Enum.GetValues<DataConnectionFailure>());
        Assert.AreEqual(0, (int)Enum.GetValues<DataConnectionFailure>()[0]);
    }

    [TestMethod]
    public async Task RefusingDataConnectionOpener_StartPassiveListenerAsync_ThrowsUnavailable()
    {
        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(async () =>
            await RefusingDataConnectionOpener.Instance.StartPassiveListenerAsync(
                ControlLocal, ControlRemote, CancellationToken.None));

        Assert.AreEqual(DataConnectionFailure.Unavailable, exception.Failure);
    }

    [TestMethod]
    public async Task RefusingDataConnectionOpener_ConnectActiveAsync_ThrowsUnavailable()
    {
        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(async () =>
            await RefusingDataConnectionOpener.Instance.ConnectActiveAsync(
                ControlRemote, new IPEndPoint(IPAddress.Loopback, 50001), TimeSpan.FromSeconds(30), CancellationToken.None));

        Assert.AreEqual(DataConnectionFailure.Unavailable, exception.Failure);
    }

    [TestMethod]
    public void ExchangeContext_DataConnections_DefaultsToTheRefusingOpener()
    {
        var context = NewContext();

        Assert.AreSame(RefusingDataConnectionOpener.Instance, context.DataConnections);
    }

    [TestMethod]
    public void ExchangeContext_DataConnections_CarriesTheOpenerItWasGiven()
    {
        var opener = new InMemoryDataConnections();

        var context = NewContext() with { DataConnections = opener };

        Assert.AreSame(opener, context.DataConnections);
    }

    [TestMethod]
    public void PassiveListenerRequest_KeepsItsArguments()
    {
        var request = new PassiveListenerRequest(ControlLocal, ControlRemote);

        Assert.AreSame(ControlLocal, request.ControlLocal);
        Assert.AreSame(ControlRemote, request.ControlRemote);
    }

    [TestMethod]
    public void ActiveConnectionRequest_KeepsItsArguments()
    {
        var target = new IPEndPoint(IPAddress.Loopback, 50001);

        var request = new ActiveConnectionRequest(ControlRemote, target, TimeSpan.FromSeconds(5));

        Assert.AreSame(ControlRemote, request.ControlRemote);
        Assert.AreSame(target, request.Target);
        Assert.AreEqual(TimeSpan.FromSeconds(5), request.Timeout);
    }

    private static ExchangeContext NewContext() => new(
        1,
        new ListenUrl("ftp", "localhost", 0).WithBoundPort(21),
        ControlLocal,
        ControlRemote,
        new RecordingExchangeLog(),
        TimeProvider.System,
        CancellationToken.None);
}
