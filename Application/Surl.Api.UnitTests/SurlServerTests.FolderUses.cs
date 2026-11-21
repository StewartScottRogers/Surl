using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1512: folder uses seeded through Mailboxes and read back through ListMailboxesAsync (ADR-0055 amendment 1, decision 6).
public sealed partial class SurlServerTests
{
    private Task<SurlServer> StartWithFoldersAsync(string folders)
    {
        var options = ApiTestSeams.Options("smtp://127.0.0.1:0");
        options.MailDomains.Add("example.com");
        options.Mailboxes.Add($"alice@example.com:secret;folders={folders}");
        return SurlServer.StartAsync(options, new ApiTestSeams().Host, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Mail_FolderUses_AreListedAndComeBackAfterAReset()
    {
        await using var server = await StartWithFoldersAsync("Sent=sent,Work");
        var token = TestContext.CancellationToken;

        var before = await server.Mail.ListMailboxesAsync("alice@example.com", token);
        await server.ResetAsync(token);
        var after = await server.Mail.ListMailboxesAsync("alice@example.com", token);

        foreach (var boxes in new[] { before, after })
        {
            Assert.AreEqual(@"\Sent", boxes.Single(box => box.Name == "Sent").Use);
            Assert.IsNull(boxes.Single(box => box.Name == "Work").Use);
            Assert.IsNull(boxes.Single(box => box.Name == "INBOX").Use);
        }
    }

    [TestMethod]
    public async Task Mail_FolderUses_TwoInstancesDoNotSeeEachOthers()
    {
        await using var first = await StartWithFoldersAsync("Box=sent");
        await using var second = await StartWithFoldersAsync("Box=trash");
        var token = TestContext.CancellationToken;

        Assert.AreEqual(@"\Sent", (await first.Mail.ListMailboxesAsync("alice@example.com", token)).Single(box => box.Name == "Box").Use);
        Assert.AreEqual(@"\Trash", (await second.Mail.ListMailboxesAsync("alice@example.com", token)).Single(box => box.Name == "Box").Use);
    }

    [TestMethod]
    [DataRow("Sent=bogus")]
    [DataRow("Sent=sent,Archive=sent")]
    [DataRow("Sent=sent,Sent=trash")]
    public async Task StartAsync_AnUnknownOrDoubledFolderUse_IsRefusedAsTheCommandLineRefuses(string folders)
    {
        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(() => StartWithFoldersAsync(folders));

        Assert.AreEqual(SurlExitCode.FailedInit, exception.ExitCode);
    }
}
