using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smb;

/// <summary>
/// What the SMB server tests share: a server over an in-memory content store holding the
/// share <c>/share/</c> (with <c>file.txt</c> in it), the file <c>/afile.txt</c> and the
/// dot-directory <c>/.hidden/</c>; the challenge <see cref="ChallengeHex"/>; requests laid out
/// as upstream curl 8.21.0 sends them (Fixtures/README.md); and their expected responses,
/// built field by field from [MS-CIFS], independent of the code under test.
/// </summary>
internal static class SmbTestExchange
{
    /// <summary>The challenge every test's server sends, the one the fixtures were recorded with.</summary>
    public const string ChallengeHex = "0123456789ABCDEF";

    /// <summary>The NT response curl computed for password <c>secret</c> and <see cref="ChallengeHex"/>.</summary>
    public const string SecretNtResponseHex = "2FECDD61DA941EC269D46DD130BA93129C4166FE03F21706";

    /// <summary>curl's <c>SMB_COM_NEGOTIATE</c>, offering only NT LM 0.12, from the login-tree-connect recording.</summary>
    public const string NegotiateHex =
        "0000002FFF534D427200000000184100BA000000000000000000000000001DD700000000000C00024E54204C4D20302E313200";

    /// <summary>curl's <c>SMB_COM_SESSION_SETUP_ANDX</c> for <c>-u alice:secret</c>, from the login-tree-connect recording.</summary>
    public const string SessionSetupHex =
        "00000095FF534D427300000000184100BA000000000000000000000000001DD7000000000DFF000000009001000100000000001800180000000000080000005800"
        + "101C21228F73993193C75440547D94B75F3231384D879388" + SecretNtResponseHex
        + "616C696365003132372E302E302E31007838365F36342D7736342D6D696E67773332006375726C00";

    /// <summary>The negotiate response with the default limits and the clock at its start: the response the fixtures were fed.</summary>
    public const string NegotiateResponseHex =
        "00000052" + "FF534D4272000000009841" + "00" + "BA00" + "0000000000000000" + "0000" + "0000" + "1DD7" + "0000" + "0000"
        + "11" + "0000" + "03" + "0100" + "0100" + "FFFF0100" + "00000100" + "00000000" + "18000000" + "00E01138D350DD01" + "0000" + "08"
        + "0D00" + ChallengeHex + "5355524C00";

    /// <summary>The session setup response accepting the login as a user: UID 1, action 0, empty OS and LAN manager, domain SURL.</summary>
    public const string SessionSetupResponseHex =
        "00000030" + "FF534D4273000000009841" + "00" + "BA00" + "0000000000000000" + "0000" + "0000" + "1DD7" + "0100" + "0000" + "03FF0000000000" + "0700" + "0000" + "5355524C00";

    /// <summary>Parses hexadecimal, ignoring spaces.</summary>
    public static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    /// <summary>A framed request: NetBIOS header, curl's header layout with <paramref name="treeId"/> and <paramref name="userId"/>, then <paramref name="bodyHex"/>.</summary>
    public static string RequestHex(byte command, ushort treeId, ushort userId, string bodyHex) =>
        Framed("FF534D42" + Byte(command) + "00000000" + "18" + "4100" + "BA00" + "0000000000000000" + "0000" + Word(treeId) + "1DD7" + Word(userId) + "0000" + bodyHex);

    /// <summary>A framed response: NetBIOS header, the reply's header with <paramref name="status"/>, <paramref name="treeId"/> and <paramref name="userId"/>, then <paramref name="bodyHex"/>.</summary>
    public static string ResponseHex(byte command, uint status, ushort treeId, ushort userId, string bodyHex = "000000") =>
        Framed("FF534D42" + Byte(command) + DoubleWord(status) + "98" + "4100" + "BA00" + "0000000000000000" + "0000" + Word(treeId) + "1DD7" + Word(userId) + "0000" + bodyHex);

    /// <summary>curl's tree connect to <c>\\127.0.0.1\</c><paramref name="share"/>, logged in as UID 1.</summary>
    public static string TreeConnectHex(string share, ushort userId = SmbSession.UserId)
    {
        var bytes = Terminated(@"\\127.0.0.1\" + share) + Terminated("?????");
        return RequestHex(SmbCommand.TreeConnectAndX, 0, userId, "04" + "FF00" + "0000" + "0000" + "0000" + Word((ushort)(bytes.Length / 2)) + bytes);
    }

    /// <summary>The response connecting a tree as <paramref name="treeId"/>: service <c>A:</c>, no file system named.</summary>
    public static string TreeConnectResponseHex(ushort treeId) =>
        ResponseHex(SmbCommand.TreeConnectAndX, 0, treeId, SmbSession.UserId, "03FF0000000000" + "0400" + "413A0000");

    /// <summary>curl's tree disconnect of <paramref name="treeId"/>.</summary>
    public static string TreeDisconnectHex(ushort treeId) => RequestHex(SmbCommand.TreeDisconnect, treeId, SmbSession.UserId, "000000");

    /// <summary>curl's NT create of <c>dir\file.txt</c> for reading on <paramref name="treeId"/>, from the login-tree-connect recording.</summary>
    public static string NtCreateHex(ushort treeId) => RequestHex(
        SmbCommand.NtCreateAndX,
        treeId,
        SmbSession.UserId,
        "18FF000000000C0000000000000000000000008000000000000000000000000007000000010000000000000000000000000D006469725C66696C652E74787400");

    /// <summary>curl's read of 32768 bytes from FID 0x4000 at offset 0.</summary>
    public static string ReadHex(ushort treeId) => RequestHex(
        SmbCommand.ReadAndX, treeId, SmbSession.UserId, "0CFF000000004000000000008000800000000000000000000000000000");

    /// <summary>A write of one byte, <c>A</c>, to FID 0x4000 at offset 0, laid out as curl lays out its writes.</summary>
    public static string WriteHex(ushort treeId) => RequestHex(
        SmbCommand.WriteAndX, treeId, SmbSession.UserId, "0EFF000000004000000000000000000000000000000100400000000000" + "0200" + "0041");

    /// <summary>curl's close of FID 0x4000.</summary>
    public static string CloseHex(ushort treeId) => RequestHex(SmbCommand.Close, treeId, SmbSession.UserId, "03004000000000" + "0000");

    /// <summary>An error response to <paramref name="command"/>, echoing <paramref name="treeId"/> and <paramref name="userId"/>.</summary>
    public static string ErrorHex(byte command, uint status, ushort treeId = 1, ushort userId = SmbSession.UserId) =>
        ResponseHex(command, status, treeId, userId);

    public static InMemoryContentFileSystem StandardFileSystem()
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider());
        var root = InMemoryContentFileSystem.RootPath;
        fileSystem.CreateDirectory(Path.Join(root, "share"));
        fileSystem.CreateDirectory(Path.Join(root, ".hidden"));
        WriteFile(fileSystem, Path.Join(root, "share", "file.txt"), "hello smb\n");
        WriteFile(fileSystem, Path.Join(root, "afile.txt"), "not a share\n");
        return fileSystem;
    }

    public static ContentStore StandardContentStore() =>
        new(InMemoryContentFileSystem.RootPath, StandardFileSystem(), new ContentExposureOptions());

    public static SmbProtocolServer Server(ISmbAuthenticationPolicy? authenticationPolicy = null) =>
        new(StandardContentStore(), authenticationPolicy ?? new SecretPasswordPolicy(), new FixedChallengeSource());

    public static ExchangeContext Context(
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null,
        CancellationToken shutdownToken = default) => new(
            1,
            new ListenUrl("smb", "127.0.0.1", 445).WithBoundPort(445),
            new IPEndPoint(IPAddress.Loopback, 445),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
            ShutdownToken = shutdownToken,
        };

    /// <summary>Serves the concatenated framed <paramref name="requestsHex"/> to their end and returns what the server wrote, in hexadecimal.</summary>
    public static async Task<string> ServeAsync(
        CancellationToken cancellationToken,
        IEnumerable<string> requestsHex,
        ISmbAuthenticationPolicy? authenticationPolicy = null,
        IExchangeLog? log = null,
        ExchangeLimits? limits = null)
    {
        var connection = new InMemoryConnection([Hex(string.Concat(requestsHex))]);
        await Server(authenticationPolicy).ServeAsync(connection, Context(new ManualTimeProvider(), cancellationToken, limits, log));
        return Convert.ToHexString(connection.WrittenBytes);
    }

    /// <summary>Serves the negotiate, the accepted session setup, a tree connect to <c>share</c> and then <paramref name="requestsHex"/>, and returns what the server wrote after the tree connect's response.</summary>
    public static async Task<string> ServeConnectedAsync(CancellationToken cancellationToken, IExchangeLog? log, params string[] requestsHex)
    {
        var written = await ServeAsync(cancellationToken, [NegotiateHex, SessionSetupHex, TreeConnectHex("share"), .. requestsHex], log: log);
        var prefix = NegotiateResponseHex + SessionSetupResponseHex + TreeConnectResponseHex(1);
        Assert.StartsWith(prefix, written);
        return written[prefix.Length..];
    }

    private static string Framed(string messageHex) =>
        "00" + (messageHex.Length / 2).ToString("X6", CultureInfo.InvariantCulture) + messageHex;

    private static string Byte(byte value) => value.ToString("X2", CultureInfo.InvariantCulture);

    private static string Word(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return Convert.ToHexString(bytes);
    }

    private static string DoubleWord(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return Convert.ToHexString(bytes);
    }

    private static string Terminated(string text) => Convert.ToHexString(Encoding.ASCII.GetBytes(text)) + "00";

    private static void WriteFile(InMemoryContentFileSystem fileSystem, string path, string text)
    {
        using var file = fileSystem.CreateFileForAsyncWrite(path);
        file.Write(Encoding.ASCII.GetBytes(text));
    }

    /// <summary>Sends <see cref="ChallengeHex"/> as every connection's challenge.</summary>
    public sealed class FixedChallengeSource : ISmbChallengeSource
    {
        public void Fill(Span<byte> challenge) => Hex(ChallengeHex).CopyTo(challenge);
    }

    /// <summary>
    /// Stands in for the account <c>alice</c> with password <c>secret</c>: accepts a login whose NT
    /// response is the one curl computed for that password and the fixed challenge, and records
    /// every login it is asked about.
    /// </summary>
    public sealed class SecretPasswordPolicy : ISmbAuthenticationPolicy
    {
        public List<SmbNtlmV1Login> Logins { get; } = [];

        public ValueTask<SmbLoginVerdict> CheckSmbNtlmV1LoginAsync(SmbNtlmV1Login login, CancellationToken cancellationToken)
        {
            Logins.Add(login);
            var isAccepted = login.UserName == "alice"
                && login.ServerChallenge.Span.SequenceEqual(Hex(ChallengeHex))
                && login.NtResponse.Span.SequenceEqual(Hex(SecretNtResponseHex));
            return ValueTask.FromResult(new SmbLoginVerdict(
                isAccepted ? SmbLoginOutcome.Accepted : SmbLoginOutcome.Refused,
                isAccepted ? "alice" : null,
                new CheckedLogin("ntlmv1", login.UserName, isAccepted)));
        }
    }

    /// <summary>Answers every login with one verdict.</summary>
    public sealed class FixedVerdictPolicy(SmbLoginVerdict verdict) : ISmbAuthenticationPolicy
    {
        public ValueTask<SmbLoginVerdict> CheckSmbNtlmV1LoginAsync(SmbNtlmV1Login login, CancellationToken cancellationToken) =>
            ValueTask.FromResult(verdict);
    }

    /// <summary>Never answers: waits until the login is cancelled, as a policy waiting out a refusal's second.</summary>
    public sealed class NeverAnsweringPolicy : ISmbAuthenticationPolicy
    {
        private readonly TaskCompletionSource asked = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Asked => asked.Task;

        public async ValueTask<SmbLoginVerdict> CheckSmbNtlmV1LoginAsync(SmbNtlmV1Login login, CancellationToken cancellationToken)
        {
            asked.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }
}
