using System.Collections.Frozen;

namespace Surl.Cli;

/// <summary>
/// The hand-written paragraphs of <c>surl --aihelp</c> (ADR-0046 decision 6): the overview's
/// <c>About</c>, <c>Command line</c> and <c>Conventions</c> and each topic's <c>About</c>, one
/// line per paragraph or list item. Each statement is true of the code it describes, and says it
/// as <see cref="ManualText"/> does where both say the same thing; work that changes that code
/// updates the paragraph in the same change.
/// </summary>
internal static class AiHelpProse
{
    /// <summary>The overview's <c>About</c> paragraphs.</summary>
    public static IReadOnlyList<string> OverviewAbout { get; } =
    [
        "surl is the server-side mate of curl: for each request upstream curl makes, surl is the server that answers it. Where `curl [options] <url>` names what to fetch, `surl [options] <url>` names what to listen on.",
        "`surl --version` names the schemes this build serves. Each protocol has a topic below with its schemes, their default ports, its options and an upstream curl command line that reaches it.",
        "surl serves until it is stopped with Ctrl+C or SIGTERM, and then exits 0.",
    ];

    /// <summary>The overview's <c>Command line</c> paragraphs.</summary>
    public static IReadOnlyList<string> OverviewCommandLine { get; } =
    [
        "A command line is `surl [options] <url>...`, with at least one listen URL (see the listen-urls topic).",
        "Options and listen URLs may come in any order and are read left to right; the first error ends the reading, and nothing is served. After `--` every argument is a listen URL.",
        "A long option's argument is the next argument (`--max-time 30`) or follows an equals sign (`--max-time=30`).",
        "Short options may be bundled: `-vs` is `-v -s`. An option that takes an argument ends the bundle and takes the rest of it as its argument, so `-vm30` is `-v -m 30`; with nothing left, it takes the next argument.",
        "A flag whose Allowed values say `--no-<name> turns it off` is turned off with `--no-<name>`, such as `--no-verbose`.",
        "An option or argument the command line refuses writes `surl: option --<name>: <reason>` to stderr, then `surl: try 'surl --help' or 'surl --manual' for more information`, and exits 2.",
    ];

    /// <summary>The overview's <c>Conventions</c> paragraphs.</summary>
    public static IReadOnlyList<string> OverviewConventions { get; } =
    [
        "Every topic page has the same five sections, in this order: About, Schemes, Options, Exit codes and Examples. A section with nothing in it holds the one line `Nothing for this topic.`.",
        "In the option table, Option is how the option is written; Argument type and Allowed values say what its argument may be; Default is its value when it is not given (`not applicable` when it has none); Loosens security says `yes, for tests only` for an option that loosens a secure default and writes a warning, `yes, widens what a peer may do` for one that exposes more, and `no` otherwise; Categories names every topic that lists it; Description is its `surl --help` line.",
        "In the exit-code table, Code is the process exit code, Name its name in surl's source, Meaning when surl returns it, and What to do next the step that fixes it.",
        "In an example, `<port>` stands for the port surl bound and `<path>` for a directory; substitute them. An example that serves keeps serving until it is stopped with Ctrl+C or SIGTERM, and then exits 0.",
        "`surl --help`, `surl --manual`, `surl --version` and these pages are written to stdout at every log level, `-s` included.",
    ];

    /// <summary>Each topic's <c>About</c> paragraphs, by topic name.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> TopicAbout { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["auth"] =
        [
            "`-u, --user <user:password>` adds one account, and may be given again to add more. The value is split at its first colon, so a user name never holds one. An empty user name makes the password a Bearer token. A user name given twice is refused.",
            "`--user-file <file>` reads accounts from a file, after every `--user` account. The file is UTF-8 (a byte-order mark is skipped), with LF or CR LF line ends; each line is one `user:password`, read as `--user` reads its value, and blank lines and lines starting with `#` are skipped. A file that cannot be read exits 37, and a malformed line exits 2, naming the line.",
            "Other local users can read `--user` in the process list, and the `--user-file` passwords are in clear: outside a test, use `--user-file` and let only the user surl runs as read it.",
            "Unless `--allow-anonymous` is given: with no account configured, an HTTP GET or HEAD needs no login, and every login is refused; once any account is configured, every HTTP request needs a login; any other HTTP method, every MQTT CONNECT, every SMTP MAIL and every FTP login always needs one.",
            "A password or token sent in clear over an unencrypted connection (HTTP Basic or Bearer over `http://`, an MQTT password over `mqtt://`, SMTP PLAIN or LOGIN over `smtp://` without STARTTLS, an FTP password over `ftp://` without AUTH TLS) is refused unchecked unless `--allow-plaintext-auth` is given, which is for tests only. Digest sends no password in clear, so `curl --digest -u user:password` logs in over `http://`; over `https://` and `mqtts://` every method is accepted.",
            "`--auth <methods>` sets the methods accepted and offered: negotiate, ntlm, digest, basic, bearer and aws-sigv4 for HTTP, and for SMTP, IMAP and POP3 logins each SASL mechanism by its lower-case name, as curl's login option `AUTH=<mech>` names it (ntlm, digest-md5, cram-md5, plain, login, oauthbearer, xoauth2, external), plus apop for POP3's APOP; of those three protocols this build serves only SMTP yet. Without it they are digest, cram-md5, basic, plain, login, bearer, oauthbearer, xoauth2, external and aws-sigv4. `external` logs in as the TLS client certificate `--cacert` verifies, so it is offered only on a connection that sent one. `gssapi` is read, but a start that gives it writes `surl: (2) --auth gssapi is not available in this build` and exits 2. It is a loosening option for tests (see the testing topic).",
            "The SSH server (see the ssh topic) checks a password or keyboard-interactive login against the accounts, never refused as plain text since SSH encrypts first, and a public-key login against `--authorized-keys <user:file>`: the OpenSSH `authorized_keys` file whose public keys `<user>` may log in with, repeatable for other users. The value is split at its first colon, and a user given twice is refused. A file that cannot be read writes `surl: (37) Could not read authorized keys <file>` and exits 37, and a line that is not a key exits 2, naming the line.",
            "`--hostkey <file>` names one SSH host private key and `--hostcert <file>` one OpenSSH host certificate, each repeatable; `--pass <phrase>` decrypts a `--hostkey` key as it does a `--key`. `--hostcert` is read, but a start that gives it writes `surl: (2) --hostcert is not available in this build` and exits 2.",
        ],
        ["content"] =
        [
            "Without `--directory`, surl serves an in-memory file system. It starts empty, holds at most 256 MiB, touches no disk, and is lost at exit. Uploads into it still need `--allow-uploads`.",
            "`--directory <path>` serves the files under `<path>` and keeps service state under `<path>/.surl/`. The `.surl` folder is never served, even with `--serve-dot-files`: a read of it is answered as a missing entry, a listing leaves it out, and an upload into it is refused.",
            "While it runs, surl holds a lock on `<path>/.surl/lock`, so one surl at a time serves a data directory; a second one started on it writes `surl: (124) Directory <path> is in use by another surl process` and exits 124. The lock file stays at exit, and the operating system releases the lock when surl ends, even when it is killed.",
            "A path that is missing, not a directory or unreadable writes `surl: (37) Could not open directory <path>` and exits 37; surl does not create it. A `.surl` folder or lock file that cannot be created exits 23, so a read-only directory cannot be served.",
            "Every protocol reads the one served root: HTTP, FTP, Gopher, TFTP, SCP and SFTP serve its files, and DICT reads the files directly in it as definitions. With `--directory`, the MQTT server keeps its retained messages in `<path>/.surl/mqtt/retained-messages` and the SMTP server the mail store in `<path>/.surl/mail`; without it, both live in memory only.",
            "Uploads, directory listings, symbolic links and names that start with a dot are off by default; `--allow-uploads` (FTP, TFTP, SCP and SFTP writes), `--list-directories` (FTP listings, Gopher menus, SFTP directory reads), `--follow-symlinks` and `--serve-dot-files` turn each on. The HTTP server answers a directory with 404 Not Found, listings on or off.",
        ],
        ["dict"] =
        [
            "The DICT server (RFC 2229) answers the `DEFINE`, `MATCH` and `SHOW` requests upstream curl sends for `dict://` URLs, and the rest of RFC 2229's commands.",
            "It serves one database, `surl`: each file directly in the served root is a headword, its name the word and its bytes the definition. Headwords are compared case and all; a name that starts with a dot and a directory are never headwords.",
            "`curl dict://127.0.0.1:<port>/d:<word>` asks for the definition of `<word>`, and `m:<word>` for the headwords that match it.",
        ],
        ["exit-codes"] =
        [
            "Every exit code surl returns, with what it means and what to do next. The Exit codes section of every other topic lists the codes tied to that topic; this page lists them all.",
            "A failure writes a `surl:` line naming what failed to stderr: `surl: (N) <message>` for a failure found once the command line is read, or `surl: option --<name>: <reason>` and the `try` line for a refused command line.",
            "With `-s` and without `-S`, a failure found once the command line is read writes nothing, and only the exit code says what failed; a refused command line is always written.",
        ],
        ["ftp"] =
        [
            "The FTP server (RFC 959) answers the commands upstream curl sends over a control connection and moves files over a separate data connection, passive (`EPSV`, `PASV`) or active (`curl -P -`, `EPRT`, `PORT`) to the client's own address only. `curl ftp://127.0.0.1:<port>/<file>` reads `<file>`, and a URL ending in `/` lists the directory, which needs `--list-directories`. `curl -T <local file>` writes a file and `curl -Q` sends `DELE`, `MKD`, `RMD` and `RNFR`/`RNTO`, each of which needs `--allow-uploads`; an upload past `--max-filesize` is answered 552 and not kept.",
            "Every login needs an account unless `--allow-anonymous` is given, curl's own anonymous login included, so `curl ftp://...` without `-u` needs `--allow-anonymous`. A refused login is answered 530, and curl exits 67. A password over `ftp://` without TLS is refused unchecked unless `--allow-plaintext-auth` is given (see the auth topic).",
            "With `--cert` or `--self-signed`, `ftp://` offers `AUTH TLS` (`curl --ssl-reqd`), and its data connections are then TLS after `PROT P`; without a certificate `AUTH` is answered 534, and `curl --ssl-reqd` exits 64. `ftps` is the same server over TLS from the first byte, so an `ftps` listen URL needs `--cert` or `--self-signed` (see the tls topic).",
            "`--max-line` bounds one command line (500 and a close past it), and `--head-timeout` the time to finish one (421 and a close).",
        ],
        ["gopher"] =
        [
            "The Gopher server (RFC 1436) answers one selector per connection. The selector is read as a percent-encoded path into the served root: a file is sent byte for byte, and a directory as an RFC 1436 menu when `--list-directories` is given.",
            "A selector that names nothing, a directory while listings are off, and anything the served root refuses are all answered with the same fixed error menu, which never echoes the selector.",
            "`gophers` is the same server over TLS from the first byte, so a `gophers` listen URL needs `--cert` or `--self-signed` (see the tls topic).",
        ],
        ["http"] =
        [
            "The HTTP server answers GET and HEAD for files in the served root over HTTP/1.0 and HTTP/1.1, one request after another on a persistent connection. A file is answered 200 OK with `Content-Type: application/octet-stream` and `Content-Length`, then its bytes for GET.",
            "A missing path, a refused path and a directory are answered 404 Not Found with an empty body. POST, PUT, DELETE, CONNECT, OPTIONS, TRACE and PATCH are answered 405 Method Not Allowed with `Allow: GET, HEAD`, and any other method 501 Not Implemented. Every response names the server `Server: surl`, with no version.",
            "`https` is the same server over TLS from the first byte, so an `https` listen URL needs `--cert` or `--self-signed` (see the tls topic).",
            "Logins follow the auth topic: with an account over `http://`, log in with Digest (`curl --digest -u user:password`), since Basic and Bearer are refused unchecked there unless `--allow-plaintext-auth` is given.",
        ],
        ["limits"] =
        [
            "Each limit is 0 for no limit:",
            "- `--max-connections <number>`: connections at once, all listeners together; default 1024.",
            "- `--max-connections-per-address <number>`: connections at once from one address; default 100.",
            "- `--idle-timeout <seconds>`: an exchange idle this long is closed; default 120.",
            "- `-m, --max-time <seconds>`: the longest one exchange may take; default 3600.",
            "- `--head-timeout <seconds>`: the time a client has to send a request head, command line or first packet; default 30.",
            "- `--max-request-head <bytes>`: the largest HTTP request head; default 100k.",
            "- `--max-line <bytes>`: the longest command line; default 8192.",
            "- `--max-message <bytes>`: the largest framed message, such as an MQTT packet; default 1M.",
            "- `--max-filesize <bytes>`: the largest upload; default 100M.",
            "A size may end in k, M, G, T or P, each 1024 times the one before. A limit reached ends only the connection or exchange that reached it, answered in its protocol's own words where it has them; it never ends surl.",
        ],
        ["listen-urls"] =
        [
            "A listen URL is `scheme://host[:port][/]`. The host is an IPv4 address, an IPv6 address in brackets, or a host name; `0.0.0.0` and `[::]` listen on every interface, and nothing else does. A listen URL has no user name, password, path, query or fragment; one that does, or is otherwise malformed, writes `surl: (3) URL rejected: <reason>` and exits 3.",
            "Without a port the scheme's default port is used (each protocol topic's Schemes section lists it), and port 0 asks for an ephemeral port.",
            "Once every listener has bound, surl writes one line per listen URL to stdout, in command-line order: `Listening on <scheme>://<host>:<bound port>/`. The line shows the port each listener got, so a test that asked for port 0 waits for the line and reads its port from the digits between the last `:` and the final `/`.",
            "Several listen URLs are several listeners, each with its own line. `tftp` listens on UDP and every other scheme on TCP.",
            "A scheme this build does not serve writes `surl: (1) Protocol \"<scheme>\" not supported` and exits 1, before any listener binds; `surl --version` lists the schemes it serves.",
            "A listener that cannot bind writes `surl: (45) Could not bind <scheme>://<address>:<port>/: <reason>` and exits 45, and a host that resolves to nothing writes `surl: (6) Could not resolve host: <host>` and exits 6.",
            "surl serves until it is stopped with Ctrl+C or SIGTERM, and then exits 0.",
        ],
        ["logging"] =
        [
            "surl has five log levels, each writing what the one before it writes and more:",
            "- none (`-s`): nothing, not even the Listening on lines or the failure messages; the exit code still says what failed.",
            "- error (`-s -S`): the `surl: (N)` failure messages on stderr, and a note when a protocol server fails.",
            "- info (the default): the Listening on lines, the startup warnings and one line per exchange.",
            "- verbose (`-v`): a line for every exchange event.",
            "- trace (`--trace <file>`, `--trace-ascii <file>`): a dump of every byte to `<file>`, or to stdout for `-`; `--trace-ascii` leaves out the hex.",
            "`--log-level <level>` sets a level by its name. The last level option given wins, and `-S` is applied after the whole command line is read. `--trace-time` stamps each exchange log line with the time.",
            "The log goes to stderr, or is appended to the file `--log-file <file>` names (`-` for stdout). The failure messages and command-line errors always go to stderr. A log or trace file that cannot be opened exits 23, and so does a trace file that is the `--log-file` file.",
        ],
        ["mqtt"] =
        [
            "The MQTT server (MQTT 3.1.1) accepts the CONNECT, the SUBSCRIBE and the PUBLISH upstream curl sends. `curl mqtt://127.0.0.1:<port>/<topic>` subscribes to `<topic>` and is answered with the retained messages its filter matches, then DISCONNECT; `curl -d <message> mqtt://127.0.0.1:<port>/<topic>` publishes `<message>`, which is kept as the topic's retained message.",
            "Every CONNECT needs a login unless `--allow-anonymous` is given: its user name and password are checked against the accounts (see the auth topic), answered CONNACK 0 when accepted, and CONNACK 4 or 5 and a close when refused. A password over `mqtt://` is refused unchecked unless `--allow-plaintext-auth` is given.",
            "Without `--directory` the retained messages live in memory only; with it, they are kept in `<path>/.surl/mqtt/retained-messages` and loaded at start, and a file that cannot be read or does not parse exits 37.",
            "`mqtts` is the same server over TLS from the first byte, so an `mqtts` listen URL needs `--cert` or `--self-signed` (see the tls topic).",
        ],
        ["security"] =
        [
            "Every option here widens what a peer may do, and each is off by default. Six are deployment choices that write no warning: `--allow-uploads`, `--list-directories`, `--follow-symlinks`, `--serve-dot-files`, `--tlsv1.0` and `--tlsv1.1`; leave each off unless it is needed.",
            "`--allow-weak-ssh-algorithms` also offers SSH algorithms built on SHA-1, MD5, CBC, RC4, 3DES and 1024-bit Diffie-Hellman. This build does not offer them yet, so a start that gives it writes `surl: (2) --allow-weak-ssh-algorithms is not available in this build` and exits 2.",
            "The other five, `--allow-anonymous`, `--allow-plaintext-auth`, `--auth`, `--self-signed` and `--throwaway-hostkey`, loosen a secure default and are for tests only (see the testing topic).",
            "Before running surl where others can reach it: bind a specific address rather than `0.0.0.0` or `[::]`; give `--cert` and `--key` for secure schemes, never `--self-signed`; configure accounts with `--user-file`; review the limits; start with no loosening option, and treat any `surl: warning:` line at start as one left on.",
        ],
        ["smtp"] =
        [
            "The SMTP server (RFC 5321) receives the mail upstream curl sends. `curl --mail-from <address> --mail-rcpt <address> -T <file> smtp://127.0.0.1:<port>/<domain>` sends `<file>` as one message, which is stored in the `INBOX` of each recipient whose local part names an account; a recipient that names no account is answered as one that does, and its copy is discarded. `curl smtp://127.0.0.1:<port>/` with nothing to send is answered with the `HELP` reply.",
            "`MAIL` needs a login unless `--allow-anonymous` is given, when every recipient's copy goes to one anonymous `INBOX`. `curl -u user:password` logs in with `AUTH` and a SASL mechanism `--auth` accepts (see the auth topic); over `smtp://` without STARTTLS, PLAIN and LOGIN are not offered unless `--allow-plaintext-auth` is given, and CRAM-MD5 is.",
            "With `--cert` or `--self-signed`, `smtp://` offers STARTTLS (`curl --ssl-reqd`); without a certificate STARTTLS is answered 454. `smtps` is the same server over TLS from the first byte, so an `smtps` listen URL needs `--cert` or `--self-signed` (see the tls topic).",
            "Without `--directory` the mail store lives in memory only; with it, it is kept in `<path>/.surl/mail` and loaded at start, and an index or message file that cannot be read or does not parse exits 37. A message past `--max-filesize`, counted with the `Return-Path` and `Received` fields surl adds, is answered 552 and not stored.",
        ],
        ["ssh"] =
        [
            "The SSH server (SSH 2.0, RFC 4251 to RFC 4254) answers `scp` and `sftp` listen URLs alike: one server, whose SCP command and SFTP subsystem serve the one served root. `curl -u user:password -k sftp://127.0.0.1:<port>/<file>` and `scp://` read `<file>`; `curl -T <local file>` writes it, which needs `--allow-uploads`; `curl -Q` sends SFTP commands such as `rm`, `rename` and `mkdir`, and an SFTP directory is listed only with `--list-directories`.",
            "An `scp` or `sftp` listen URL needs a host key: `--hostkey <file>` (an OpenSSH, PKCS #8, PKCS #1 or SEC 1 private key; RSA of at least 2048 bits, ECDSA or Ed25519; one per key type; `--pass` for an encrypted one), or `--throwaway-hostkey` for a throwaway RSA key made at start, for tests only. Without either, surl writes `surl: (2) <url> needs a host key: give --hostkey <file>, or --throwaway-hostkey for a throwaway one` and exits 2. A `--hostkey` file that cannot be read exits 37, and one surl cannot use exits 2, naming the file.",
            "curl checks the host key: with `-v`, surl writes `* Serving SSH host key <key type>, --hostpubsha256 <base64> --hostpubmd5 <hex>` for each key before the Listening on lines, and a test passes `--hostpubsha256 <base64>` to curl, or `-k` to skip the check.",
            "Every login needs an account unless `--allow-anonymous` is given: a password or keyboard-interactive login is checked against `--user` and `--user-file`, and a public-key login against `--authorized-keys` (see the auth topic). Six refused attempts end the connection.",
            "`--head-timeout` covers everything from the connection to the login, `--max-message` bounds one SSH packet, and `--max-filesize` one upload.",
        ],
        ["surl"] =
        [
            "`-h, --help <subject>`, `-M, --manual` and `-V, --version` each end the reading of the command line, write their text to stdout and exit 0, whatever else is on it. The first of them read wins; an error read before it still refuses the command line.",
            "`surl --help` writes the short list; `surl --help <subject>` writes a category (`surl --help category` lists them), `all` for every option, or one option's page. `surl --manual` writes the whole manual, and `surl --version` the version and the schemes this build serves.",
            "A command line with no listen URL writes `surl: (2) no URL specified` and the `try` line to stderr and exits 2.",
        ],
        ["telnet"] =
        [
            "The TELNET server (RFC 854, RFC 855) negotiates options with the client, reports what the client tells it about its terminal and environment, then echoes each line back until the client sends `quit`.",
            "`curl telnet://127.0.0.1:<port>/` carries the session: what curl reads from stdin is sent, and what the server answers is written to stdout. `curl -t <option=value>` sets curl's side of the negotiation.",
        ],
        ["testing"] =
        [
            "`--allow-anonymous`, `--allow-plaintext-auth`, `--auth` and `--self-signed` each loosen a secure default so a test can reach surl without accounts, without TLS or without a certificate file. They are for tests only.",
            "Each writes a `surl: warning:` line on start, from the info level up, to the log (stderr unless `--log-file` is given); `--self-signed` writes its line only when it makes its certificate. What each one loosens, and why none is the default, follows the option table.",
            "`--throwaway-hostkey` loosens the SSH host key the same way `--self-signed` loosens the certificate: surl makes a throwaway RSA key when it serves an `scp` or `sftp` listen URL and writes `surl: warning: --throwaway-hostkey: serving a throwaway SSH host key (--hostpubsha256 <base64>); clients must pin it or skip the check (curl -k)` (see the ssh topic).",
        ],
        ["tftp"] =
        [
            "The TFTP server (RFC 1350) listens on UDP and answers read and write requests with the `blksize`, `tsize` and `timeout` options (RFC 2347 to RFC 2349) upstream curl sends, from and into the served root.",
            "`curl tftp://127.0.0.1:<port>/<file>` reads `<file>`; `curl -T <local file> tftp://127.0.0.1:<port>/<file>` writes it, which needs `--allow-uploads`. A missing or refused file is answered with TFTP ERROR 1, a write while uploads are off with ERROR 2, and a write past `--max-filesize` with ERROR 3.",
        ],
        ["tls"] =
        [
            "`https`, `ftps`, `gophers`, `mqtts` and `smtps` start with a TLS handshake, so a listen URL of one of them needs a certificate: `--cert <file>` (PEM unless `--cert-type` says DER or P12), with `--key <file>` when the key is not in the `--cert` file and `--pass <phrase>` when the key needs one.",
            "Without `--cert` and without `--self-signed`, such a listen URL writes `surl: (58) <url> needs a certificate: give --cert <file>, or --self-signed for a throwaway one` and exits 58, and a `--cert` or `--key` file that cannot be used exits 58. `--key` or `--key-type` without `--cert`, `--pass` without `--cert` or `--hostkey`, `--key` with `--cert-type P12`, and `--self-signed` with `--cert` are refused, exit 2.",
            "`--self-signed` is for tests: surl makes a throwaway certificate at start and writes `surl: warning: --self-signed: serving a throwaway certificate; clients must skip verification (curl -k)`.",
            "surl accepts TLS 1.2 and 1.3 by default. `--tlsv1.0`, `--tlsv1.1`, `--tlsv1.2` and `--tlsv1.3` set the lowest version accepted, and `--tls-max <version>` the highest.",
            "`--cacert <file>` names the CA certificates for client certificates; a file that does not exist exits 2, and one that cannot be read exits 77.",
        ],
    }.ToFrozenDictionary(StringComparer.Ordinal);
}
