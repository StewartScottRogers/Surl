using Surl.Extended.Protocol.AzureStorage;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    // --az-plaintext (ADR-0109 decision 6, BL-1025): az composed plain, with no certificate and no TLS settings.
    [TestMethod]
    public async Task ComposeAsync_AzWithAzPlaintext_ServesItPlainWithNoTlsSettingsAndWarns()
    {
        var factory = new FakeListenerFactory();
        var tlsSettingsAsked = new List<ServerTlsSettings?>();
        var seams = new StartTestSeams(factory).Seams with
        {
            AreExtensionsTurnedOn = true,
            CreateListenerFactory = settings =>
            {
                tlsSettingsAsked.Add(settings);
                return factory;
            },
        };
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithAzureStorage("--az-plaintext", "az://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;
        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        var boundUrls = await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        Assert.IsNull(start.Failure);
        Assert.IsNotNull(serving.Servers.OfType<AzureStorageProtocolServer>().SingleOrDefault());
        Assert.IsFalse(boundUrls.Single().IsImplicitTls);
        CollectionAssert.AreEqual(new ServerTlsSettings?[] { null }, tlsSettingsAsked);
        Assert.IsEmpty(serving.ThrowawayCertificateFingerprints);
        StringAssert.Contains(log.ToString(), "surl: warning: --az-plaintext: az is served as plain HTTP, without TLS" + Environment.NewLine);
    }

    [TestMethod]
    public async Task ComposeAsync_AzWithSelfSignedAndNoAzPlaintext_ServesItOverTls()
    {
        var factory = new FakeListenerFactory();
        var tlsSettingsAsked = new List<ServerTlsSettings?>();
        var seams = new StartTestSeams(factory).Seams with
        {
            AreExtensionsTurnedOn = true,
            CreateListenerFactory = settings =>
            {
                tlsSettingsAsked.Add(settings);
                return factory;
            },
        };
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.ParseWithAzureStorage("--self-signed", "az://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;
        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        var boundUrls = await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        Assert.IsNull(start.Failure);
        Assert.IsTrue(boundUrls.Single().IsImplicitTls);
        Assert.IsNotNull(tlsSettingsAsked.Single());
        Assert.DoesNotContain("--az-plaintext", log.ToString());
    }
}
