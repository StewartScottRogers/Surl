using System.Text;
using Surl.Cli;

namespace Surl.Api;

// BL-668: the group, mail, share, bucket, topic and queue item properties and ConfigFiles are spellings of
// their options (ADR-0101 decisions 2 and 4); the command line they make is the one the same words make.
public sealed partial class SurlServerArgumentsTests
{
    [TestMethod]
    public void Compose_EveryItemProperty_IsItsOptionInSurlsOwnWordOrder()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Groups.Add("g=a");
        options.MailDomains.Add("d");
        options.Mailboxes.Add("m");
        options.MailboxFiles.Add("mf");
        options.MailAliases.Add("al");
        options.MailCatchAlls.Add("ca");
        options.SharedFolders.Add("sf");
        options.Shares.Add("s");
        options.ShareAccess.Add("sa");
        options.Buckets.Add("b");
        options.Topics.Add("t");
        options.Queues.Add("q");
        options.TopicMaps.Add("tm");
        options.TopicAccess.Add("ta");
        options.ConfigFiles.Add("c.conf");
        options.AdditionalArguments.Add("--allow-uploads");

        var actual = SurlServerArguments.Compose(options);

        CollectionAssert.AreEqual(
            new[]
            {
                HttpListenUrl, "--group", "g=a", "--mail-domain", "d", "--mailbox", "m", "--mailbox-file", "mf",
                "--mail-alias", "al", "--mail-catch-all", "ca", "--shared-folder", "sf", "--share", "s",
                "--share-access", "sa", "--bucket", "b", "--topic", "t", "--queue", "q", "--topic-map", "tm",
                "--topic-access", "ta", "--config", "c.conf", "--allow-uploads",
            },
            actual.ToArray());
    }

    [TestMethod]
    public void Compose_NoItemProperty_AddsNoWord()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);

        var actual = SurlServerArguments.Compose(options);

        CollectionAssert.AreEqual(new[] { HttpListenUrl }, actual.ToArray());
    }

    [TestMethod]
    public void Parse_ItemProperties_ComposeWhatTheOptionsCompose()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.MailDomains.Add("example.com");
        options.Mailboxes.Add("alice@example.com:pw");
        options.Topics.Add("sensors/#");
        options.Queues.Add("jobs;durable");
        var expected = CommandLineParser.Parse(
            [HttpListenUrl, "--mail-domain", "example.com", "--mailbox", "alice@example.com:pw", "--topic", "sensors/#", "--queue", "jobs;durable"])
            .CommandLine!;

        var actual = SurlServerArguments.Parse(options, []);

        CollectionAssert.AreEqual(expected.MailDomains.ToArray(), actual.MailDomains.ToArray());
        CollectionAssert.AreEqual(expected.Mailboxes.ToArray(), actual.Mailboxes.ToArray());
        CollectionAssert.AreEqual(expected.Topics.ToArray(), actual.Topics.ToArray());
        CollectionAssert.AreEqual(expected.Queues.ToArray(), actual.Queues.ToArray());
    }

    [TestMethod]
    public void Parse_AMalformedShare_IsRefusedAsSurlRefusesIt()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.Shares.Add("no-equals-sign");
        var expected = CommandLineParser.Parse([HttpListenUrl, "--share", "no-equals-sign"]).Failure!;

        var exception = Assert.ThrowsExactly<SurlServerStartException>(() => SurlServerArguments.Parse(options, []));

        Assert.AreEqual(expected.ExitCode, exception.ExitCode);
        Assert.AreEqual(expected.Message, exception.Lines[0]);
    }

    [TestMethod]
    public void Parse_ConfigFiles_AreReadThroughTheFileReaderAndOverriddenByAdditionalArguments()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.ConfigFiles.Add("a.conf");
        options.AdditionalArguments.Add("--mail-domain");
        options.AdditionalArguments.Add("later.example");
        byte[] Read(string path) => path == "a.conf" ? Encoding.UTF8.GetBytes("mail-domain = \"first.example\"\n") : throw new FileNotFoundException(path);

        var actual = SurlServerArguments.Parse(options, [], Read, CustomProtocolServers.Of(options));

        CollectionAssert.AreEqual(new[] { "first.example", "later.example" }, actual.MailDomains.ToArray());
    }

    [TestMethod]
    public void Parse_AMissingConfigFile_IsRefusedAsSurlRefusesIt()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.ConfigFiles.Add("missing.conf");
        var expected = CommandLineParser.Parse([HttpListenUrl, "--config", "missing.conf"]).Failure!;

        var exception = Assert.ThrowsExactly<SurlServerStartException>(
            () => SurlServerArguments.Parse(options, [], _ => throw new FileNotFoundException(), CustomProtocolServers.Of(options)));

        Assert.AreEqual(expected.ExitCode, exception.ExitCode);
    }

    [TestMethod]
    public void Parse_WithoutAFileReader_ReadsConfigFilesFromDisk()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.ConfigFiles.Add("no-such-directory-surl/missing.conf");
        var expected = CommandLineParser.Parse([HttpListenUrl, "--config", "no-such-directory-surl/missing.conf"]).Failure!;

        var exception = Assert.ThrowsExactly<SurlServerStartException>(
            () => SurlServerArguments.Parse(options, [], null, CustomProtocolServers.Of(options)));

        Assert.AreEqual(expected.ExitCode, exception.ExitCode);
    }

    [TestMethod]
    public void Parse_ATooLongConfigFile_IsRefusedAsSurlRefusesIt()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.ConfigFiles.Add("big.conf");
        var big = new byte[17_000_000];
        Array.Fill(big, (byte)'\n');

        Assert.ThrowsExactly<SurlServerStartException>(
            () => SurlServerArguments.Parse(options, [], _ => big, CustomProtocolServers.Of(options)));
    }

    [TestMethod]
    public void IdentityOf_TwoSpellingsOfOnePath_ShareAnIdentity()
    {
        var source = new ApiConfigFileSource(null);

        Assert.AreEqual(source.IdentityOf("a.conf"), source.IdentityOf("./a.conf"));
    }

    [TestMethod]
    public void Read_WithoutAFileReader_ReadsTheDisk()
    {
        var source = new ApiConfigFileSource(null);

        Assert.IsNull(source.Read(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "none.conf"), 100));
    }

    [TestMethod]
    public void ThrowIfMisused_ANullItemInAnyList_IsAnArgumentException()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.TopicAccess.Add(null!);

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }

    [TestMethod]
    public void ThrowIfMisused_StandardInputAsAConfigFile_IsAnArgumentException()
    {
        var options = ApiTestSeams.Options(HttpListenUrl);
        options.ConfigFiles.Add("-");

        Assert.ThrowsExactly<ArgumentException>(() => SurlServerArguments.ThrowIfMisused(options));
    }
}
