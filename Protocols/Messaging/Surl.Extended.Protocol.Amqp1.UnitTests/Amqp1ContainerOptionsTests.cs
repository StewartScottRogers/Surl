using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The container's id and idle time-out (ADR-0107 decision 3).
/// </summary>
[TestClass]
public sealed class Amqp1ContainerOptionsTests
{
    [TestMethod]
    public void Default_IdleTimeout_Is120SecondsAdvertisedAsHalf()
    {
        var options = Amqp1ContainerOptions.Default;

        Assert.AreEqual(TimeSpan.FromSeconds(120), options.SilenceLimit);
        Assert.AreEqual(60000u, options.AdvertisedIdleTimeOutMilliseconds);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "zero")]
    [DataRow(-1, DisplayName = "infinite")]
    public void IdleTimeoutOff_IsNeverSilentAndAdvertisesNone(int milliseconds)
    {
        var options = new Amqp1ContainerOptions { IdleTimeout = TimeSpan.FromMilliseconds(milliseconds) };

        Assert.AreEqual(Timeout.InfiniteTimeSpan, options.SilenceLimit);
        Assert.IsNull(options.AdvertisedIdleTimeOutMilliseconds);
    }

    [TestMethod]
    public void AdvertisedIdleTimeOut_BeyondAUint_IsUintMax()
    {
        var options = new Amqp1ContainerOptions { IdleTimeout = TimeSpan.FromDays(100) };

        Assert.AreEqual(uint.MaxValue, options.AdvertisedIdleTimeOutMilliseconds);
    }

    [TestMethod]
    public void NewContainerId_IsSurlAnd32LowerCaseHexDigitsDrawnFromTheSource()
    {
        var first = Amqp1ContainerOptions.NewContainerId(new UnitTestSeededIdentifierRandom(1));

        Assert.MatchesRegex("^surl-[0-9a-f]{32}$", first);
        Assert.AreEqual(first, Amqp1ContainerOptions.NewContainerId(new UnitTestSeededIdentifierRandom(1)));
        Assert.AreNotEqual(first, Amqp1ContainerOptions.NewContainerId(new UnitTestSeededIdentifierRandom(2)));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1ContainerOptions.NewContainerId(null!));
    }

    [TestMethod]
    public void Default_HoldsNoDrawnContainerId() => Assert.IsNull(Amqp1ContainerOptions.Default.ContainerId);

    [TestMethod]
    public void TwoContainers_UnseededDefaults_SendDifferentContainerIds()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider());

        var first = new Amqp1Container(new UnitTestAmqp1SaslPolicy(), bus);
        var second = new Amqp1Container(new UnitTestAmqp1SaslPolicy(), bus);

        Assert.MatchesRegex("^surl-[0-9a-f]{32}$", first.ContainerId);
        Assert.AreNotEqual(first.ContainerId, second.ContainerId);
    }

    [TestMethod]
    public void TwoContainers_SameSeededSource_SendTheSameContainerId()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider());

        var first = new Amqp1Container(new UnitTestAmqp1SaslPolicy(), bus, null, null, new UnitTestSeededIdentifierRandom(5));
        var second = new Amqp1Container(new UnitTestAmqp1SaslPolicy(), bus, null, null, new UnitTestSeededIdentifierRandom(5));

        Assert.AreEqual(first.ContainerId, second.ContainerId);
    }

    [TestMethod]
    public void Container_OptionsGiveAContainerId_KeepsIt()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider());

        var container = new Amqp1Container(new UnitTestAmqp1SaslPolicy(), bus, new Amqp1ContainerOptions { ContainerId = "given" });

        Assert.AreEqual("given", container.ContainerId);
    }

    [TestMethod]
    public void Connection_OptionsWithoutAContainerId_Throw()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider());
        var context = new ExchangeContext(
            1,
            new ListenUrl("amqp1", "127.0.0.1", 5672).WithBoundPort(5672),
            new IPEndPoint(IPAddress.Loopback, 5672),
            new IPEndPoint(IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            new ManualTimeProvider(),
            CancellationToken.None);

        Assert.ThrowsExactly<ArgumentException>(() => new Amqp1ContainerConnection(new InMemoryConnection([]), context, Amqp1ContainerOptions.Default, bus, null));
    }
}
