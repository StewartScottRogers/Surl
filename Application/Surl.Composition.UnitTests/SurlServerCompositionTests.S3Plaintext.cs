using Surl.Extended.Protocol.S3;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    // --s3-plaintext (ADR-0108 decision 5, BL-1021): s3 composed plain, with no certificate and no TLS settings.
    [TestMethod]
    public async Task ComposeAsync_S3WithS3Plaintext_ServesItPlainWithNoTlsSettingsAndWarns()
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
            StartTestSeams.ParseWithS3("--s3-plaintext", "s3://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;
        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        var boundUrls = await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        Assert.IsNull(start.Failure);
        Assert.IsNotNull(serving.Servers.OfType<S3ProtocolServer>().SingleOrDefault());
        Assert.IsFalse(boundUrls.Single().IsImplicitTls);
        CollectionAssert.AreEqual(new ServerTlsSettings?[] { null }, tlsSettingsAsked);
        Assert.IsEmpty(serving.ThrowawayCertificateFingerprints);
        StringAssert.Contains(log.ToString(), "surl: warning: --s3-plaintext: s3 is served as plain HTTP, without TLS" + Environment.NewLine);
    }

    [TestMethod]
    public async Task ComposeAsync_S3WithSelfSignedAndNoS3Plaintext_ServesItOverTls()
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
            StartTestSeams.ParseWithS3("--self-signed", "s3://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;
        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, bound.SetResult), stop.Token);
        var boundUrls = await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        Assert.IsNull(start.Failure);
        Assert.IsTrue(boundUrls.Single().IsImplicitTls);
        Assert.IsNotNull(tlsSettingsAsked.Single());
        Assert.DoesNotContain("--s3-plaintext", log.ToString());
    }
}
