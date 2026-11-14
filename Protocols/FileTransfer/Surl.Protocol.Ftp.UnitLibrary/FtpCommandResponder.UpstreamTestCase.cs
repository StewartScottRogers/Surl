using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The commands an upstream test case's reply answers in place of the content store
/// (<see cref="FtpUpstreamTestCaseServer"/>; ADR-0080 decision 2): <c>CWD</c>, <c>SIZE</c>,
/// <c>MDTM</c>, <c>RETR</c>, <c>LIST</c>, <c>NLST</c>, <c>STOR</c>, <c>APPE</c> and <c>DELE</c>
/// (which deletes nothing and succeeds). Each still
/// uses the server's own replies, data connections and TLS; only the content is the case's.
/// </summary>
internal sealed partial class FtpCommandResponder
{
    private const int UploadReadBufferBytes = 16 * 1024;

    private const string WeirdRetrieveCompletion = "226 File transfer complete";

    // RETRWEIRDO weirds the first RETR only, as upstream clears the flag.
    private bool weirdRetrieveSpent;

    private void AnswerAsTheUpstreamTestCaseSays(FtpUpstreamTestCaseReplies replies)
    {
        dataConnections.ReportedPassiveHost = replies.ReportsBadPassiveAddress ? "1,2,3,4" : null;
        commands["CWD"] = AnswerUpstreamTestCaseChangeWorkingDirectoryAsync;
        commands["XCWD"] = AnswerUpstreamTestCaseChangeWorkingDirectoryAsync;
        commands["SIZE"] = argument => AnswerUpstreamTestCaseFileStatusAsync(argument, replies.SizeReply);
        commands["MDTM"] = argument => AnswerUpstreamTestCaseFileStatusAsync(argument, _ => replies.ModificationTimeReply());
        commands["RETR"] = argument => AnswerUpstreamTestCaseRetrieveAsync(argument, replies);
        commands["LIST"] = _ => AnswerUpstreamTestCaseListingAsync(replies);
        commands["NLST"] = _ => AnswerUpstreamTestCaseListingAsync(replies);
        commands["STOR"] = argument => AnswerUpstreamTestCaseUploadAsync(argument, replies);
        commands["APPE"] = argument => AnswerUpstreamTestCaseUploadAsync(argument, replies);
        commands["DELE"] = argument => ReplyAsync(argument is null ? SyntaxError : "250 File deleted");
    }

    private async ValueTask<bool> WriteScriptedReplyAsync(byte[] reply)
    {
        await WriteControlAsync(reply);

        return true;
    }

    // Every directory exists: upstream's test server enters any.
    private ValueTask<bool> AnswerUpstreamTestCaseChangeWorkingDirectoryAsync(byte[]? argument) =>
        ReplyAsync(argument is null ? SyntaxError : DirectoryChanged);

    private ValueTask<bool> AnswerUpstreamTestCaseFileStatusAsync(byte[]? argument, Func<string, string?> reply) =>
        ReplyAsync(argument is null ? SyntaxError : reply(Encoding.Latin1.GetString(argument)) ?? NoSuchFile);

    // <data> is what is sent, whatever REST asked for: a resuming case writes the file's
    // remainder as its <data> (test 110: "expected to be a file without the first part").
    // The REST offset is spent all the same, as it applies to one RETR.
    private async ValueTask<bool> AnswerUpstreamTestCaseRetrieveAsync(byte[]? argument, FtpUpstreamTestCaseReplies replies)
    {
        restartOffset = 0;
        if (argument is null)
        {
            return await ReplyAsync(SyntaxError);
        }

        if (replies.FindFile(Encoding.Latin1.GetString(argument)) is not { } file)
        {
            return await ReplyAsync(NoSuchFile);
        }

        if (replies.SendsRetrieveRepliesTogether && !weirdRetrieveSpent)
        {
            weirdRetrieveSpent = true;

            // One write carries the 150 and the 226; the data follows and no further 226 does (ADR-0156 decision 5).
            return await TransferAsync(
                replies.WeirdRetrieveOpeningReply(file.Length) + "\r\n" + WeirdRetrieveCompletion,
                dataConnection => SendBytesAsync(dataConnection, file, "file"),
                TransferComplete);
        }

        return await TransferOrAnswerWithoutDataConnectionAsync(
            replies,
            replies.RetrieveOpeningReply(FtpPath.ToReplyText(argument), file.Length),
            dataConnection => SendBytesAsync(dataConnection, file, "file"));
    }

    // NODATACONN425, NODATACONN421 and NODATACONN150: no data connection is ever made, so the data command
    // is answered on the control connection alone (ADR-0156 decisions 1 and 2). The 421 does not close it.
    private ValueTask<bool> TransferOrAnswerWithoutDataConnectionAsync(
        FtpUpstreamTestCaseReplies replies, string openingReply, Func<IConnection, Task<string>> send) =>
        replies.NoDataConnectionReply == FtpNoDataConnectionReply.None
            ? TransferAsync(openingReply, send)
            : AnswerWithoutDataConnectionAsync(replies.NoDataConnectionReply);

    private async ValueTask<bool> AnswerWithoutDataConnectionAsync(FtpNoDataConnectionReply kind)
    {
        await ReplyAsync("150 Opening data connection");

        return kind switch
        {
            FtpNoDataConnectionReply.Refuse425 => await ReplyAsync("425 Can't open data connection"),
            FtpNoDataConnectionReply.Timeout421 => await ReplyAsync("421 Connection timed out"),
            _ => true,
        };
    }

    private ValueTask<bool> AnswerUpstreamTestCaseListingAsync(FtpUpstreamTestCaseReplies replies) =>
        TransferOrAnswerWithoutDataConnectionAsync(
            replies, ListingOpeningReply, dataConnection => SendBytesAsync(dataConnection, replies.Listing, "listing"));

    private async ValueTask<bool> AnswerUpstreamTestCaseUploadAsync(byte[]? argument, FtpUpstreamTestCaseReplies replies)
    {
        restartOffset = 0;
        if (argument is null)
        {
            return await ReplyAsync(SyntaxError);
        }

        return await TransferOrAnswerWithoutDataConnectionAsync(
            replies,
            $"150 Opening data connection for {FtpPath.ToReplyText(argument)}",
            dataConnection => ReceiveUpstreamTestCaseUploadAsync(dataConnection, replies));
    }

    // Reads the data connection to its end; a client that breaks it is answered 426.
    private async Task<string> ReceiveUpstreamTestCaseUploadAsync(IConnection dataConnection, FtpUpstreamTestCaseReplies replies)
    {
        var buffer = new byte[UploadReadBufferBytes];
        try
        {
            int count;
            while ((count = await dataConnection.ReadAsync(buffer, context.CancellationToken)) > 0)
            {
                replies.RecordUpload(buffer.AsSpan(0, count));
            }
        }
        catch (IOException)
        {
            dataConnection.Abort();
            return NoteFailedTransfer(TransferAborted, "The data connection failed before the whole upload arrived; the data connection was reset.");
        }

        return replies.StoreReply ?? TransferComplete;
    }
}
