namespace Surl.Cli;

[TestClass]
public sealed class ManualTextTests
{
    private static readonly string NewLine = Environment.NewLine;

    // ADR-0034 decision 6's sections, in its order.
    private static readonly string[] Headings =
    [
        "NAME", "SYNOPSIS", "DESCRIPTION", "LISTEN URLS", "DEPLOYMENT CHECKLIST", "DATA DIRECTORY",
        "IN-MEMORY MODE", "ACCOUNTS", "SSH OPTIONS", "LOOSENING OPTIONS", "LOG LEVELS", "LIMITS", "EXIT CODES", "SEE ALSO",
    ];

    [TestMethod]
    public void Text_IsTheWholeManual()
    {
        string[] lines =
        [
            "NAME",
            "",
            "    surl - the server-side mate of curl",
            "",
            "SYNOPSIS",
            "",
            "    surl [options] <url>...",
            "",
            "DESCRIPTION",
            "",
            "    surl is the server-side mate of curl: for each request upstream curl makes,",
            "    surl is the server that answers it. Where curl [options] <url> names what",
            "    to fetch, surl [options] <url> names what to listen on. surl --version",
            "    names the schemes this build serves.",
            "",
            "    Options and listen URLs may come in any order and are read left to right;",
            "    the first error ends the reading, and nothing is served. After -- every",
            "    argument is a listen URL.",
            "",
            "LISTEN URLS",
            "",
            "    A listen URL is scheme://host[:port][/]. The host is an IPv4 address, an",
            "    IPv6 address in brackets, or a host name; 0.0.0.0 and [::] listen on every",
            "    interface, and nothing else does. Without a port the scheme's default port",
            "    is used, and port 0 asks for an ephemeral port. A listen URL has no user",
            "    name, password, path, query or fragment.",
            "",
            "    Once every listener has bound, surl writes one line per listen URL to",
            "    stdout, in command-line order:",
            "",
            "        Listening on <scheme>://<host>:<bound port>/",
            "",
            "    The line shows the port each listener got, so a test that asked for port 0",
            "    reads its port from it. Several listen URLs are several listeners; tftp",
            "    listens on UDP and every other scheme on TCP.",
            "",
            "DEPLOYMENT CHECKLIST",
            "",
            "    Before running surl where others can reach it:",
            "",
            "    1. Bind a specific address rather than 0.0.0.0 or [::], unless every",
            "       interface should answer.",
            "    2. Give --directory for files and state that must survive a restart.",
            "    3. Give --cert and --key for secure schemes, never --self-signed.",
            "    4. Configure accounts with --user-file rather than --user, which other",
            "       local users can read in the process list, and let only the user surl",
            "       runs as read the file.",
            "    5. Leave --allow-uploads, --list-directories, --follow-symlinks and",
            "       --serve-dot-files off unless they are needed.",
            "    6. Review the limits (see LIMITS).",
            "    7. Start with no loosening option, and treat any \"surl: warning:\" line at",
            "       start as one left on.",
            "    8. Pick a log level and a --log-file.",
            "    9. Stop surl with Ctrl+C or SIGTERM; it exits 0.",
            "",
            "DATA DIRECTORY",
            "",
            "    --directory <path> serves the files under <path> and keeps service state",
            "    under <path>/.surl/. The .surl folder is never served, even with",
            "    --serve-dot-files: a read of it is answered as a missing entry, a listing",
            "    leaves it out, and an upload into it is refused.",
            "",
            "    While it runs, surl holds a lock on <path>/.surl/lock, so one surl at a",
            "    time serves a data directory; a second one started on it exits 124. The",
            "    lock file stays at exit, and the operating system releases the lock when",
            "    surl ends, even when it is killed.",
            "",
            "    A path that is missing, not a directory or unreadable exits 37; surl does",
            "    not create it. A .surl folder or lock file that cannot be created exits 23,",
            "    so a read-only directory cannot be served.",
            "",
            "    The MQTT server keeps its retained messages in",
            "    <path>/.surl/mqtt/retained-messages and loads them at start; a file that",
            "    cannot be read or does not parse exits 37.",
            "",
            "    The SMTP, IMAP and POP3 servers share the mail store in <path>/.surl/mail,",
            "    an index and one file per message, and load it at start; an index or",
            "    message file that cannot be read or does not parse exits 37. Without",
            "    --directory, the mail store lives in memory only.",
            "",
            "IN-MEMORY MODE",
            "",
            "    Without --directory, surl serves an in-memory file system. It starts empty,",
            "    holds at most 256 MiB, touches no disk, and is lost at exit. Uploads into",
            "    it still need --allow-uploads.",
            "",
            "ACCOUNTS",
            "",
            "    -u, --user <user:password> adds one account, and may be given again to add",
            "    more. The value is split at its first colon, so a user name never holds",
            "    one. An empty user name makes the password a Bearer token.",
            "",
            "    --user-file <file> reads accounts from a file, after every --user account.",
            "    The file is UTF-8 (a byte-order mark is skipped), with LF or CR LF line",
            "    ends; each line is one user:password, read as --user reads its value, and",
            "    blank lines and lines starting with # are skipped. The passwords are in",
            "    clear, so let only the user surl runs as read the file. A file that cannot",
            "    be read exits 37, and a malformed line exits 2, naming the line.",
            "",
            "    --keytab <file> reads Kerberos service keys from an MIT keytab file when",
            "    surl starts, before any listener binds: the version 0x0502 format ktutil,",
            "    kadmin ktadd and Windows' ktpass /out write, big-endian entries of",
            "    principal, timestamp, key version number, enctype and key. A key serves",
            "    the principal the keytab names it for, <service>/<host>@<REALM>, where the",
            "    service is HTTP for http and https, smtp, imap or pop; the host and the",
            "    realm are whatever the keytab names. Only the AES keys are kept",
            "    (aes128-cts-hmac-sha1-96, aes256-cts-hmac-sha1-96,",
            "    aes128-cts-hmac-sha256-128, aes256-cts-hmac-sha384-192); each key of",
            "    another enctype, such as rc4-hmac, is skipped with \"surl: warning:",
            "    --keytab: skipped the <enctype> key of <principal>\". A file that cannot be",
            "    read exits 37; one that is not a version 0x0502 keytab or is cut short,",
            "    and one with no AES key, exit 2. --auth gssapi needs --keytab, and",
            "    --keytab when --auth names neither negotiate nor gssapi (the default",
            "    names neither) warns that it is unused. No key byte is ever logged. In",
            "    this build no login uses the keys yet: Negotiate carries only NTLM, and",
            "    gssapi is not available. Once one does, a Kerberos login is the ticket's",
            "    client principal, written user@EXAMPLE.COM, and is accepted only when an",
            "    account of exactly that name exists; its password is not used.",
            "",
            "    Unless --allow-anonymous is given: with no account configured, an HTTP GET",
            "    or HEAD needs no login, and every login is refused; once any account is",
            "    configured, every HTTP request needs a login; any other HTTP method,",
            "    every MQTT CONNECT, every SMTP MAIL, every IMAP command that reads or",
            "    changes a mailbox, every POP3 maildrop command and every FTP login always",
            "    needs one.",
            "",
            "    --auth <methods> sets the methods accepted and offered, comma-separated,",
            "    in any case. For HTTP: negotiate, ntlm, digest, basic, bearer and",
            "    aws-sigv4. For SMTP, IMAP and POP3 logins, each SASL mechanism by its",
            "    name in lower case, as curl's login option AUTH=<mech> names it: ntlm,",
            "    digest-md5, cram-md5, plain, login, oauthbearer, xoauth2 and external,",
            "    and apop for POP3's APOP. Without it they are digest, cram-md5, basic,",
            "    plain, login, bearer, oauthbearer, xoauth2, external and aws-sigv4.",
            "    external logs in as the TLS client certificate --cacert verifies, so it",
            "    is offered only on a connection that sent one. gssapi is read, but a",
            "    start that gives it without --keytab writes \"surl: (2) --auth gssapi",
            "    needs --keytab\", and with it \"surl: (2) --auth gssapi is not available",
            "    in this build\"; both exit 2.",
            "",
            "SSH OPTIONS",
            "",
            "    The SSH server answers scp and sftp listen URLs, and each needs a host key.",
            "    --hostkey <file> names one SSH host private key, and may be given again to",
            "    add more, one per key type. --throwaway-hostkey asks for a throwaway RSA",
            "    host key instead, made at start, and cannot be used with --hostkey. --pass",
            "    <phrase> decrypts a --hostkey key as it does a --key. With neither, an scp",
            "    or sftp listen URL exits 2. A --hostkey file that cannot be read exits 37,",
            "    and one surl cannot use exits 2, naming the file. With -v, surl writes each",
            "    host key's SHA-256 and MD5 hashes, the values curl pins a host key with.",
            "",
            "    --authorized-keys <user:file> names the OpenSSH authorized_keys file whose",
            "    public keys <user> may log in with, and may be given again for other",
            "    users. The value is split at its first colon, so a Windows path after it",
            "    keeps its drive colon; a user given twice is refused. A file that cannot",
            "    be read exits 37, and a line that is not a key exits 2, naming the line.",
            "    Password logins are checked against --user and --user-file.",
            "",
            "    --hostcert <file> names one OpenSSH host certificate for a --hostkey key,",
            "    and --allow-weak-ssh-algorithms also offers SSH algorithms built on SHA-1,",
            "    MD5, CBC, RC4, 3DES and 1024-bit Diffie-Hellman. Both are read, but this",
            "    build does not serve them yet: a start that gives one writes \"surl: (2)",
            "    --<option> is not available in this build\" and exits 2.",
            "",
            "LOOSENING OPTIONS",
            "",
            "    --allow-anonymous, --allow-plaintext-auth, --auth, --self-signed and",
            "    --throwaway-hostkey each loosen a secure default for tests, and each",
            "    writes a \"surl: warning:\" line on start (--self-signed and",
            "    --throwaway-hostkey only when they make their certificate or key).",
            "    What each one loosens, and why none is the default, is written by:",
            "",
            "        surl --help testing",
            "",
            "LOG LEVELS",
            "",
            "    surl has five log levels, each writing what the one before it writes and",
            "    more:",
            "",
            "    - none (-s): nothing, not even the Listening on lines or the failure",
            "      messages; the exit code still says what failed.",
            "    - error (-s -S): the \"surl: (N)\" failure messages on stderr, and a note",
            "      when a protocol server fails.",
            "    - info (the default): the Listening on lines, the startup warnings and one",
            "      line per exchange.",
            "    - verbose (-v): a line for every exchange event.",
            "    - trace (--trace <file>, --trace-ascii <file>): a dump of every byte to",
            "      <file>, or to stdout for -; --trace-ascii leaves out the hex.",
            "",
            "    --log-level <level> sets a level by its name. The last level option given",
            "    wins, and -S is applied after the whole command line is read. --trace-time",
            "    stamps each exchange log line with the time.",
            "",
            "    The log goes to stderr, or is appended to the file --log-file <file> names",
            "    (- for stdout). The failure messages and command-line errors always go to",
            "    stderr, and the help, the manual and the version are written at every",
            "    level. A log or trace file that cannot be opened exits 23, and so does a",
            "    trace file that is the --log-file file.",
            "",
            "LIMITS",
            "",
            "    Each limit is 0 for no limit:",
            "",
            "    - --max-connections <number>: connections at once, all listeners together;",
            "      default 1024.",
            "    - --max-connections-per-address <number>: connections at once from one",
            "      address; default 100.",
            "    - --idle-timeout <seconds>: an exchange idle this long is closed; default",
            "      120.",
            "    - -m, --max-time <seconds>: the longest one exchange may take; default",
            "      3600.",
            "    - --head-timeout <seconds>: the time a client has to send a request head,",
            "      command line or first packet; default 30.",
            "    - --max-request-head <bytes>: the largest HTTP request head; default 100k.",
            "    - --max-line <bytes>: the longest command line; default 8192.",
            "    - --max-message <bytes>: the largest framed message, such as an MQTT",
            "      packet; default 1M.",
            "    - --max-filesize <bytes>: the largest upload; default 100M.",
            "",
            "    A size may end in k, M, G, T or P, each 1024 times the one before. A limit",
            "    reached ends only the connection or exchange that reached it, answered in",
            "    its protocol's own words where it has them; it never ends surl.",
            "",
            "EXIT CODES",
            "",
            "    - 0: help, an --aihelp answer, the manual or the version was written, or",
            "      surl was stopped by Ctrl+C or SIGTERM.",
            "    - 1: a listen URL names a scheme this build does not serve.",
            "    - 2: the command line cannot be used: an option refused or not available",
            "      in this build, a malformed --user-file, --authorized-keys or --keytab",
            "      file, a --keytab with no key surl can use, a --hostkey file surl cannot",
            "      use, an scp or sftp listen URL with no host key, or a --cacert file that",
            "      does not exist.",
            "    - 3: a listen URL is malformed.",
            "    - 6: a listen URL names a host that resolves to nothing.",
            "    - 23: the .surl folder or its lock file cannot be created, a log or trace",
            "      file cannot be opened, or the trace file is the --log-file file.",
            "    - 37: the data directory cannot be opened, or the --user-file, an",
            "      --authorized-keys, --keytab or --hostkey file, the MQTT retained-message",
            "      file or the mail store cannot be read.",
            "    - 45: a listener cannot bind its address and port.",
            "    - 58: a secure listen URL has no certificate, or the --cert or --key file",
            "      cannot be used.",
            "    - 77: the --cacert file cannot be read.",
            "    - 124: another surl holds the data directory.",
            "    - 125: surl failed while serving.",
            "",
            "SEE ALSO",
            "",
            "    surl --help all, surl --help category, surl --aihelp, curl(1),",
            "    https://github.com/StewartScottRogers/Surl",
        ];

        Assert.AreEqual(string.Concat(lines.Select(line => line + NewLine)), ManualText.Text);
    }

    [TestMethod]
    public void Text_HasTheSectionsInOrderEachFollowedByAnEmptyLineAndAnIndentedBody()
    {
        var lines = Lines();
        var headings = lines.Where(line => line.Length > 0 && line[0] != ' ').ToArray();

        CollectionAssert.AreEqual(Headings, headings);
        foreach (var heading in headings)
        {
            var index = Array.IndexOf(lines, heading);
            Assert.AreEqual(string.Empty, lines[index + 1], heading);
            StringAssert.StartsWith(lines[index + 2], "    ", heading);
            Assert.IsTrue(index == 0 || lines[index - 1].Length == 0, heading);
        }
    }

    [TestMethod]
    public void Text_HasNoTabNoTrailingSpaceAndNoLinePast79Columns()
    {
        foreach (var line in Lines())
        {
            Assert.AreEqual(line.TrimEnd(), line);
            Assert.DoesNotContain("\t", line);
            Assert.IsLessThanOrEqualTo(79, line.Length, line);
        }
    }

    [TestMethod]
    public void Text_NamesOnlyOptionsThatExist() =>
        OptionNames.AssertEveryNamedOptionExists(ManualText.Text);

    [TestMethod]
    public void Text_NamesEveryLooseningOptionAndPointsToHelpTesting()
    {
        var looseningSection = Section("LOOSENING OPTIONS");

        foreach (var looseningOption in new[] { "--allow-anonymous", "--allow-plaintext-auth", "--auth", "--self-signed", "--throwaway-hostkey" })
        {
            StringAssert.Contains(looseningSection, looseningOption);
        }

        StringAssert.Contains(looseningSection, "surl --help testing");
    }

    private static string[] Lines() => ManualText.Text.Split(NewLine)[..^1];

    private static string Section(string heading)
    {
        var lines = Lines();
        var start = Array.IndexOf(lines, heading);
        var end = Array.FindIndex(lines, start + 1, line => line.Length > 0 && line[0] != ' ');
        return string.Join(" ", lines[start..end]);
    }
}
