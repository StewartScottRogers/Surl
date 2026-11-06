using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for SCP and SFTP (ADR-0089, BL-582): every conversation
/// <see cref="UpstreamCurlTransfersFilesWithSurlOverScpTests"/>, <see cref="UpstreamCurlTransfersFilesWithSurlOverSftpTests"/>,
/// <see cref="UpstreamCurlLogsInToSurlOverSshTests"/>, <see cref="UpstreamCurlOffersSshAlgorithmsTests"/> and
/// <see cref="UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests"/> run, through pinned upstream curl and the
/// pinned Curl port, each against its own fresh <c>surl</c> and with its own empty <c>HOME</c> and
/// <c>USERPROFILE</c> (ADR-0051 decision 8), judged by <see cref="PinnedCurlPortBesideUpstreamCurl"/>. One
/// test per mirrored test, each naming it in its <see cref="DescriptionAttribute"/>; a method's name is the
/// mirrored one, prefixed <c>Scp_</c> or <c>Sftp_</c> for the two transfer classes, whose names overlap.
/// The SSH transport is encrypted, so no request bytes are compared (ADR-0089 decision 4): the exit code,
/// standard output, the asserted standard error lines, stored files and <c>surl</c>'s <c>*</c> notes are,
/// with each run's throwaway host-key fingerprint replaced; the <c>KEXINIT</c> cases compare the client's
/// identification line and <c>KEXINIT</c> name-lists as <see cref="ClientKexInitRecorder"/> records them.
/// A test fails on a Surl defect, on a port defect <c>Documentation/Conformance/CurlPort/Ssh.md</c> does not
/// list, and on a listed case that agrees (ADR-0089 decision 5). Inconclusive when either pinned binary is
/// absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverSshTests
{
    private const string FindingsFile = "Ssh.md";

    private const string Scp = nameof(UpstreamCurlTransfersFilesWithSurlOverScpTests);
    private const string Sftp = nameof(UpstreamCurlTransfersFilesWithSurlOverSftpTests);
    private const string LogsIn = nameof(UpstreamCurlLogsInToSurlOverSshTests);
    private const string Offers = nameof(UpstreamCurlOffersSshAlgorithmsTests);
    private const string Weak = nameof(UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests);

    /// <summary>The per-run value of <c>--hostpubsha256</c> for surl's <c>ssh-rsa</c> key, in a case's arguments.</summary>
    private const string RsaSha256 = "<surl's ssh-rsa SHA-256>";

    /// <summary>The per-run value of <c>--hostpubmd5</c> for surl's <c>ssh-rsa</c> key, in a case's arguments.</summary>
    private const string RsaMd5 = "<surl's ssh-rsa MD5>";

    private const string MtimeDate = "\"Sun, 27 Sep 2026 12:34:56 GMT\"";
    private const string NoSuchFile = "curl: (78) Could not open remote file for reading: No such file or directory";

    // stunnel/static-curl's Windows build of upstream curl's tag 8.21.0 on OpenSSL 4.0.1 (ADR-0063).
    private const string OpenSslWindowsBuildSha256 = "589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648";

    private const string LegacyProviderConfiguration =
        "openssl_conf = openssl_init\n"
        + "[openssl_init]\nproviders = provider_sect\n"
        + "[provider_sect]\ndefault = default_sect\nlegacy = legacy_sect\n"
        + "[default_sect]\nactivate = 1\n"
        + "[legacy_sect]\nactivate = 1\n";

    private const string WinCngKex =
        "diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,"
        + "diffie-hellman-group14-sha256,diffie-hellman-group14-sha1,diffie-hellman-group1-sha1,"
        + "diffie-hellman-group-exchange-sha1,ext-info-c,kex-strict-c-v00@openssh.com";

    private const string WinCngHostKey =
        "rsa-sha2-512,rsa-sha2-256,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,"
        + "ssh-rsa,ssh-rsa-cert-v01@openssh.com";

    private const string WinCngCipher =
        "chacha20-poly1305@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,"
        + "aes192-cbc,aes128-cbc,arcfour128,arcfour,3des-cbc";

    private const string WinCngMac =
        "hmac-sha2-256,hmac-sha2-256-etm@openssh.com,hmac-sha2-512,hmac-sha2-512-etm@openssh.com,hmac-sha1,"
        + "hmac-sha1-etm@openssh.com,hmac-sha1-96,hmac-md5,hmac-md5-96";

    private const string OpenSslKex =
        "curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,ecdh-sha2-nistp384,ecdh-sha2-nistp521,"
        + WinCngKex;

    private const string OpenSslHostKey =
        "ecdsa-sha2-nistp256,ecdsa-sha2-nistp384,ecdsa-sha2-nistp521,ecdsa-sha2-nistp256-cert-v01@openssh.com,"
        + "ecdsa-sha2-nistp384-cert-v01@openssh.com,ecdsa-sha2-nistp521-cert-v01@openssh.com,ssh-ed25519,"
        + "ssh-ed25519-cert-v01@openssh.com," + WinCngHostKey;

    private const string OpenSslCipher =
        "chacha20-poly1305@openssh.com,aes256-gcm@openssh.com,aes128-gcm@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,"
        + "aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,blowfish-cbc,arcfour128,arcfour,cast128-cbc,3des-cbc";

    private const string OpenSslMac = WinCngMac + ",hmac-ripemd160,hmac-ripemd160@openssh.com";

    private const string Libssh2Identification = "SSH-2.0-libssh2_1.11.1";

    // A process killed by SIGSEGV, as .NET reports it on Unix: 128 + 11.
    private const int KilledBySegmentationFault = 139;

    private static readonly byte[] HelloWorld = "hello world\n"u8.ToArray();
    private static readonly byte[] HelloAgain = "hello again"u8.ToArray();
    private static readonly string Account = $"{AccountsFile.User}:{AccountsFile.Password}";

    /// <summary>One modification time for every served file, so <c>-R</c> and listings agree across runs.</summary>
    private static readonly DateTime ServedModificationTime = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static readonly Dictionary<string, byte[]> TransferFiles = new()
    {
        ["a.txt"] = HelloWorld,
        ["dir/b.txt"] = "bee\n"u8.ToArray(),
        [".hidden.txt"] = "hidden\n"u8.ToArray(),
    };

    private static readonly Dictionary<string, byte[]> LoginFiles = new() { ["a.txt"] = HelloWorld };

    private IsolatedCurlHome localFiles = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateLocalFiles() => localFiles = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteLocalFiles() => localFiles.Dispose();

    // --- UpstreamCurlTransfersFilesWithSurlOverScpTests ---

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverScpTests.Download_ServedFile_WritesItsBytes")]
    [DataRow("a.txt")]
    [DataRow("~/a.txt")]
    public Task Scp_Download_ServedFile_WritesItsBytes(string path) =>
        JudgeAsync(Transfer(Named(path), Scp, [$"{{surl}}/{path}"], [], ExitsZeroWriting(HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverScpTests.Download_AbsentHiddenOrDirectory_Exits78FailedToReceive")]
    [DataRow("missing.txt")]
    [DataRow(".hidden.txt")]
    [DataRow("dir")]
    public Task Scp_Download_AbsentHiddenOrDirectory_Exits78FailedToReceive(string path) =>
        JudgeAsync(Transfer(Named(path), Scp, [$"{{surl}}/{path}"], [], ExitsWithError(78, "curl: (78) Failed to recv file")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_UploadsOff_Exits25AndWritesNothing")]
    public async Task Scp_Upload_UploadsOff_Exits25AndWritesNothing() =>
        await JudgeAsync(Transfer(Named(), Scp, ["-T", await WriteUploadAsync(), "{surl}/new.txt"], [], run =>
            ExitsWithError(25, "curl: (25) failed to send file")(run) ?? Absent(run, "new.txt")) with
        {
            ComparedStoredFiles = ["new.txt"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_UploadsOn_WritesTheFile")]
    public async Task Scp_Upload_UploadsOn_WritesTheFile() =>
        await JudgeAsync(Transfer(Named(), Scp, ["-T", await WriteUploadAsync(), "{surl}/new.txt"], ["--allow-uploads"], run =>
            ExitsZero(run) ?? Stored(run, "new.txt", HelloAgain)) with
        {
            ComparedStoredFiles = ["new.txt"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_MissingDirectory_Exits25")]
    public async Task Scp_Upload_MissingDirectory_Exits25() =>
        await JudgeAsync(Transfer(Named(), Scp, ["-T", await WriteUploadAsync(), "{surl}/nodir/new.txt"], ["--allow-uploads"], ExitsWith(25)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverScpTests.Upload_PastMaxFilesize_Exits25AndWritesNothing")]
    public async Task Scp_Upload_PastMaxFilesize_Exits25AndWritesNothing() =>
        await JudgeAsync(Transfer(Named(), Scp, ["-T", await WriteUploadAsync(), "{surl}/new.txt"], ["--allow-uploads", "--max-filesize", "4"], run =>
            ExitsWith(25)(run) ?? Absent(run, "new.txt")) with
        {
            ComparedStoredFiles = ["new.txt"],
        });

    // --- UpstreamCurlTransfersFilesWithSurlOverSftpTests ---

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_ServedFile_WritesItsBytes")]
    [DataRow("a.txt")]
    [DataRow("~/a.txt")]
    public Task Sftp_Download_ServedFile_WritesItsBytes(string path) =>
        JudgeAsync(Transfer(Named(path), Sftp, [$"{{surl}}/{path}"], [], ExitsZeroWriting(HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_AbsentHiddenOrStateFile_Exits78NoSuchFile")]
    [DataRow("missing.txt")]
    [DataRow(".hidden.txt")]
    [DataRow(".surl/lock")]
    public Task Sftp_Download_AbsentHiddenOrStateFile_Exits78NoSuchFile(string path) =>
        JudgeAsync(Transfer(Named(path), Sftp, [$"{{surl}}/{path}"], [], ExitsWithError(78, NoSuchFile)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_Directory_Exits79")]
    public Task Sftp_Download_Directory_Exits79() =>
        JudgeAsync(Transfer(Named(), Sftp, ["{surl}/dir"], [], ExitsWith(79)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_Range_WritesThoseBytes")]
    public Task Sftp_Download_Range_WritesThoseBytes() =>
        JudgeAsync(Transfer(Named(), Sftp, ["-r", "0-4", "{surl}/a.txt"], [], ExitsZeroWriting("hello"u8.ToArray())));

    // Each run resumes its own copy of part.txt; the file curl completed is compared as a stored file.
    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_ResumedFromPartialFile_CompletesIt")]
    public async Task Sftp_Download_ResumedFromPartialFile_CompletesIt()
    {
        var output = new PerRunOutputFile(localFiles, "part.txt", "hello"u8.ToArray());
        await JudgeAsync(
            Transfer(Named(), Sftp, ["-C", "-", "-o", "<part.txt>", "{surl}/a.txt"], [], run => ExitsZero(run) ?? Stored(run, "part.txt", HelloWorld)) with
            {
                ComparedStoredFiles = ["part.txt"],
            },
            output);
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Download_RemoteTime_KeepsTheServedFilesModificationTime")]
    public async Task Sftp_Download_RemoteTime_KeepsTheServedFilesModificationTime()
    {
        var output = new PerRunOutputFile(localFiles, "out.txt", null);
        await JudgeAsync(
            Transfer(Named(), Sftp, ["-R", "-o", "<out.txt>", "{surl}/a.txt"], [], run =>
                ExitsZero(run) ?? ModifiedAt(run, "out.txt", ServedModificationTime)) with
            {
                ComparedStoredFiles = ["out.txt", ModificationTimeOf("out.txt")],
            },
            output);
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Listing_ListingsOff_Exits78")]
    public Task Sftp_Listing_ListingsOff_Exits78() =>
        JudgeAsync(Transfer(Named(), Sftp, ["{surl}/dir/"], [], ExitsWithError(78, "curl: (78) Could not open directory for reading: No such file or directory")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Listing_ListingsOn_WritesOneLongNameLinePerEntry")]
    public Task Sftp_Listing_ListingsOn_WritesOneLongNameLinePerEntry() =>
        JudgeAsync(Transfer(Named(), Sftp, ["{surl}/dir/"], ["--list-directories"], run =>
        {
            var listing = Encoding.UTF8.GetString(run.StandardOutput);
            return run.ExitCode == 0 && listing.EndsWith(" b.txt\n", StringComparison.Ordinal) && listing.Count(character => character == '\n') == 1
                ? null
                : Describe(run);
        }));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Listing_NamesOnly_ListsVisibleEntries")]
    public Task Sftp_Listing_NamesOnly_ListsVisibleEntries() =>
        JudgeAsync(Transfer(Named(), Sftp, ["-l", "{surl}/"], ["--list-directories"], ExitsZeroWriting("a.txt\ndir\n"u8.ToArray())));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_UploadsOff_Exits9AndWritesNothing")]
    public async Task Sftp_Upload_UploadsOff_Exits9AndWritesNothing() =>
        await JudgeAsync(Transfer(Named(), Sftp, ["-T", await WriteUploadAsync(), "{surl}/new.txt"], [], run =>
            ExitsWith(9)(run) ?? Absent(run, "new.txt")) with
        {
            ComparedStoredFiles = ["new.txt"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_UploadsOn_WritesTheFile")]
    [DataRow("")]
    [DataRow("--create-file-mode 0600")]
    public async Task Sftp_Upload_UploadsOn_WritesTheFile(string modeOptions) =>
        await JudgeAsync(Transfer(Named(modeOptions), Sftp, [.. Split(modeOptions), "-T", await WriteUploadAsync(), "{surl}/new.txt"], ["--allow-uploads"], run =>
            ExitsZero(run) ?? Stored(run, "new.txt", HelloAgain)) with
        {
            ComparedStoredFiles = ["new.txt"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_MissingDirectory_Exits78")]
    public async Task Sftp_Upload_MissingDirectory_Exits78() =>
        await JudgeAsync(Transfer(Named(), Sftp, ["-T", await WriteUploadAsync(), "{surl}/nodir/new.txt"], ["--allow-uploads"], ExitsWith(78)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_CreateDirectories_CreatesThemAndWritesTheFile")]
    public async Task Sftp_Upload_CreateDirectories_CreatesThemAndWritesTheFile() =>
        await JudgeAsync(Transfer(Named(), Sftp, ["--ftp-create-dirs", "-T", await WriteUploadAsync(), "{surl}/new/deep/b.txt"], ["--allow-uploads"], run =>
            ExitsZero(run) ?? Stored(run, "new/deep/b.txt", HelloAgain)) with
        {
            ComparedStoredFiles = ["new/deep/b.txt"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_Append_AppendsToTheFile")]
    public async Task Sftp_Upload_Append_AppendsToTheFile() =>
        await JudgeAsync(Transfer(Named(), Sftp, ["-a", "-T", await WriteUploadAsync(), "{surl}/a.txt"], ["--allow-uploads"], run =>
            ExitsZero(run) ?? Stored(run, "a.txt", [.. HelloWorld, .. HelloAgain])) with
        {
            ComparedStoredFiles = ["a.txt"],
        });

    // The mirrored test writes c.txt into the served folder once surl has started; here each run's
    // surl serves it from the start, which is the same folder when curl connects.
    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_ResumedOverPartialFile_CompletesIt")]
    public async Task Sftp_Upload_ResumedOverPartialFile_CompletesIt() =>
        await JudgeAsync(Transfer(Named(), Sftp, ["-C", "-", "-T", await WriteUploadAsync(), "{surl}/c.txt"], ["--allow-uploads"], run =>
            ExitsZero(run) ?? Stored(run, "c.txt", HelloAgain)) with
        {
            Files = new Dictionary<string, byte[]>(TransferFiles) { ["c.txt"] = "hello"u8.ToArray() },
            ComparedStoredFiles = ["c.txt"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_ResumedWithNoFile_WritesTheWholeFile")]
    public async Task Sftp_Upload_ResumedWithNoFile_WritesTheWholeFile() =>
        await JudgeAsync(Transfer(Named(), Sftp, ["-C", "-", "-T", await WriteUploadAsync(), "{surl}/c.txt"], ["--allow-uploads"], run =>
            ExitsZero(run) ?? Stored(run, "c.txt", HelloAgain)) with
        {
            ComparedStoredFiles = ["c.txt"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Upload_PastMaxFilesize_Exits79AndWritesNothing")]
    public async Task Sftp_Upload_PastMaxFilesize_Exits79AndWritesNothing() =>
        await JudgeAsync(Transfer(Named(), Sftp, ["-T", await WriteUploadAsync(), "{surl}/new.txt"], ["--allow-uploads", "--max-filesize", "4"], run =>
            ExitsWith(79)(run) ?? Absent(run, "new.txt")) with
        {
            ComparedStoredFiles = ["new.txt"],
        });

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Rename_RenamesBeforeTheDownload")]
    [DataRow("rename /a.txt /c.txt")]
    [DataRow("rename /~/a.txt /~/c.txt")]
    public Task Sftp_Quote_Rename_RenamesBeforeTheDownload(string command) =>
        JudgeAsync(Transfer(Named(command), Sftp, ["-Q", command, "{surl}/c.txt"], ["--allow-uploads"], ExitsZeroWriting(HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_RenameOntoExistingFile_Exits21")]
    public Task Sftp_Quote_RenameOntoExistingFile_Exits21() =>
        JudgeAsync(Transfer(Named(), Sftp, ["-Q", "rename /a.txt /dir/b.txt", "{surl}/a.txt"], ["--allow-uploads"], ExitsWith(21)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Remove_RemovesBeforeTheListing")]
    public Task Sftp_Quote_Remove_RemovesBeforeTheListing() =>
        JudgeAsync(Transfer(Named(), Sftp, ["-Q", "rm /a.txt", "-l", "{surl}/"], ["--allow-uploads", "--list-directories"], ExitsZeroWriting("dir\n"u8.ToArray())));

    // The mirrored test runs curl twice against one surl: mkdir, then rmdir. Each is one conversation
    // here, judged on its own; the second run's surl has x made before curl connects, as the first
    // run left it.
    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_MakeThenRemoveDirectory_ListsItEmptyThenFindsItGone")]
    [DataRow("mkdir")]
    [DataRow("rmdir")]
    public Task Sftp_Quote_MakeThenRemoveDirectory_ListsItEmptyThenFindsItGone(string step)
    {
        var measurementCase = step == "mkdir"
            ? Transfer(Named(step), Sftp, ["-Q", "mkdir /x", "{surl}/x/"], ["--allow-uploads", "--list-directories"], ExitsZeroWriting([]))
            : Transfer(Named(step), Sftp, ["-Q", "rmdir /x", "{surl}/x/"], ["--allow-uploads", "--list-directories"], ExitsWith(78));
        return JudgeAsync(measurementCase, arrange: step == "mkdir" ? null : surl => Directory.CreateDirectory(Path.Combine(surl.ServedDirectory!, "x")));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_Unsupported_Exits21")]
    [DataRow("chmod 0600 /a.txt")]
    [DataRow("chown 1000 /a.txt")]
    [DataRow("chgrp 1000 /a.txt")]
    [DataRow("ln /a.txt /l.txt")]
    [DataRow("symlink /a.txt /l.txt")]
    [DataRow("statvfs /")]
    public Task Sftp_Quote_Unsupported_Exits21(string command) =>
        JudgeAsync(Transfer(Named(command), Sftp, ["-Q", command, "{surl}/a.txt"], ["--allow-uploads"], ExitsWith(21)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_AcceptedOrFailureAllowed_DownloadsTheFile")]
    [DataRow("*chmod 0600 /a.txt")]
    [DataRow("*statvfs /")]
    [DataRow("pwd")]
    [DataRow("atime " + MtimeDate + " /a.txt")]
    public Task Sftp_Quote_AcceptedOrFailureAllowed_DownloadsTheFile(string command) =>
        JudgeAsync(Transfer(Named(command), Sftp, ["-Q", command, "{surl}/a.txt"], ["--allow-uploads"], ExitsZeroWriting(HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_ModificationTime_SetsTheFilesTime")]
    public async Task Sftp_Quote_ModificationTime_SetsTheFilesTime()
    {
        var output = new PerRunOutputFile(localFiles, "out.txt", null);
        await JudgeAsync(
            Transfer(Named(), Sftp, ["-Q", "mtime " + MtimeDate + " /a.txt", "-R", "-o", "<out.txt>", "{surl}/a.txt"], ["--allow-uploads"], run =>
                ExitsZero(run) ?? ModifiedAt(run, "out.txt", new DateTime(2026, 9, 27, 12, 34, 56, DateTimeKind.Utc))) with
            {
                ComparedStoredFiles = ["out.txt", ModificationTimeOf("out.txt")],
            },
            output);
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTransfersFilesWithSurlOverSftpTests.Quote_ChangeWithUploadsOff_Exits21")]
    [DataRow("mtime " + MtimeDate + " /a.txt")]
    [DataRow("rm /a.txt")]
    public Task Sftp_Quote_ChangeWithUploadsOff_Exits21(string command) =>
        JudgeAsync(Transfer(Named(command), Sftp, ["-Q", command, "{surl}/a.txt"], [], ExitsWith(21)));

    // --- UpstreamCurlLogsInToSurlOverSshTests ---

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.Login_Password_DownloadsTheFile")]
    [DataRow("sftp")]
    [DataRow("scp")]
    public Task Login_Password_DownloadsTheFile(string scheme) =>
        JudgeAsync(LogIn(Named(scheme), scheme, ["--hostpubsha256", RsaSha256, "-u", Account, "{surl}/a.txt"], ["--throwaway-hostkey", "--user", Account], run =>
            ExitsZeroWriting(HelloWorld)(run) ?? NotesContain(run, $"SSH login request: password for {AccountsFile.User}")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.Login_PublicKey_DownloadsTheFile")]
    [DataRow("sftp")]
    [DataRow("scp")]
    public async Task Login_PublicKey_DownloadsTheFile(string scheme)
    {
        var (privateKey, publicKey) = SshTestKeys.RsaUserKeyPair();
        var keyFile = await WriteTextAsync("id_test", privateKey);
        var publicKeyFile = await WriteTextAsync("id_test.pub", publicKey + "\n");
        var authorizedKeys = await WriteTextAsync("authorized_keys", publicKey + "\n");

        await JudgeAsync(LogIn(
            Named(scheme),
            scheme,
            ["--hostpubsha256", RsaSha256, "-u", $"{AccountsFile.User}:", "--key", keyFile, "--pubkey", publicKeyFile, "{surl}/a.txt"],
            ["--throwaway-hostkey", "--authorized-keys", $"{AccountsFile.User}:{authorizedKeys}"],
            run => ExitsZeroWriting(HelloWorld)(run) ?? NotesContain(run, $"SSH login request: publickey for {AccountsFile.User}, key ssh-rsa")));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.Login_WrongPassword_Exits67LoginDenied")]
    [DataRow("sftp")]
    [DataRow("scp")]
    public Task Login_WrongPassword_Exits67LoginDenied(string scheme) =>
        JudgeAsync(LogIn(
            Named(scheme),
            scheme,
            ["--hostpubsha256", RsaSha256, "-u", $"{AccountsFile.User}:wrong", "{surl}/a.txt"],
            ["--throwaway-hostkey", "--user", Account],
            run => ExitsWithError(67, "curl: (67) Login denied")(run) ?? (run.StandardOutput.Length == 0 ? null : Describe(run))));

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.HostKey_PinnedByMd5_DownloadsTheFile")]
    public Task HostKey_PinnedByMd5_DownloadsTheFile() =>
        JudgeAsync(LogIn(Named(), "sftp", ["--hostpubmd5", RsaMd5, "-u", Account, "{surl}/a.txt"], ["--throwaway-hostkey", "--user", Account], ExitsZeroWriting(HelloWorld)));

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.HostKey_CheckSkipped_DownloadsTheFile")]
    public Task HostKey_CheckSkipped_DownloadsTheFile() =>
        JudgeAsync(LogIn(Named(), "sftp", ["-k", "-u", Account, "{surl}/a.txt"], ["--throwaway-hostkey", "--user", Account], ExitsZeroWriting(HelloWorld)));

    // The refusal names each run's own throwaway fingerprint, so the mirrored assertion is checked
    // against upstream's run with the fingerprint replaced by <sha256>. The comparison's own stderr
    // normalisation does not replace a fingerprint, so stderr is not compared here; the exit code is.
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.HostKey_OtherSha256Pinned_Exits60")]
    public Task HostKey_OtherSha256Pinned_Exits60()
    {
        const string otherSha256 = "47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=";
        var measurementCase = LogIn(Named(), "sftp", ["--hostpubsha256", otherSha256, "-u", Account, "{surl}/a.txt"], ["--throwaway-hostkey", "--user", Account], run =>
        {
            var expected = $"curl: (60) Denied establishing ssh session: mismatch SHA256 fingerprint. Remote {SshSha256Placeholder} is not equal to {otherSha256}";
            return run.ExitCode == 60 && WithoutFingerprints(run.StandardError).StartsWith(expected, StringComparison.Ordinal) ? null : Describe(run);
        });
        return JudgeAsync(measurementCase with { AssertedStandardErrorFragments = [] });
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.HostKey_NoPinAndNoKnownHostsFile_Exits2")]
    public Task HostKey_NoPinAndNoKnownHostsFile_Exits2()
    {
        if (AccountsOwnKnownHostsFileExists())
        {
            Assert.Inconclusive("The account's own home directory holds .ssh/known_hosts, which curl falls back to.");
        }

        return JudgeAsync(LogIn(Named(), "sftp", ["-u", Account, "{surl}/a.txt"], ["--throwaway-hostkey", "--user", Account], ExitsWith(2)));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.HostKey_NoPinAndOnlyTheAccountsOwnKnownHostsFile_Exits60")]
    public Task HostKey_NoPinAndOnlyTheAccountsOwnKnownHostsFile_Exits60()
    {
        if (!AccountsOwnKnownHostsFileExists())
        {
            Assert.Inconclusive("The account's own home directory holds no .ssh/known_hosts for curl to fall back to.");
        }

        return JudgeAsync(LogIn(Named(), "sftp", ["-u", Account, "{surl}/a.txt"], ["--throwaway-hostkey", "--user", Account], ExitsWith(60)));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.HostKey_NotInKnownHosts_Exits60")]
    public async Task HostKey_NotInKnownHosts_Exits60()
    {
        var knownHosts = await WriteTextAsync("known_hosts", string.Empty);
        await JudgeAsync(LogIn(Named(), "sftp", ["--knownhosts", knownHosts, "-u", Account, "{surl}/a.txt"], ["--throwaway-hostkey", "--user", Account], ExitsWith(60)));
    }

    // Each run writes a known_hosts naming its own surl's port with another RSA key.
    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.HostKey_DifferentKeyInKnownHosts_Exits60")]
    public Task HostKey_DifferentKeyInKnownHosts_Exits60()
    {
        var knownHosts = localFiles.PathOf("known_hosts");
        return JudgeAsync(
            LogIn(Named(), "sftp", ["--knownhosts", knownHosts, "-u", Account, "{surl}/a.txt"], ["--throwaway-hostkey", "--user", Account], ExitsWith(60)),
            arrange: surl => File.WriteAllText(knownHosts, $"[127.0.0.1]:{surl.BaseUrl.Port} {SshTestKeys.OtherRsaPublicKeyLine()}\n"));
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.Login_CompressedSsh_NegotiatesZlibAndDownloadsTheFile")]
    [DataRow("sftp")]
    [DataRow("scp")]
    public Task Login_CompressedSsh_NegotiatesZlibAndDownloadsTheFile(string scheme) =>
        JudgeAsync(LogIn(
            Named(scheme),
            scheme,
            ["--compressed-ssh", "--hostpubsha256", RsaSha256, "-u", Account, "{surl}/a.txt"],
            ["--throwaway-hostkey", "--user", Account],
            run => ExitsZeroWriting(HelloWorld)(run) ?? NegotiatedContains(run, "compression zlib")));

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.HostKey_OnlyRsa_NegotiatesRsaAndDownloadsTheFile")]
    public async Task HostKey_OnlyRsa_NegotiatesRsaAndDownloadsTheFile()
    {
        var hostKey = await WriteTextAsync("host_key.pem", SshTestKeys.RsaHostKeyPem());
        await JudgeAsync(LogIn(Named(), "sftp", ["--hostpubsha256", RsaSha256, "-u", Account, "{surl}/a.txt"], ["--hostkey", hostKey, "--user", Account], run =>
            ExitsZeroWriting(HelloWorld)(run) ?? NegotiatedContains(run, "host key rsa-sha2-")));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.HostKey_OnlyEllipticCurveOnOpenSslBuilds_NegotiatesItAndDownloadsTheFile")]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ssh-ed25519")]
    public async Task HostKey_OnlyEllipticCurveOnOpenSslBuilds_NegotiatesItAndDownloadsTheFile(string keyType)
    {
        var hostKey = await WriteTextAsync("host_key.pem", EllipticCurveHostKeyPem(keyType));
        await JudgeAsync(LogIn(Named(keyType), "sftp", ["--hostpubsha256", HostKeySha256Of(keyType), "-u", Account, "{surl}/a.txt"], ["--hostkey", hostKey, "--user", Account], run =>
            ExitsZeroWriting(HelloWorld)(run) ?? NegotiatedContains(run, $"host key {keyType},")));
    }

    // surl writes its "no common host key algorithm" note as the key exchange fails, which can be a
    // moment after curl exits: JudgeAsync reads the notes once surl has ended the exchange.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverSshTests.HostKey_OnlyEllipticCurveOnTheWindowsBuild_Exits2NoCommonHostKeyAlgorithm")]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ssh-ed25519")]
    public async Task HostKey_OnlyEllipticCurveOnTheWindowsBuild_Exits2NoCommonHostKeyAlgorithm(string keyType)
    {
        const string noCommonHostKey = "SSH no common host key algorithm; client offered ";
        var hostKey = await WriteTextAsync("host_key.pem", EllipticCurveHostKeyPem(keyType));
        await JudgeAsync(
            LogIn(Named(keyType), "sftp", ["--hostpubsha256", HostKeySha256Of(keyType), "-u", Account, "{surl}/a.txt"], ["--hostkey", hostKey, "--user", Account], run =>
                ExitsWithError(2, "curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys")(run)
                ?? NotesContain(run, noCommonHostKey + "rsa-sha2-512,")));
    }

    // --- UpstreamCurlOffersSshAlgorithmsTests ---

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlOffersSshAlgorithmsTests.KexInit_WindowsBuild_ListsTheWinCngAlgorithms")]
    public Task KexInit_WindowsBuild_ListsTheWinCngAlgorithms() =>
        JudgeKexInitAsync(Named(), [], null, Libssh2Identification, [WinCngKex, WinCngHostKey, WinCngCipher, WinCngCipher, WinCngMac, WinCngMac, "none", "none", string.Empty, string.Empty]);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlOffersSshAlgorithmsTests.KexInit_LinuxAndMacOSBuilds_ListTheOpenSslAlgorithms")]
    public Task KexInit_LinuxAndMacOSBuilds_ListTheOpenSslAlgorithms() =>
        JudgeKexInitAsync(Named(), [], null, Libssh2Identification, [OpenSslKex, OpenSslHostKey, OpenSslCipher, OpenSslCipher, OpenSslMac, OpenSslMac, "none", "none", string.Empty, string.Empty]);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [Description("Mirrors UpstreamCurlOffersSshAlgorithmsTests.KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms")]
    public Task KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms() =>
        JudgeKexInitAsync(Named(), [], OpenSslWindowsBuildSha256, Libssh2Identification, [OpenSslKex, OpenSslHostKey, OpenSslCipher, OpenSslCipher, OpenSslMac, OpenSslMac, "none", "none", string.Empty, string.Empty]);

    [TestMethod]
    [Description("Mirrors UpstreamCurlOffersSshAlgorithmsTests.KexInit_CompressedSsh_ListsZlibBeforeNone")]
    public Task KexInit_CompressedSsh_ListsZlibBeforeNone() =>
        JudgeKexInitAsync(Named(), ["--compressed-ssh"], null, null, null, (6, "zlib,zlib@openssh.com,none"), (7, "zlib,zlib@openssh.com,none"));

    // --- UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests ---

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [Description("Mirrors UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile")]
    [DataRow("blowfish-cbc")]
    [DataRow("cast128-cbc")]
    public async Task Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile(string cipher) =>
        await JudgeWeakAsync(Named(cipher), ["--ssh-ciphers", cipher], await LegacyProviderEnvironmentAsync(), run =>
            ExitsZeroWriting(HelloWorld)(run) ?? NegotiatedContains(run, $"cipher {cipher}/{cipher},"));

    [TestMethod]
    [OSCondition(OperatingSystems.OSX)]
    [Description("Mirrors UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes")]
    [DataRow("blowfish-cbc")]
    [DataRow("cast128-cbc")]
    public async Task Download_CipherOnlySurlOffersOnTheMacOsBuild_AgreesItAndCurlCrashes(string cipher) =>
        await JudgeWeakAsync(Named(cipher), ["--ssh-ciphers", cipher], await LegacyProviderEnvironmentAsync(), run =>
            (run.ExitCode == KilledBySegmentationFault && run.StandardOutput.Length == 0 ? null : Describe(run))
            ?? NegotiatedContains(run, $"cipher {cipher}/{cipher},"));

    [TestMethod]
    [Description("Mirrors UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_MacOnlySurlOffers_AgreesItAndWritesTheFile")]
    [DataRow("hmac-ripemd160")]
    [DataRow("hmac-ripemd160@openssh.com")]
    public Task Download_MacOnlySurlOffers_AgreesItAndWritesTheFile(string mac) =>
        JudgeWeakAsync(Named(mac), ["--ssh-ciphers", "aes128-ctr", "--ssh-macs", mac], new Dictionary<string, string>(), run =>
            ExitsZeroWriting(HelloWorld)(run) ?? NegotiatedContains(run, $"MAC {mac}/{mac},"));

    // --- The cases ---

    private static CurlPortMeasurementCase Transfer(
        (string Name, string Mirrors) test, string mirroredClass, IReadOnlyList<string> arguments, IReadOnlyList<string> surlOptions, Func<CurlPortMeasuredRun, string?> check) =>
        Case(
            test,
            mirroredClass,
            mirroredClass == Scp ? "scp" : "sftp",
            ["--hostpubsha256", RsaSha256, "-u", Account, .. arguments],
            ["--throwaway-hostkey", "--user", Account, "-v", .. surlOptions],
            TransferFiles,
            check);

    private static CurlPortMeasurementCase LogIn(
        (string Name, string Mirrors) test, string scheme, IReadOnlyList<string> arguments, IReadOnlyList<string> surlOptions, Func<CurlPortMeasuredRun, string?> check) =>
        Case(test, LogsIn, scheme, arguments, ["-v", .. surlOptions], LoginFiles, check);

    private static CurlPortMeasurementCase Case(
        (string Name, string Mirrors) test,
        string mirroredClass,
        string scheme,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> surlOptions,
        IReadOnlyDictionary<string, byte[]> files,
        Func<CurlPortMeasuredRun, string?> check) =>
        new(test.Name, $"{mirroredClass}.{test.Mirrors}", scheme, ["-sS", .. arguments], scheme, surlOptions, files, check)
        {
            AssertedStandardErrorFragments = ["curl: ("],
        };

    /// <summary>
    /// The case's name, the first column of the findings file: the test method's name, followed by its
    /// data row in brackets when it has one; and the mirrored method, the name without its <c>Scp_</c> or
    /// <c>Sftp_</c> prefix.
    /// </summary>
    private static (string Name, string Mirrors) Named(string? dataRow = null, [CallerMemberName] string method = "") =>
        (dataRow is null ? method : $"{method}[{dataRow}]", Regex.Replace(method, "^(Scp|Sftp)_", string.Empty));

    private static string[] Split(string options) => options.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string HostKeySha256Of(string keyType) => $"<surl's {keyType} SHA-256>";

    private static string EllipticCurveHostKeyPem(string keyType) =>
        keyType == "ssh-ed25519" ? SshTestKeys.Ed25519HostKeyPem() : SshTestKeys.EcdsaHostKeyPem();

    private static bool AccountsOwnKnownHostsFileExists() =>
        !OperatingSystem.IsWindows()
        && File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "known_hosts"));

    // --- The mirrored assertions ---

    private static string Describe(CurlPortMeasuredRun run) =>
        $"exit {run.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}";

    private static string? ExitsZero(CurlPortMeasuredRun run) => run.ExitCode == 0 ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsWith(int exitCode) =>
        run => run.ExitCode == exitCode ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsZeroWriting(byte[] expected) =>
        run => run.ExitCode == 0 && run.StandardOutput.AsSpan().SequenceEqual(expected) ? null : Describe(run);

    private static Func<CurlPortMeasuredRun, string?> ExitsWithError(int exitCode, string standardError) =>
        run => run.ExitCode == exitCode && run.StandardError.Trim() == standardError ? null : Describe(run);

    private static string? NotesContain(CurlPortMeasuredRun run, string text) =>
        run.Surl.Notes.Contains(text, StringComparison.Ordinal) ? null : $"surl's notes lack \"{text}\":\n{run.Surl.Notes}";

    private static string? NegotiatedContains(CurlPortMeasuredRun run, string text)
    {
        var negotiated = string.Join('\n', SshLogNotes.NegotiatedAlgorithms(run.Surl.Notes));
        return negotiated.Contains(text, StringComparison.Ordinal) ? null : $"surl negotiated \"{negotiated}\", without \"{text}\"";
    }

    private static string? Absent(CurlPortMeasuredRun run, string name) =>
        run.Surl.StoredFiles.ContainsKey(name) ? $"{name} is stored" : null;

    private static string? Stored(CurlPortMeasuredRun run, string name, byte[] expected) =>
        run.Surl.StoredFiles.TryGetValue(name, out var stored) && stored.AsSpan().SequenceEqual(expected)
            ? null
            : $"{name} stored as {(stored is null ? "nothing" : Convert.ToHexString(stored))}";

    private static string? ModifiedAt(CurlPortMeasuredRun run, string name, DateTime expected) =>
        run.Surl.StoredFiles.TryGetValue(ModificationTimeOf(name), out var time) && Encoding.ASCII.GetString(time) == expected.ToString("O", CultureInfo.InvariantCulture)
            ? null
            : $"{name} modified at {(time is null ? "nothing" : Encoding.ASCII.GetString(time))}, not {expected:O}";

    /// <summary>The compared item holding a client-written file's modification time, in round-trip form.</summary>
    private static string ModificationTimeOf(string name) => $"{name} (modification time)";

    // --- What is compared: surl's notes without packet dumps or this run's host-key fingerprints ---

    private const string SshSha256Placeholder = "<sha256>";

    private const string Libssh2WinCngKeyExchangeFailure = "curl: (2) Failure establishing ssh session: -8, Unable to exchange encryption keys";

    private const int MeasurementAttempts = 3;

    private static readonly TimeSpan ExchangeEndTimeout = TimeSpan.FromSeconds(10);

    private static readonly Regex ExchangeEnded = new(@"\* Exchange \d+ ended", RegexOptions.CultureInvariant);

    /// <summary>
    /// The notes surl writes as the client closes the SSH connection, after the transfer curl reports:
    /// how the client's SSH layer tears down (channel EOF and close before <c>SSH_MSG_DISCONNECT</c>, or
    /// the disconnect alone, or the TCP connection dropped) and the SCP download's final acknowledgement,
    /// which libssh2 sometimes closes the channel before sending. They are inside the encrypted
    /// transport, their order races the client's exit, and ADR-0089's BL-582 amendment leaves them
    /// uncompared; the exit code, output and stored files carry the transfer's outcome.
    /// </summary>
    private static readonly Regex TeardownNote = new(
        @"^#\d+ \* (SSH disconnect received: |SSH channel \d+ ended: |SFTP session ended: |Exchange \d+ ended|SCP download \S+(: sent \d+ bytes| refused: read error: The SSH channel was closed before the data was sent))",
        RegexOptions.CultureInvariant);

    private static readonly string[] ExtensionSwitches = ["CURL_EXTENSIONS", "SURL_EXTENSIONS"];

    /// <summary>
    /// Gets <paramref name="text"/> with each run's throwaway host-key fingerprints replaced: in surl's
    /// <c>Serving SSH host key</c> note and in curl's <c>Remote … is not equal to</c> refusal.
    /// </summary>
    private static string WithoutFingerprints(string text) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(text, @"--hostpubsha256 [A-Za-z0-9+/=]+", $"--hostpubsha256 {SshSha256Placeholder}"),
                "--hostpubmd5 [0-9a-f]{32}",
                "--hostpubmd5 <md5>"),
            @"Remote \S+ is not equal to",
            $"Remote {SshSha256Placeholder} is not equal to");

    private static string ComparedNotes(string notes) =>
        WithoutFingerprints(string.Join('\n', CurlPortRunSetup.WithoutPacketDumps(notes).Split('\n').Where(line => !TeardownNote.IsMatch(line))));

    // --- The judgement ---

    /// <summary>
    /// Runs <paramref name="measurementCase"/> through both clients and applies ADR-0089 decision 5.
    /// Each run's arguments get that run's own host-key fingerprints and, with <paramref name="output"/>,
    /// its own local output file, read back as a compared stored file. Every served file gets one fixed
    /// modification time, and <paramref name="arrange"/> runs, before the client starts.
    /// </summary>
    private async Task JudgeAsync(
        CurlPortMeasurementCase measurementCase,
        PerRunOutputFile? output = null,
        Action<SurlOnLoopback>? arrange = null,
        CurlPortRunSetup? baseSetup = null)
    {
        SurlOnLoopback? currentSurl = null;
        var setup = (baseSetup ?? CurlPortRunSetup.None) with
        {
            ArgumentsForRunAsync = (surl, baseUrl, _) =>
            {
                currentSurl = surl;
                output?.StartRun();
                return Task.FromResult<IReadOnlyList<string>>([.. measurementCase.ArgumentsFor(baseUrl).Select(argument => ForRun(argument, surl, output))]);
            },
            ComparedNotes = notes =>
            {
                // surl may still be writing the exchange's notes when the client exits; read them once it
                // has ended the exchange, so neither run's notes are cut short.
                if (currentSurl is { } surl)
                {
                    SpinWait.SpinUntil(() => ExchangeEnded.IsMatch(surl.Log), ExchangeEndTimeout);
                    notes = surl.Log;
                }

                return ComparedNotes(notes);
            },
            ReadStoredFilesAfterStopAsync = output is null ? null : output.ReadAsync,
        };

        PinnedCurlPortMeasurement measurement;
        var attempt = 0;
        do
        {
            attempt++;
            measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(
                TestContext,
                measurementCase,
                setup,
                (surl, _) =>
                {
                    PinModificationTimes(surl.ServedDirectory!);
                    arrange?.Invoke(surl);
                    return Task.FromResult<IAsyncDisposable>(new NothingToDispose());
                });
        }
        while (attempt < MeasurementAttempts && ShowsLibssh2WinCngKeyExchangeDefect(measurement));

        await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, measurementCase.Name, measurement);
    }

    /// <summary>
    /// Gets whether either run failed the way ADR-0062 decision 3 says libssh2 1.11.1 on WinCNG fails about
    /// one SSH key exchange in 256 against a correct server - exit 2 with exactly
    /// <see cref="Libssh2WinCngKeyExchangeFailure"/> - which a re-run clears. Such a measurement is taken
    /// again, both runs, up to <see cref="MeasurementAttempts"/> times; any other failure is judged as it is.
    /// </summary>
    private bool ShowsLibssh2WinCngKeyExchangeDefect(PinnedCurlPortMeasurement measurement)
    {
        var verdict = measurement.Verdict;
        var shows = verdict.Reason.Contains(Libssh2WinCngKeyExchangeFailure, StringComparison.Ordinal)
            || verdict.Finding is { } finding
            && new[] { finding.Upstream, finding.Port }.Any(run =>
                run.ExitCode == 2 && run.StandardError.Contains(Libssh2WinCngKeyExchangeFailure, StringComparison.Ordinal));
        if (shows)
        {
            TestContext.WriteLine($"A run hit libssh2 1.11.1 WinCNG's key-exchange defect (ADR-0062); measuring again: {verdict}");
        }

        return shows;
    }

    private static string ForRun(string argument, SurlOnLoopback surl, PerRunOutputFile? output)
    {
        foreach (var keyType in new[] { "ssh-rsa", "ecdsa-sha2-nistp256", "ssh-ed25519" })
        {
            if (argument == HostKeySha256Of(keyType))
            {
                return SshLogNotes.HostKeySha256(surl.Log, keyType);
            }
        }

        return argument == RsaMd5
            ? SshLogNotes.HostKeyMd5(surl.Log, "ssh-rsa")
            : output is not null && argument == $"<{output.Name}>" ? output.CurrentPath : argument;
    }

    private Task JudgeWeakAsync(
        (string Name, string Mirrors) test, IReadOnlyList<string> surlOptions, IReadOnlyDictionary<string, string> environment, Func<CurlPortMeasuredRun, string?> check) =>
        JudgeAsync(
            Case(
                test,
                Weak,
                "sftp",
                ["--hostpubsha256", RsaSha256, "-u", Account, "{surl}/a.txt"],
                ["-v", "--throwaway-hostkey", "--user", Account, "--allow-weak-ssh-algorithms", .. surlOptions],
                LoginFiles,
                check),
            baseSetup: new CurlPortRunSetup
            {
                UpstreamBuildSha256 = OperatingSystem.IsWindows() ? OpenSslWindowsBuildSha256 : null,
                Environment = environment,
            });

    /// <summary>
    /// Runs one <c>KEXINIT</c> case: each client, with its own empty <c>HOME</c>, connects to its own
    /// <see cref="ClientKexInitRecorder"/>, which hangs up once it has read the client's identification
    /// line and <c>KEXINIT</c>; the two recordings are compared line by line as each run's notes, beside
    /// the exit code and output. A port run that sends no readable <c>KEXINIT</c> is recorded as such, so
    /// it is judged rather than failing the test.
    /// </summary>
    private async Task JudgeKexInitAsync(
        (string Name, string Mirrors) test,
        IReadOnlyList<string> options,
        string? upstreamBuildSha256,
        string? identification,
        string[]? nameLists,
        params (int Index, string Value)[] nameListsAt)
    {
        RequireNoExtensionSwitch();
        var measurementCase = new CurlPortMeasurementCase(
            test.Name,
            $"{Offers}.{test.Mirrors}",
            "sftp",
            ["-sS", "-k", .. options, "{surl}/x"],
            "sftp",
            [],
            new Dictionary<string, byte[]>(),
            run => CheckKexInit(run, identification, nameLists, nameListsAt));

        var (upstream, upstreamSide) = await RecordKexInitAsync(measurementCase, (arguments, environment) => upstreamBuildSha256 is null
            ? PinnedUpstreamCurl.RunWithEnvironmentAsync(TestContext, environment, [.. arguments])
            : PinnedUpstreamCurl.RunSupplementaryBuildWithEnvironmentAsync(TestContext, upstreamBuildSha256, environment, [.. arguments]), recordingMayFail: false);
        var portRunner = await LocatePortRunnerAsync();
        var (port, portSide) = await RecordKexInitAsync(
            measurementCase,
            (arguments, environment) => portRunner.RunAsync(arguments, ReadOnlyMemory<byte>.Empty, environment, TestContext.CancellationToken),
            recordingMayFail: true);

        var verdict = CurlPortComparison.Compare(measurementCase, upstream, upstreamSide, port, portSide, await SurlCommitAsync());
        TestContext.WriteLine($"{measurementCase.Name}: {verdict}");
        await CurlPortFindingsFile.AssertVerdictIsListedAsync(
            TestContext, FindingsFile, measurementCase.Name, new PinnedCurlPortMeasurement(verdict, upstreamSide, portSide));
    }

    // As UpstreamCurlOffersSshAlgorithmsTests records it (BL-356): the client is awaited first, then
    // the listener is stopped, and only then is the recording read.
    private async Task<(TResult Result, CurlPortSurlSide Side)> RecordKexInitAsync<TResult>(
        CurlPortMeasurementCase measurementCase,
        Func<IReadOnlyList<string>, IReadOnlyDictionary<string, string>, Task<TResult>> runClientAsync,
        bool recordingMayFail)
    {
        using var home = new IsolatedCurlHome();
        using var recorder = ClientKexInitRecorder.Start();
        var recording = recorder.RecordAsync(TestContext.CancellationToken);
        var environment = new Dictionary<string, string>(home.Environment) { ["CURL_HOME"] = home.PathOf(string.Empty) };

        var result = await runClientAsync(measurementCase.ArgumentsFor(new Uri($"sftp://127.0.0.1:{recorder.Port}/")), environment);
        recorder.Dispose();

        string notes;
        try
        {
            var (recordedIdentification, recordedNameLists) = await recording;
            notes = string.Join('\n', [recordedIdentification, .. recordedNameLists]);
        }
        catch (Exception exception) when (recordingMayFail && exception is AssertFailedException or IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            notes = $"no KEXINIT recorded: {exception.GetType().Name}";
        }

        TestContext.WriteLine($"{result}\n{notes}");
        return (result, new CurlPortSurlSide(recorder.Port, notes, new Dictionary<string, byte[]>()));
    }

    private static string? CheckKexInit(CurlPortMeasuredRun run, string? identification, string[]? nameLists, (int Index, string Value)[] nameListsAt)
    {
        var lines = run.Surl.Notes.Split('\n');
        if (lines.Length != 11)
        {
            return $"no KEXINIT recorded: {run.Surl.Notes}";
        }

        var recordedNameLists = lines[1..];
        var differs = (identification is not null && lines[0] != identification)
            || (nameLists is not null && !recordedNameLists.SequenceEqual(nameLists))
            || nameListsAt.Any(expected => recordedNameLists[expected.Index] != expected.Value);
        return differs ? $"recorded:\n{run.Surl.Notes}" : null;
    }

    private async Task<CurlPortClientRunner> LocatePortRunnerAsync()
    {
        var root = PinnedUpstreamCurl.RepositoryRoot();
        var portPins = CurlPortClientPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, CurlPortClientPins.FileName), TestContext.CancellationToken));
        var upstreamPins = UpstreamCurlBuildPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, UpstreamCurlBuildPins.FileName), TestContext.CancellationToken));
        return PinnedCurlPortClientOnThisMachine.RunnerFor(
            new CurlPortClientLocator(new FileSystemUpstreamCurlFileAccess()).Locate(portPins, upstreamPins, UpstreamCurlLocator.CurrentPlatform));
    }

    private static void RequireNoExtensionSwitch()
    {
        foreach (var name in ExtensionSwitches)
        {
            if (Environment.GetEnvironmentVariable(name) is not null)
            {
                Assert.Inconclusive($"{name} is set in the test process; ADR-0089 decision 3 runs both clients and surl as shipped, without it.");
            }
        }
    }

    private async Task<string> SurlCommitAsync()
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-C", PinnedUpstreamCurl.RepositoryRoot(), "rev-parse", "HEAD" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var git = System.Diagnostics.Process.Start(startInfo)!;
        var commit = (await git.StandardOutput.ReadToEndAsync(TestContext.CancellationToken)).Trim();
        await git.WaitForExitAsync(TestContext.CancellationToken);
        return commit.Length > 0 ? commit : "unknown";
    }

    /// <summary>
    /// Gives every served file and folder one fixed modification time, so <c>-R</c> and a long listing
    /// are the same in both runs, whose <c>surl</c>s wrote their content seconds apart.
    /// </summary>
    private static void PinModificationTimes(string servedDirectory)
    {
        foreach (var entry in new DirectoryInfo(servedDirectory).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            entry.LastWriteTimeUtc = ServedModificationTime;
        }
    }

    private Task<string> WriteUploadAsync() => localFiles.WriteFileAsync("up.txt", HelloAgain, TestContext.CancellationToken);

    private Task<string> WriteTextAsync(string name, string text) =>
        localFiles.WriteFileAsync(name, Encoding.UTF8.GetBytes(text), TestContext.CancellationToken);

    private async Task<IReadOnlyDictionary<string, string>> LegacyProviderEnvironmentAsync() =>
        new Dictionary<string, string> { ["OPENSSL_CONF"] = await WriteTextAsync("openssl.cnf", LegacyProviderConfiguration) };

    /// <summary>
    /// A local file curl writes with <c>-o</c>, in a fresh folder per run (optionally holding the partial
    /// content the run resumes), read back after the run as two compared stored files: its bytes, and
    /// its modification time in round-trip form.
    /// </summary>
    private sealed class PerRunOutputFile(IsolatedCurlHome localFiles, string name, byte[]? partialContent)
    {
        private int runs;

        public string Name => name;

        public string CurrentPath { get; private set; } = string.Empty;

        public void StartRun()
        {
            var folder = Directory.CreateDirectory(localFiles.PathOf($"run{++runs}"));
            CurrentPath = Path.Combine(folder.FullName, name);
            if (partialContent is not null)
            {
                File.WriteAllBytes(CurrentPath, partialContent);
            }
        }

        public async Task<IReadOnlyDictionary<string, byte[]>> ReadAsync(CancellationToken cancellationToken)
        {
            var read = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (File.Exists(CurrentPath))
            {
                read[name] = await File.ReadAllBytesAsync(CurrentPath, cancellationToken);
                read[ModificationTimeOf(name)] = Encoding.ASCII.GetBytes(File.GetLastWriteTimeUtc(CurrentPath).ToString("O", CultureInfo.InvariantCulture));
            }

            return read;
        }
    }

    private sealed class NothingToDispose : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
