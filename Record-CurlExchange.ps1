<#
.SYNOPSIS
    Records one real curl exchange against a canned loopback server, as test fixtures:
    the request bytes curl sent, its stdout, its stderr and its exit code.

.DESCRIPTION
    The request bytes upstream curl sends are the inbound fixtures Surl's tests replay,
    and they are measured from upstream curl first - never taken from the Curl port
    (ADR-0003). This script is how they are measured. It binds 127.0.0.1:<Port> with
    System.Net.Sockets.TcpListener, runs curl with the given arguments, and for each of
    the first <Connections> connections curl opens it:

      1. reads the request until the header block ends (CRLF CRLF) and, when the
         headers carry Content-Length or Transfer-Encoding: chunked, until the body has
         arrived - each read waits at most one second, so a body that never comes
         ends the read instead of hanging it;
      2. records the raw request bytes;
      3. sends that connection's canned response and closes the connection.

    It then writes four files to OutDirectory:

      request.bin   the raw request bytes, every connection's in the order accepted
      stdout.bin    curl's standard output, byte for byte
      stderr.txt    curl's standard error, byte for byte
      exitcode.txt  curl's exit code, as decimal digits with no line ending

    A connection curl never opens is not waited for: once curl exits, the listener is
    stopped. The script exits 0 when the fixtures were written, whatever curl's own
    exit code was; recording a failing curl is as useful as recording a passing one.

.PARAMETER Port
    The loopback TCP port to listen on. The URL in CurlArgs must use the same port.

.PARAMETER Response
    The canned response, as a string with backslash escapes: \r \n \t \0 \\ \" \' and
    \xHH (two hex digits). After decoding, each character becomes one byte (Latin-1),
    so a character above U+00FF is an error. The response is sent exactly as given;
    the script adds nothing. Defaults to an empty 200 with Content-Length: 0.
    Give several, one per connection, to answer a followed redirect's hops
    differently: connection N gets the Nth, and every connection past the last gets
    the last.

.PARAMETER CurlArgs
    The arguments passed to curl, one per element. Each is quoted for the Windows
    command line as needed, so an argument with spaces or quotes reaches curl intact.
    powershell -File hands a comma-separated list to the script as one string, so under
    -File a single CurlArgs string is split at its commas back into the list.
    To pass an argument that itself holds a comma, run the script from PowerShell
    (& or .\) instead. An empty element reaches curl as an empty argument (""), as
    when measuring how an option treats a blank value.

.PARAMETER OutDirectory
    Where the four fixture files are written. Created if missing; existing fixture
    files there are overwritten.

.PARAMETER Connections
    How many connections to serve. Default 1. Use more for a run that makes several
    requests, such as several URLs or a followed redirect.

.PARAMETER ResponseDelayMilliseconds
    How long to wait after reading each request before sending the response. Default 0.
    Use it to make a hop take a known time, as when measuring how -m counts across a
    followed redirect.

.PARAMETER Reset
    Instead of reading a request and answering it, reset each connection as soon as it
    is accepted: the socket closes with a zero linger time, so Windows sends a TCP RST
    rather than a FIN. Use it to measure what curl prints when the server resets the
    connection, as during a TLS handshake. request.bin is then empty.

.PARAMETER HoldOpenMilliseconds
    After sending each response, keep the connection open instead of closing it, until
    curl closes its end or this many milliseconds pass without a byte from curl, and
    record anything curl sends meanwhile. Default 0: close at once. Use it to measure
    what curl does with a response that stops without the peer closing, such as a head
    that never ends. The script always reports how long curl ran.

.PARAMETER RespondAfterBodyBytes
    Instead of reading the whole request, send the response as soon as the header block
    and this many body bytes have arrived, then go on reading (and recording) whatever
    curl still sends until it stops for two seconds or closes its end. Default -1: off.
    Each read then waits up to five seconds, so curl's one-second wait for
    100 Continue does not end the read early. Use it to measure a response that arrives
    while the body is being sent.

.PARAMETER InterimResponse
    An interim response, with the same backslash escapes as Response, sent on each
    connection as soon as the header block has arrived and before any body byte is
    waited for; the body is then read as usual and the canned response sent after it.
    Default empty: none. Use it to measure what curl does with a 100 Continue, e.g.
    -InterimResponse 'HTTP/1.1 100 Continue\r\n\r\n'. Ignored with RespondAfterBodyBytes.

.PARAMETER CloseUnread
    With RespondAfterBodyBytes, close the connection as soon as the response is sent,
    without reading what curl still sends: the body bytes left unread in the receive
    buffer make the close a TCP reset on Windows and Linux. Use it to measure what curl
    does with a server that closes without a lingering close.

.PARAMETER StandardInput
    What curl reads from standard input, with the same backslash escapes as Response.
    It is written in full and then standard input is closed. Default empty: standard
    input is closed at once. Use it to measure an option that reads '-', such as
    -b -.

.PARAMETER Ftp
    Serve one FTP session instead of HTTP responses: send a greeting, then read curl's
    commands one line at a time and answer each from a table of replies, opening a
    passive data connection for EPSV, PASV, RETR, LIST, STOR and APPE. request.bin then holds every
    command line curl sent on the control connection, and transcript.txt holds both
    directions, each line prefixed "> " (curl) or "< " (server). Response,
    Connections, ResponseDelayMilliseconds and Reset are ignored.

    The default replies are: greeting 220, USER 331, PASS 230, PWD 257 "/", EPSV 229
    with the data port, PASV 227 with 127.0.0.1 and the data port, TYPE 200, SIZE 213
    with FtpData's length, MDTM 213 20260927123456, CWD 250, REST 350 (remembering the
    offset), RETR 150 then FtpData from the last REST offset over the data connection
    then 226 (LIST and NLST the same), STOR 150 then every byte curl sends over the data
    connection until it closes it then 226 (APPE the same), QUIT 221 (and the session
    ends), and 502 for any other command. A data connection curl closes early (a range
    read) is not an error. The bytes received on STOR's and APPE's data connections are
    written to upload.bin, and each upload adds one "= <n> bytes received on the data
    connection: <bytes>" line to transcript.txt.

    Active mode and TLS: EPRT and PORT are answered 200 and remember the address
    curl announced, and every later data connection is then dialled to that address instead
    of accepted on the passive listener. AUTH is answered 234 and the control connection is
    then served over TLS with the same throwaway certificate as -Tls (curl needs -k), with a
    "= TLS handshake completed on the control connection" line in transcript.txt; an
    overridden AUTH whose reply starts 234 is upgraded the same way. PBSZ is answered 200,
    PROT 200, and after PROT P (or with -Tls, until a PROT C or a refused PROT) every data
    connection is TLS too, ended with close_notify when the server sends.

.PARAMETER FtpReply
    Overrides for the FTP reply table, each 'VERB=reply' with the same backslash escapes
    as Response, e.g. 'PASS=430 Access denied'. The reply is sent as given with CRLF
    appended. VERB is a command name in capitals, GREETING for the greeting, or RETRDONE
    for the reply sent after RETR's or LIST's data, or STORDONE for the reply sent after
    STOR's or APPE's data. An overridden EPSV, PASV, RETR, LIST, NLST, STOR or APPE sends
    only the reply: no data connection is offered. The reply CLOSE closes the control
    connection instead of answering, e.g. 'PWD=CLOSE'. Several overrides for one VERB are
    answered in the order given, one per command, the last repeating for the rest, e.g.
    'CWD=550 No such directory' 'CWD=250 OK'. In a reply, {DATAPORT} stands for
    the data listener's port and {DATAPORT_HI} and {DATAPORT_LO} for its two PASV numbers,
    e.g. 'PASV=227 Entering Passive Mode (127,0,0,2,{DATAPORT_HI},{DATAPORT_LO})'.

.PARAMETER FtpData
    The file served by RETR, and the listing served by LIST, in -Ftp mode, with the
    same backslash escapes as Response.
    Default empty.

.PARAMETER Smtp
    Serve one SMTP session instead of HTTP responses: send a greeting, then read
    curl's command lines one at a time and answer each from a table of replies, as -Ftp
    does. request.bin then holds every line curl sent, the message body after DATA
    included, and transcript.txt holds both directions, each line prefixed "> " (curl)
    or "< " (server). Response, Connections, ResponseDelayMilliseconds and Reset are
    ignored. With -Tls it serves implicit TLS from the first byte, as smtps:// expects.

    The default replies (RFC 5321) are: greeting 220 localhost ESMTP; EHLO a multiline
    250 advertising AUTH PLAIN LOGIN CRAM-MD5, STARTTLS (left out once the session is
    TLS), SIZE 1000000, 8BITMIME and SMTPUTF8; HELO 250; AUTH 235, after the 334
    continuations the mechanism needs (PLAIN without an initial response one, LOGIN one
    per credential it still lacks, CRAM-MD5 one challenge), each continuation line curl
    sends recorded like a command; MAIL 250; RCPT 250; DATA 354, then every line up to
    and including the lone "." that ends the body, then 250; VRFY 250; EXPN 250; HELP
    214; NOOP 250; RSET 250; QUIT 221 (and the session ends); and 502 for any other
    command. STARTTLS is answered 220 and the session is then served over TLS with the
    same throwaway certificate as -Tls (curl needs -k), with a "= TLS handshake completed
    on the control connection" line in transcript.txt.

.PARAMETER SmtpReply
    Overrides for the SMTP reply table, each 'VERB=reply' with the same backslash escapes
    as Response, e.g. 'RCPT=550 no such user'. The reply is sent as given with CRLF
    appended; a multiline reply is written with \r\n between its lines. VERB is a command
    name in capitals, GREETING for the greeting, or DATADONE for the reply sent after the
    message body. An overridden AUTH sends only the reply, with no continuations. An
    overridden DATA whose reply starts 354 still reads the body, and an overridden
    STARTTLS whose reply starts 220 still switches to TLS. The reply CLOSE and the use of
    several overrides for one VERB work as in FtpReply.

.PARAMETER SmtpIdleMilliseconds
    How long, in -Smtp mode, the server waits for curl's next line before it hangs up.
    Default 5000.

.PARAMETER Imap
    Serve one IMAP4rev1 session instead of HTTP responses: send an untagged
    greeting, then read curl's tagged commands one at a time and answer each with its
    untagged data and a tagged completion that echoes curl's tag (A001 OK ...), from a
    table of replies. A command line ending in a literal {n} is answered "+ Ready for
    literal data" (nothing for a non-synchronizing {n+}) and its n bytes and the rest of
    the command are read before the reply, so an APPEND upload is recorded whole.
    request.bin then holds every byte curl sent, literals included, and transcript.txt
    holds both directions, each line prefixed "> " (curl) or "< " (server). Response,
    Connections, ResponseDelayMilliseconds and Reset are ignored. With -Tls it serves
    implicit TLS from the first byte, as imaps:// expects.

    The default replies (RFC 3501) are: greeting
    * OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready; CAPABILITY the same
    list (STARTTLS left out once the session is TLS); LOGIN OK; AUTHENTICATE OK, after
    the "+" continuations the mechanism needs (PLAIN without an initial response one,
    LOGIN one per credential it still lacks), each continuation line curl sends recorded
    like a command; SELECT and EXAMINE FLAGS, * 2 EXISTS, * 0 RECENT,
    * OK [UIDVALIDITY 1] and * OK [UIDNEXT 3], then OK [READ-WRITE] or [READ-ONLY];
    FETCH and UID FETCH * <n> FETCH (<item> {length}) carrying ImapMessage as a literal,
    for the first message number curl named and echoing the item it asked for; LIST two
    mailboxes, INBOX and Sent; SEARCH and UID SEARCH * SEARCH 1 2; APPEND OK once its
    literal has arrived; NOOP OK; LOGOUT * BYE then OK (and the session ends); and BAD
    for any other command. STARTTLS is answered OK and the session is then served over
    TLS with the same throwaway certificate as -Tls (curl needs -k), with a
    "= TLS handshake completed on the control connection" line in transcript.txt.

.PARAMETER ImapReply
    Overrides for the IMAP reply table, each 'COMMAND=reply' with the same backslash
    escapes as Response, e.g. 'LOGIN=NO denied'. COMMAND is the command name in capitals,
    'UID FETCH' or 'UID SEARCH' for a UID command, or GREETING for the greeting. The reply
    is written without the tag: its last line is sent with curl's tag in front and any
    lines before it, separated by \r\n, are sent as given, so
    'SELECT=* 0 EXISTS\r\nOK [READ-WRITE] done' serves an empty mailbox. The greeting is
    sent exactly as given. An overridden AUTHENTICATE sends only the reply, with no
    continuations; an overridden STARTTLS whose last line starts OK still switches to
    TLS; a literal is always read after its "+" continuation, whatever the reply. The
    reply CLOSE and the use of several overrides for one COMMAND work as in FtpReply.

.PARAMETER ImapMessage
    The message FETCH and UID FETCH serve as a literal in -Imap mode, with the same
    backslash escapes as Response. Default a four-line message ending in CRLF:
    From: sender@example.com, To: recipient@example.com, Subject: Recorded, a blank line
    and "Hello from the recorder.".

.PARAMETER ImapIdleMilliseconds
    How long, in -Imap mode, the server waits for curl's next line before it hangs up.
    Default 5000.

.PARAMETER Pop3
    Serve one POP3 session instead of HTTP responses: send a greeting, then read
    curl's command lines one at a time and answer each from a table of replies, as -Smtp
    does. A multi-line reply ends with a lone "." line and every line of it that starts
    with "." is dot-stuffed (RFC 1939 3). request.bin then holds every line curl sent and
    transcript.txt holds both directions, each line prefixed "> " (curl) or "< " (server),
    multi-line replies as sent, stuffing included. Response, Connections,
    ResponseDelayMilliseconds and Reset are ignored. With -Tls it serves implicit TLS from
    the first byte, as pop3s:// expects.

    The default replies (RFC 1939, RFC 2449, RFC 5034) are: greeting
    +OK POP3 ready <1896.697170952@localhost>, the timestamp APOP digests; CAPA a
    multi-line list of USER, SASL PLAIN LOGIN, STLS (left out once the session is TLS),
    TOP and UIDL; USER +OK; PASS +OK; APOP +OK; AUTH +OK, after the "+" continuations the
    mechanism needs (PLAIN without an initial response one, LOGIN one per credential it
    still lacks), each continuation line curl sends recorded like a command, and AUTH
    without a mechanism a multi-line list of PLAIN and LOGIN; a maildrop of two copies of
    Pop3Message, so STAT +OK 2 <both sizes>, LIST a multi-line listing of both and
    LIST n the one line +OK n <size>; RETR n Pop3Message multi-line; TOP n k its headers,
    the blank line and its first k body lines, multi-line; UIDL a multi-line listing of
    uid-1 and uid-2 and UIDL n one line; DELE +OK; RSET +OK; NOOP +OK; QUIT +OK (and the
    session ends); and -ERR for any other command. STLS is answered +OK and the session is
    then served over TLS with the same throwaway certificate as -Tls (curl needs -k), with
    a "= TLS handshake completed on the control connection" line in transcript.txt.

.PARAMETER Pop3Reply
    Overrides for the POP3 reply table, each 'VERB=reply' with the same backslash escapes
    as Response, e.g. 'PASS=-ERR denied'. VERB is a command name in capitals or GREETING
    for the greeting. The reply is sent exactly as given with CRLF appended, so a
    multi-line reply is written with \r\n between its lines and ends in its own "."
    line, e.g. 'LIST=+OK\r\n.' for an empty maildrop. An overridden AUTH sends only the
    reply, with no continuations, and an overridden STLS whose reply starts +OK still
    switches to TLS. The reply CLOSE and the use of several overrides for one VERB work
    as in FtpReply.

.PARAMETER Pop3Message
    The message RETR and TOP serve, and whose length LIST and STAT report, in -Pop3 mode,
    with the same backslash escapes as Response. It is dot-stuffed as it is sent. Default
    a six-line message ending in CRLF: From: sender@example.com, To:
    recipient@example.com, Subject: Recorded, a blank line, "Hello from the recorder."
    and ".A line that starts with a dot.", which exercises the stuffing.

.PARAMETER Pop3IdleMilliseconds
    How long, in -Pop3 mode, the server waits for curl's next line before it hangs up.
    Default 5000.

.PARAMETER Raw
    Serve one TCP conversation driven by scripted replies instead of HTTP
    responses, for a protocol the script has no mode for, such as DICT (RFC 2229), Gopher
    (RFC 1436), TELNET (RFC 854) or MQTT 3.1.1. The server accepts one connection and
    then, repeatedly, reads what curl sends until curl pauses for RawIdleMilliseconds or
    closes its end, and sends the next RawReply. After the last reply it goes on reading
    until curl closes or goes idle, then closes the connection. With RawReplyFirst the
    first reply is sent before anything is read. A pause in which curl sent nothing
    still counts: the next reply is sent regardless.

    request.bin then holds every byte curl sent, in order, and transcript.txt holds
    both directions, each burst split into lines after each LF and each line prefixed
    "> " (curl) or "< " (server). A line's closing CRLF is not shown; every other byte
    outside printable ASCII is written \xHH, and a backslash as \\. The line
    "= curl closed the connection" marks curl hanging up. Response, Connections,
    ResponseDelayMilliseconds, Reset, HoldOpenMilliseconds and RespondAfterBodyBytes are
    ignored. With -Tls the conversation is TLS 1.2 from the first byte, as gophers://
    or mqtts:// expects, with the same throwaway certificate as -Tls (curl needs -k):
    request.bin and transcript.txt hold the decrypted bytes, transcript.txt opens with
    "= TLS handshake completed", and the server sends a TLS close_notify before it
    closes. A burst then ends when a read of the decrypted stream has not completed
    within RawIdleMilliseconds. Combining it with -Ftp, -Smtp, -Imap, -Pop3, -Tftp or
    -NoServer is refused.

.PARAMETER RawReply
    The replies -Raw sends, one per pause in what curl sends, in order, each with the
    same backslash escapes as Response, \xHH included, so a binary packet such as an
    MQTT CONNACK is given as '\x20\x02\x00\x00'. Each is sent exactly as given; the
    script adds no line ending. Default none: curl is only listened to.

.PARAMETER RawReplyFirst
    With -Raw, send the first RawReply as soon as the connection is accepted, before
    reading anything, for a protocol where the server speaks first, such as DICT's 220
    banner or TELNET's option negotiation.

.PARAMETER RawIdleMilliseconds
    How long, in -Raw mode, a pause in what curl sends must last before the server
    treats the burst as complete and sends the next reply, and, after the last reply,
    before it hangs up. Default 1000.

.PARAMETER Tftp
    Serve one TFTP transfer (RFC 1350) over UDP instead of HTTP responses: bind
    <ListenAddress>:<Port> for UDP, wait for curl's first datagram, and answer it from a
    new UDP port, the server's transfer identifier (RFC 1350, section 4), for the rest
    of the transfer. A read request (RRQ) is answered with TftpData in DATA blocks, each
    sent once curl has acknowledged the one before; a write request (WRQ, curl -T) is
    answered with ACKs, and the DATA curl sends is written to upload.bin. The transfer
    ends after the last block (the first one shorter than the block size) is sent and
    acknowledged or received and acknowledged, or when curl sends an ERROR.

    When curl's request carries options (RFC 2347), the first reply is an OACK accepting
    the ones the server knows: blksize (RFC 2348, capped at 65464; a value below 8 is
    left out), tsize (RFC 2349: TftpData's length for a read, curl's own value echoed
    for a write) and timeout (echoed). A request with no option the server knows gets no
    OACK: a read starts with DATA 1, a write with ACK 0. An unanswered packet is sent
    again after TftpIdleMilliseconds of silence, at most three times, and then the
    server gives up. A first datagram that is neither RRQ nor WRQ is answered with
    ERROR 4. Datagrams curl sends to <Port> after the first, such as a repeated request,
    are recorded and not answered. Mode (octet or netascii) is recorded, not acted on:
    TftpData is sent exactly as given.

    request.bin then holds every datagram curl sent, in order, and transcript.txt holds
    one line per datagram in either direction: "> " for curl's and "< " for the
    server's, then "curl:<port> -> server:<port>" (or the reverse) naming both UDP
    ports, then the datagram's bytes, printable ASCII as is, a backslash as \\ and every
    other byte as \xHH, e.g. "> curl:52001 -> server:18069 \x00\x01file.txt\x00octet\x00".
    Response, Connections, ResponseDelayMilliseconds, Reset, HoldOpenMilliseconds and
    RespondAfterBodyBytes are ignored. Combining it with -Ftp, -Smtp, -Imap, -Pop3, -Raw,
    -Tls or -NoServer is refused.

.PARAMETER TftpData
    The file a read request is served, in -Tftp mode, with the same backslash escapes as
    Response. Default empty: a read gets one empty DATA block.

.PARAMETER TftpReply
    Replaces the server's first reply in -Tftp mode with this datagram, with the same
    backslash escapes as Response, e.g. '\x00\x05\x00\x01File not found\x00' for an
    ERROR 1, or '\x00\x06blksize\x001024\x00' for an OACK of the server's choosing. It
    is sent from the new port exactly as given. When it is an OACK (opcode 6) the
    transfer then goes on as after the server's own OACK, with the block size it names
    (512 when it names none); anything else ends the transfer, and whatever curl sends
    until it stays silent for TftpIdleMilliseconds is recorded. Default empty: the server
    answers as described under Tftp.

.PARAMETER TftpIdleMilliseconds
    How long, in -Tftp mode, the server waits for curl's next datagram before it sends
    its last packet again (or, after three resends, gives up). Default 2000.

.PARAMETER Tls
    Answer each connection over TLS 1.2 instead of plain TCP, so the recorder can stand
    in for an HTTPS server or an HTTPS proxy. The certificate served
    is a throwaway self-signed RSA 2048 certificate for CN=127.0.0.1 (subject
    alternative name IP 127.0.0.1), made for this run and valid for one day; curl needs
    -k (or --proxy-insecure for a proxy) to accept it. request.bin, and the Response
    matching, hold the decrypted bytes, not the TLS records. The certificate is never
    added to a certificate store, and its key container is deleted when the run ends.
    A connection whose handshake fails is recorded as empty. -Reset resets the connection
    before any handshake. With -Ftp it serves implicit FTPS: the control connection is TLS
    from its first byte, as ftps:// expects; with -Smtp, implicit SMTPS, as
    smtps:// expects; with -Imap, implicit IMAPS, as imaps:// expects;
    with -Pop3, implicit POP3S, as pop3s:// expects; with -Raw, the raw conversation
    over TLS from its first byte, as gophers:// expects. A -Raw connection whose
    handshake fails is recorded as empty too.

.PARAMETER TlsRootCertificateFile
    With -Tls, serve a certificate issued by a throwaway private root CA in place of the
    self-signed one, and write that root's PEM to this path so curl can be given
    --cacert <path>. Neither certificate names a revocation endpoint, so the
    Schannel build's revocation check of the leaf ends "status unknown" (observed while
    building the Curl port; re-measure before a Surl test relies on it). The root's key is never written; the file is left for the caller to delete.

.PARAMETER TlsProtocol
    With -Tls and no session mode, the TLS versions the server accepts: Tls12 (the
    default), Tls13, or Tls12AndTls13. Use it to measure which versions curl negotiates
    or refuses. The -Ftp, -Smtp, -Imap, -Pop3 and -Raw sessions always serve TLS 1.2.

.PARAMETER TlsRenegotiationOff
    With -Tls and no session mode, serve TLS with renegotiation refused
    (SslServerAuthenticationOptions.AllowRenegotiation = false), as Surl does (ADR-0006,
    section 4). Windows PowerShell's .NET Framework SslStream cannot refuse it, so the
    handshake is run by a small TLS relay, a C# file-based app this script writes under
    %TEMP%\SurlRecorder and starts with `dotnet run` (it needs the .NET 10 SDK): the relay
    listens on ListenAddress:Port, shakes hands with the same throwaway certificate and
    -TlsProtocol versions, and forwards the plaintext to the recorder's own server on an
    ephemeral loopback port, so request.bin and the Response matching work as with -Tls.
    A connection whose handshake fails is never forwarded, so it records nothing. Not
    combined with -Reset.

.PARAMETER FtpIdleMilliseconds
    How long, in -Ftp mode, the server waits for curl's next command before it hangs up.
    Default 5000. Raise it to measure a wait longer than five seconds, such as curl's
    60-second accept timeout when an active-mode data connection never arrives.

.PARAMETER Curl
    The upstream curl executable to run. Defaults to curl 8.21.0 from Git for Windows'
    mingw64 directory, found beside git.exe. Whatever the path, the file's SHA-256 must
    match a build pinned in UpstreamCurlBuilds.json or the script refuses to run it
    (ADR-0003): the Curl port reports upstream's own version number, so only the file
    itself can tell the two apart.

.PARAMETER ListenAddress
    The address the server (and, with -Ftp, its passive data listener, named in PASV's
    227 reply) binds. Default 127.0.0.1. Surl refuses -Curl wsl.exe, which the
    Curl port's copy of this script uses to reach the Linux build: only a build pinned in
    UpstreamCurlBuilds.json may run (ADR-0003), and a curl inside WSL cannot be
    checksummed from Windows.

.PARAMETER NoServer
    Bind no port and serve nothing: run curl against a server the caller started, such
    as a local OpenSSH sshd, an LDAP server or an SMB share, which a PowerShell loopback
    server cannot speak. Only stdout.bin, stderr.txt and exitcode.txt are
    written; there is no request.bin, since the script sees none of the traffic. Port,
    Response, Connections, ResponseDelayMilliseconds, Reset, HoldOpenMilliseconds,
    RespondAfterBodyBytes, FtpReply, FtpData, FtpIdleMilliseconds, SmtpReply,
    SmtpIdleMilliseconds, ImapReply, ImapMessage, ImapIdleMilliseconds, Pop3Reply,
    Pop3Message, Pop3IdleMilliseconds, TftpData, TftpReply, TftpIdleMilliseconds and
    ListenAddress are ignored, and Port need not be given. Combining it with a server
    mode, -Ftp, -Smtp, -Imap, -Pop3, -Raw, -Tftp or -Tls, is refused. StandardInput and Curl work as in every other mode.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18081 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -CurlArgs 'http://127.0.0.1:18081/a?b' -OutDirectory fixtures\default-get

    request.bin then holds
    GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n
    and stdout.bin holds hello.

.EXAMPLE
    .\Record-CurlExchange.ps1 -NoServer -CurlArgs '-sS','-k','sftp://tester:secret@127.0.0.1:2222/home/tester/a.txt' -OutDirectory fixtures\sftp-get

    Runs curl against an sshd the caller already started on port 2222 and records only
    what curl printed and its exit code.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18025 -Smtp -SmtpReply 'RCPT=550 no such user' -CurlArgs '-sS','--mail-from','a@b','--mail-rcpt','c@d','-T','mail.txt','smtp://127.0.0.1:18025/' -OutDirectory fixtures\smtp-rcpt-refused

    Serves one SMTP session that refuses the recipient; transcript.txt shows EHLO,
    MAIL FROM, RCPT TO and the 550, and request.bin the lines curl sent.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18143 -Imap -CurlArgs '-sS','-u','u:p','imap://127.0.0.1:18143/INBOX;UID=1' -OutDirectory fixtures\imap-fetch

    Serves one IMAP session; transcript.txt shows CAPABILITY, AUTHENTICATE PLAIN, SELECT
    INBOX, UID FETCH 1 BODY[] and LOGOUT with their tagged replies, and stdout.bin the
    message curl printed.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18110 -Pop3 -CurlArgs '-sS','-u','u:p','pop3://127.0.0.1:18110/1' -OutDirectory fixtures\pop3-retr

    Serves one POP3 session; transcript.txt shows CAPA, AUTH PLAIN, RETR 1 with the
    dot-stuffed message and QUIT, and stdout.bin the message curl printed.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18628 -Raw -RawReplyFirst -RawReply '220 dict.example ready <auth.mime> <1.2@dict.example>\r\n','150 1 definitions retrieved\r\n151 "hello" test "Test dictionary"\r\nA greeting.\r\n.\r\n250 ok\r\n','221 bye\r\n' -CurlArgs '-sS','dict://127.0.0.1:18628/d:hello' -OutDirectory fixtures\dict-define

    Sends a DICT banner, then answers the burst curl sends (CLIENT libcurl 8.21.0,
    DEFINE ! hello and QUIT, all at once) with a definition and, after the next pause,
    221. request.bin holds curl's three lines and transcript.txt both directions.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18069 -Tftp -TftpData 'hello\n' -CurlArgs '-sS','tftp://127.0.0.1:18069/file.txt' -OutDirectory fixtures\tftp-read

    Serves one TFTP read; transcript.txt shows curl's RRQ to port 18069, the server's
    replies from a port of its own and curl's ACKs, and stdout.bin holds hello.
#>
[CmdletBinding()]
param(
    [ValidateRange(0, 65535)] [int] $Port = 0,
    [string[]] $Response = @('HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n'),
    [Parameter(Mandatory = $true)] [AllowEmptyString()] [string[]] $CurlArgs,
    [Parameter(Mandatory = $true)] [string] $OutDirectory,
    [ValidateRange(1, 1000)] [int] $Connections = 1,
    [ValidateRange(0, 600000)] [int] $ResponseDelayMilliseconds = 0,
    [switch] $Reset,
    [ValidateRange(0, 600000)] [int] $HoldOpenMilliseconds = 0,
    [ValidateRange(-1, [int]::MaxValue)] [int] $RespondAfterBodyBytes = -1,
    [switch] $CloseUnread,
    [string] $InterimResponse = '',
    [string] $StandardInput = '',
    [switch] $Ftp,
    [string[]] $FtpReply = @(),
    [string] $FtpData = '',
    [ValidateRange(1, 600000)] [int] $FtpIdleMilliseconds = 5000,
    [switch] $Smtp,
    [string[]] $SmtpReply = @(),
    [ValidateRange(1, 600000)] [int] $SmtpIdleMilliseconds = 5000,
    [switch] $Imap,
    [string[]] $ImapReply = @(),
    [string] $ImapMessage = 'From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n',
    [ValidateRange(1, 600000)] [int] $ImapIdleMilliseconds = 5000,
    [switch] $Pop3,
    [string[]] $Pop3Reply = @(),
    [string] $Pop3Message = 'From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n.A line that starts with a dot.\r\n',
    [ValidateRange(1, 600000)] [int] $Pop3IdleMilliseconds = 5000,
    [switch] $Raw,
    [string[]] $RawReply = @(),
    [switch] $RawReplyFirst,
    [ValidateRange(1, 600000)] [int] $RawIdleMilliseconds = 1000,
    [switch] $Tftp,
    [string] $TftpData = '',
    [string] $TftpReply = '',
    [ValidateRange(1, 600000)] [int] $TftpIdleMilliseconds = 2000,
    [switch] $Tls,
    [string] $TlsRootCertificateFile,
    [ValidateSet('Tls12', 'Tls13', 'Tls12AndTls13')] [string] $TlsProtocol = 'Tls12',
    [switch] $TlsRenegotiationOff,
    [string] $Curl,
    [System.Net.IPAddress] $ListenAddress = [System.Net.IPAddress]::Loopback,
    [switch] $NoServer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# powershell -File binds '-sS','http://...' as the one string "-sS,http://..."; only then
# is the invocation line empty, so only then is that string split back into its elements.
if ([string]::IsNullOrEmpty($MyInvocation.Line) -and $CurlArgs.Count -eq 1) { $CurlArgs = $CurlArgs[0].Split(',') }
if ($NoServer -and ($Ftp -or $Smtp -or $Imap -or $Pop3 -or $Raw -or $Tftp -or $Tls)) { throw '-NoServer runs no server, so it cannot be combined with -Ftp, -Smtp, -Imap, -Pop3, -Raw, -Tftp or -Tls.' }
if (@($Ftp, $Smtp, $Imap, $Pop3, $Raw, $Tftp | Where-Object { $_ }).Count -gt 1) { throw '-Ftp, -Smtp, -Imap, -Pop3, -Raw and -Tftp each serve a whole session; give one of them.' }
if ($Tftp -and $Tls) { throw '-Tftp serves plain UDP, so it cannot be combined with -Tls.' }
if ($TlsRenegotiationOff -and (-not $Tls -or $Ftp -or $Smtp -or $Imap -or $Pop3 -or $Raw -or $Reset)) { throw '-TlsRenegotiationOff needs -Tls with no session mode, and cannot be combined with -Reset.' }
if (-not $NoServer -and $Port -eq 0) { throw '-Port is required unless -NoServer is given: the URL in CurlArgs must name the port the server listens on.' }

function Get-ReferenceCurlPath {
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($null -ne $git) {
        # git.exe lives in <Git>\cmd or <Git>\bin; the reference curl in <Git>\mingw64\bin.
        $gitRoot = Split-Path (Split-Path $git.Source -Parent) -Parent
        $candidate = Join-Path $gitRoot 'mingw64\bin\curl.exe'
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    $candidate = Join-Path $env:ProgramFiles 'Git\mingw64\bin\curl.exe'
    if (Test-Path -LiteralPath $candidate) { return $candidate }
    throw 'The reference curl (Git for Windows mingw64\bin\curl.exe) was not found; pass -Curl.'
}

# Surl is measured against upstream curl and nothing else (ADR-0003). The Curl port reports
# upstream's own version number, so neither a name nor --version can tell the two apart;
# only the file can. UpstreamCurlBuilds.json pins every build by SHA-256.
function Assert-PinnedUpstreamCurl {
    param([string] $Path)

    $command = Get-Command -Name $Path -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $command) { throw "The curl to run, $Path, was not found." }
    $pins = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'UpstreamCurlBuilds.json')) | ConvertFrom-Json
    $hash = (Get-FileHash -LiteralPath $command.Source -Algorithm SHA256).Hash
    $pinned = @($pins.builds | Where-Object { $_.sha256 -eq $hash })
    if ($pinned.Count -eq 0) {
        throw "$($command.Source) (SHA-256 $hash) is not a pinned upstream curl build. Surl is measured only against the builds in UpstreamCurlBuilds.json (ADR-0003); pinning another is a decision, and the Curl port is never one."
    }
}

function ConvertFrom-EscapedResponse {
    param([string] $Text)
    $bytes = New-Object System.Collections.Generic.List[byte]
    $index = 0
    while ($index -lt $Text.Length) {
        $character = $Text[$index]
        if ($character -ne '\') {
            if ([int] $character -gt 255) { throw "Response character U+$(([int] $character).ToString('X4')) is not Latin-1." }
            $bytes.Add([byte] [int] $character)
            $index++
            continue
        }
        if ($index + 1 -ge $Text.Length) { throw 'Response ends with a lone backslash.' }
        $escape = $Text[$index + 1]
        switch -CaseSensitive ([string] $escape) {
            'r' { $bytes.Add(13); $index += 2 }
            'n' { $bytes.Add(10); $index += 2 }
            't' { $bytes.Add(9); $index += 2 }
            '0' { $bytes.Add(0); $index += 2 }
            '\' { $bytes.Add(92); $index += 2 }
            '"' { $bytes.Add(34); $index += 2 }
            "'" { $bytes.Add(39); $index += 2 }
            'x' {
                $hex = if ($index + 4 -le $Text.Length) { $Text.Substring($index + 2, 2) } else { '' }
                if ($hex -notmatch '^[0-9A-Fa-f]{2}$') { throw "Response has a malformed \x escape at index $index." }
                $bytes.Add([Convert]::ToByte($hex, 16))
                $index += 4
            }
            default { throw "Response has an unknown escape \$escape at index $index." }
        }
    }
    return , $bytes.ToArray()
}

function ConvertTo-CommandLineArgument {
    # Quotes one argument so CommandLineToArgvW (and the C runtime) reads it back unchanged.
    param([string] $Argument)
    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') { return $Argument }
    $builder = New-Object System.Text.StringBuilder
    [void] $builder.Append('"')
    $backslashes = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq '\') { $backslashes++; continue }
        if ($character -eq '"') {
            [void] $builder.Append('\', 2 * $backslashes + 1)
        } elseif ($backslashes -gt 0) {
            [void] $builder.Append('\', $backslashes)
        }
        $backslashes = 0
        [void] $builder.Append($character)
    }
    if ($backslashes -gt 0) { [void] $builder.Append('\', 2 * $backslashes) }
    [void] $builder.Append('"')
    return $builder.ToString()
}

# The server runs in its own runspace so curl can run in this one. It returns one
# byte array per connection served.
$serveConnections = {
    param($Listener, $ResponseBytes, [int] $ConnectionCount, [int] $DelayMilliseconds, [bool] $ResetConnections, [int] $EarlyResponseBodyBytes, $TlsCertificate, [int] $HoldOpen, [int] $ServedTlsProtocols, [bool] $CloseEarly, [byte[]] $InterimBytes)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)

    function Test-RequestComplete {
        param([byte[]] $Received, [int] $Length)
        $text = $latin1.GetString($Received, 0, $Length)
        $headerEnd = $text.IndexOf("`r`n`r`n")
        if ($headerEnd -lt 0) { return $false }
        $bodyStart = $headerEnd + 4
        if ($EarlyResponseBodyBytes -ge 0) { return ($Length - $bodyStart) -ge $EarlyResponseBodyBytes }
        $headers = $text.Substring(0, $headerEnd)
        $contentLength = [regex]::Match($headers, '(?im)^Content-Length:[ \t]*(\d+)[ \t]*\r?$')
        if ($contentLength.Success) {
            return ($Length - $bodyStart) -ge [long] $contentLength.Groups[1].Value
        }
        if ($headers -match '(?im)^Transfer-Encoding:.*\bchunked\b') {
            $body = $text.Substring($bodyStart)
            return $body.StartsWith("0`r`n`r`n") -or $body.EndsWith("`r`n0`r`n`r`n")
        }
        return $true
    }

    $requests = New-Object System.Collections.Generic.List[object]
    for ($served = 0; $served -lt $ConnectionCount; $served++) {
        try {
            $client = $Listener.AcceptTcpClient()
        } catch {
            break  # The listener was stopped: curl exited without opening this connection.
        }
        if ($ResetConnections) {
            # A zero linger time makes Close send RST instead of FIN.
            $client.LingerState = New-Object System.Net.Sockets.LingerOption($true, 0)
            $client.Close()
            $requests.Add([byte[]] @())
            continue
        }
        try {
            $stream = $client.GetStream()
            if ($null -ne $TlsCertificate) {
                $stream = New-Object System.Net.Security.SslStream($stream, $false)
                $stream.ReadTimeout = 5000
                try {
                    $stream.AuthenticateAsServer($TlsCertificate, $false, [System.Security.Authentication.SslProtocols] $ServedTlsProtocols, $false)
                } catch {
                    # curl refused the certificate or hung up mid-handshake: nothing was sent.
                    $requests.Add([byte[]] @())
                    continue
                }
            }
            $stream.ReadTimeout = if ($EarlyResponseBodyBytes -ge 0) { 5000 } else { 1000 }
            $buffer = New-Object byte[] 65536
            $received = New-Object System.IO.MemoryStream
            $interimSent = $null -eq $InterimBytes -or $InterimBytes.Length -eq 0 -or $EarlyResponseBodyBytes -ge 0
            while ($true) {
                if (-not $interimSent -and $latin1.GetString($received.GetBuffer(), 0, [int] $received.Length).Contains("`r`n`r`n")) {
                    # The head has arrived: the interim response goes out before the body is waited for.
                    $stream.Write($InterimBytes, 0, $InterimBytes.Length)
                    $stream.Flush()
                    $interimSent = $true
                }
                if (Test-RequestComplete -Received $received.GetBuffer() -Length ([int] $received.Length)) { break }
                try {
                    $count = $stream.Read($buffer, 0, $buffer.Length)
                } catch [System.IO.IOException] {
                    break  # One second without a byte: take what arrived.
                }
                if ($count -le 0) { break }
                $received.Write($buffer, 0, $count)
            }
            if ($DelayMilliseconds -gt 0) { [System.Threading.Thread]::Sleep($DelayMilliseconds) }
            [byte[]] $response = $ResponseBytes[[Math]::Min($served, $ResponseBytes.Count - 1)]
            try {
                $stream.Write($response, 0, $response.Length)
                $stream.Flush()
            } catch [System.IO.IOException] {
                # curl already closed its end; the request is still worth recording.
            }
            if ($EarlyResponseBodyBytes -ge 0 -and -not $CloseEarly) {
                # Answered mid-body: record whatever curl goes on sending until it stops.
                $stream.ReadTimeout = 2000
                while ($true) {
                    try {
                        $count = $stream.Read($buffer, 0, $buffer.Length)
                    } catch [System.IO.IOException] {
                        break
                    }
                    if ($count -le 0) { break }
                    $received.Write($buffer, 0, $count)
                }
            }
            if ($HoldOpen -gt 0) {
                # Held open: wait for curl to hang up, recording what it still sends.
                $stream.ReadTimeout = $HoldOpen
                while ($true) {
                    try {
                        $count = $stream.Read($buffer, 0, $buffer.Length)
                    } catch [System.IO.IOException] {
                        break
                    }
                    if ($count -le 0) { break }
                    $received.Write($buffer, 0, $count)
                }
            }
            $requests.Add($received.ToArray())
        } finally {
            $client.Close()
        }
    }
    return , $requests.ToArray()
}

# Helpers the line-at-a-time sessions (-Ftp, -Smtp, -Imap and -Pop3) dot-source into their runspace.
# They pass as text, since a script block invoked in another runspace would run back in
# this one, which is busy waiting for curl. They read $TlsCertificate and $Overrides
# from the session that dot-sources them.
$sessionHelpers = {
    # Serves TLS 1.2 over $Stream with the throwaway certificate, as the server side.
    function Wrap-Tls {
        param($Stream)
        $secure = New-Object System.Net.Security.SslStream($Stream, $false)
        $secure.AuthenticateAsServer($TlsCertificate, $false, [System.Security.Authentication.SslProtocols]::Tls12, $false)
        return $secure
    }

    # Several overrides for one verb are used in turn; the last one repeats.
    function Get-Override {
        param([string] $Verb)
        $replies = $Overrides[$Verb]
        if ($replies.Count -gt 1) {
            $reply = $replies[0]
            $replies.RemoveAt(0)
            return $reply
        }
        return $replies[0]
    }
}

# The -Ftp server: one control connection, answered a line at a time, with a passive
# data listener for RETR, LIST, STOR and APPE. It returns the control bytes curl sent, as
# one array, writes the two-way transcript into $Transcript, and the bytes uploaded on
# STOR and APPE data connections into $UploadedData.
$serveFtpSession = {
    param($Listener, [hashtable] $Overrides, [byte[]] $DataBytes, [System.Text.StringBuilder] $Transcript, [System.IO.MemoryStream] $UploadedData, $TlsCertificate, [bool] $ImplicitTls, [int] $ControlIdleMilliseconds, [System.Net.IPAddress] $ListenAddress, [string] $SessionHelpers)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    . ([scriptblock]::Create($SessionHelpers))
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $received = New-Object System.IO.MemoryStream
    $dataListener = New-Object System.Net.Sockets.TcpListener($ListenAddress, 0)
    $dataListener.Start()
    $dataPort = ([System.Net.IPEndPoint] $dataListener.LocalEndpoint).Port
    $restOffset = [long] 0
    # Set by EPRT or PORT: the address curl listens on, which the data connection then
    # dials instead of waiting on the passive listener.
    $activeEndPoint = $null
    # Set by PROT P, and by implicit TLS until a PROT C: data connections are TLS.
    $protectData = $ImplicitTls

    # Opens the data connection: dials curl's EPRT/PORT address in active mode, otherwise
    # accepts on the passive listener; either way wrapped in TLS once PROT P was accepted.
    function Open-DataConnection {
        param([string] $Verb, $ActiveEndPoint, [bool] $Protect)
        if ($null -ne $ActiveEndPoint) {
            $dataClient = New-Object System.Net.Sockets.TcpClient($ActiveEndPoint.AddressFamily)
            $dataClient.Connect($ActiveEndPoint)
        } else {
            $accept = $dataListener.AcceptTcpClientAsync()
            if (-not $accept.Wait(5000)) { throw "curl sent $Verb but opened no data connection within five seconds." }
            $dataClient = $accept.Result
        }
        $dataStream = $dataClient.GetStream()
        if ($Protect) { $dataStream = Wrap-Tls -Stream $dataStream }
        return @($dataClient, $dataStream)
    }

    # EPRT |1|127.0.0.1|5000| or PORT 127,0,0,1,19,136, as an IPEndPoint.
    function ConvertFrom-ActiveCommand {
        param([string] $Verb, [string] $Argument)
        if ($Verb -eq 'EPRT') {
            $fields = $Argument.Split($Argument[0])
            return New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Parse($fields[2]), [int] $fields[3])
        }
        $numbers = $Argument.Split(',')
        $activePort = [int] $numbers[4] * 256 + [int] $numbers[5]
        return New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Parse(($numbers[0..3] -join '.')), $activePort)
    }

    function Send-Reply {
        param($Stream, [string] $Reply)
        $Reply = $Reply.Replace('{DATAPORT_HI}', [string] [Math]::Floor($dataPort / 256)).Replace('{DATAPORT_LO}', [string] ($dataPort % 256)).Replace('{DATAPORT}', [string] $dataPort)
        $bytes = $latin1.GetBytes($Reply + "`r`n")
        $Stream.Write($bytes, 0, $bytes.Length)
        $Stream.Flush()
        foreach ($line in ($Reply -split "`r`n")) { [void] $Transcript.Append("< $line`r`n") }
    }

    try {
        try {
            $client = $Listener.AcceptTcpClient()
        } catch {
            return , @(, $received.ToArray())
        }
        try {
            $stream = $client.GetStream()
            if ($ImplicitTls) { $stream = Wrap-Tls -Stream $stream }
            $stream.ReadTimeout = $ControlIdleMilliseconds
            $greeting = if ($Overrides.ContainsKey('GREETING')) { Get-Override -Verb 'GREETING' } else { '220 Recorder ready' }
            Send-Reply -Stream $stream -Reply $greeting
            $line = New-Object System.IO.MemoryStream
            while ($true) {
                try {
                    $next = $stream.ReadByte()
                } catch [System.IO.IOException] {
                    break  # FtpIdleMilliseconds without a byte: curl is done with us.
                }
                if ($next -lt 0) { break }
                $received.WriteByte([byte] $next)
                $line.WriteByte([byte] $next)
                if ($next -ne 10) { continue }
                $command = $latin1.GetString($line.ToArray()).TrimEnd("`r", "`n")
                $line.SetLength(0)
                [void] $Transcript.Append("> $command`r`n")
                $verb = ($command -split ' ', 2)[0].ToUpperInvariant()
                $argument = if ($command.Contains(' ')) { ($command -split ' ', 2)[1] } else { '' }
                if ($Overrides.ContainsKey($verb)) {
                    $override = Get-Override -Verb $verb
                    if ($override -ceq 'CLOSE') { break }  # Hang up instead of replying.
                    Send-Reply -Stream $stream -Reply $override
                    if ($verb -eq 'QUIT') { break }
                    # A refused PROT leaves the data connections in plaintext.
                    if ($verb -eq 'PROT') { $protectData = $override.StartsWith('2') -and $argument -ceq 'P' }
                    if ($verb -eq 'AUTH' -and $override.StartsWith('234')) {
                        $stream = Wrap-Tls -Stream $stream
                        $stream.ReadTimeout = $ControlIdleMilliseconds
                        [void] $Transcript.Append("= TLS handshake completed on the control connection`r`n")
                    }
                    continue
                }
                switch ($verb) {
                    'AUTH' {
                        Send-Reply -Stream $stream -Reply '234 AUTH accepted'
                        $stream = Wrap-Tls -Stream $stream
                        $stream.ReadTimeout = $ControlIdleMilliseconds
                        [void] $Transcript.Append("= TLS handshake completed on the control connection`r`n")
                    }
                    'PBSZ' { Send-Reply -Stream $stream -Reply '200 PBSZ=0' }
                    'PROT' {
                        $protectData = $argument -ceq 'P'
                        Send-Reply -Stream $stream -Reply "200 Protection level set to $argument"
                    }
                    { $_ -eq 'EPRT' -or $_ -eq 'PORT' } {
                        $activeEndPoint = ConvertFrom-ActiveCommand -Verb $verb -Argument $argument
                        Send-Reply -Stream $stream -Reply "200 $verb command successful"
                    }
                    'USER' { Send-Reply -Stream $stream -Reply '331 Password required' }
                    'PASS' { Send-Reply -Stream $stream -Reply '230 Logged in' }
                    'PWD' { Send-Reply -Stream $stream -Reply '257 "/" is current directory' }
                    'CWD' { Send-Reply -Stream $stream -Reply '250 OK' }
                    'TYPE' { Send-Reply -Stream $stream -Reply '200 Type set' }
                    'SIZE' { Send-Reply -Stream $stream -Reply "213 $($DataBytes.Length)" }
                    'MDTM' { Send-Reply -Stream $stream -Reply '213 20260927123456' }
                    'REST' {
                        $restOffset = [long] ($command -split ' ', 2)[1]
                        Send-Reply -Stream $stream -Reply "350 Restarting at $restOffset"
                    }
                    'EPSV' { Send-Reply -Stream $stream -Reply "229 Entering Extended Passive Mode (|||$dataPort|)" }
                    'PASV' { Send-Reply -Stream $stream -Reply "227 Entering Passive Mode ($($ListenAddress.ToString().Replace('.', ',')),$([Math]::Floor($dataPort / 256)),$($dataPort % 256))" }
                    { $_ -eq 'RETR' -or $_ -eq 'LIST' -or $_ -eq 'NLST' } {
                        Send-Reply -Stream $stream -Reply '150 Opening BINARY mode data connection'
                        $dataClient, $dataStream = Open-DataConnection -Verb $verb -ActiveEndPoint $activeEndPoint -Protect $protectData
                        try {
                            $start = [int] [Math]::Max(0, [Math]::Min($restOffset, $DataBytes.Length))
                            $dataStream.Write($DataBytes, $start, $DataBytes.Length - $start)
                            $dataStream.Flush()
                            # A TLS data connection ends with close_notify, so curl reads a clean end.
                            if ($dataStream -is [System.Net.Security.SslStream]) { $dataStream.ShutdownAsync().Wait() }
                        } catch [System.IO.IOException] {
                            # curl closed the data connection early, as it does once a range is read.
                        } finally {
                            $dataClient.Close()
                        }
                        $done = if ($Overrides.ContainsKey('RETRDONE')) { Get-Override -Verb 'RETRDONE' } else { '226 Transfer complete' }
                        Send-Reply -Stream $stream -Reply $done
                    }
                    { $_ -eq 'STOR' -or $_ -eq 'APPE' } {
                        Send-Reply -Stream $stream -Reply '150 Opening BINARY mode data connection'
                        $dataClient, $dataStream = Open-DataConnection -Verb $verb -ActiveEndPoint $activeEndPoint -Protect $protectData
                        $uploaded = New-Object System.IO.MemoryStream
                        try {
                            $dataStream.ReadTimeout = 5000
                            $dataStream.CopyTo($uploaded)
                        } catch [System.IO.IOException] {
                            # Five seconds without a byte, or a reset: keep what arrived.
                        } finally {
                            $dataClient.Close()
                        }
                        [byte[]] $uploadedBytes = $uploaded.ToArray()
                        $UploadedData.Write($uploadedBytes, 0, $uploadedBytes.Length)
                        [void] $Transcript.Append("= $($uploadedBytes.Length) bytes received on the data connection: $($latin1.GetString($uploadedBytes))`r`n")
                        $done = if ($Overrides.ContainsKey('STORDONE')) { Get-Override -Verb 'STORDONE' } else { '226 Transfer complete' }
                        Send-Reply -Stream $stream -Reply $done
                    }
                    'QUIT' { Send-Reply -Stream $stream -Reply '221 Bye'; break }
                    default { Send-Reply -Stream $stream -Reply '502 Command not implemented' }
                }
                if ($verb -eq 'QUIT') { break }
            }
        } catch [System.IO.IOException] {
            # curl closed the control connection mid-reply; what arrived is still recorded.
        } finally {
            $client.Close()
        }
    } finally {
        $dataListener.Stop()
    }
    return , @(, $received.ToArray())
}

# The -Smtp server: one connection, answered a line at a time, reading the message body
# after DATA up to its lone "." line. It returns every byte curl sent, as one array, and
# writes the two-way transcript into $Transcript.
$serveSmtpSession = {
    param($Listener, [hashtable] $Overrides, [System.Text.StringBuilder] $Transcript, $TlsCertificate, [bool] $ImplicitTls, [int] $IdleMilliseconds, [string] $SessionHelpers)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    . ([scriptblock]::Create($SessionHelpers))
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $received = New-Object System.IO.MemoryStream

    function Send-Reply {
        param($Stream, [string] $Reply)
        $bytes = $latin1.GetBytes($Reply + "`r`n")
        $Stream.Write($bytes, 0, $bytes.Length)
        $Stream.Flush()
        foreach ($line in ($Reply -split "`r`n")) { [void] $Transcript.Append("< $line`r`n") }
    }

    # One line from curl, recorded, without its line ending; $null once curl hangs up or
    # stays silent for IdleMilliseconds.
    function Read-Line {
        param($Stream)
        $line = New-Object System.IO.MemoryStream
        while ($true) {
            try {
                $next = $Stream.ReadByte()
            } catch [System.IO.IOException] {
                return $null
            }
            if ($next -lt 0) { return $null }
            $received.WriteByte([byte] $next)
            $line.WriteByte([byte] $next)
            if ($next -eq 10) { break }
        }
        $text = $latin1.GetString($line.ToArray()).TrimEnd("`r", "`n")
        [void] $Transcript.Append("> $text`r`n")
        return $text
    }

    function Get-EhloReply {
        param([bool] $Secure)
        $capabilities = @('250-localhost', '250-AUTH PLAIN LOGIN CRAM-MD5')
        # RFC 3207: STARTTLS is not offered again once the session is TLS.
        if (-not $Secure) { $capabilities += '250-STARTTLS' }
        $capabilities += @('250-SIZE 1000000', '250-8BITMIME', '250 SMTPUTF8')
        return $capabilities -join "`r`n"
    }

    # The 334 continuations each mechanism needs before 235; $false once curl hangs up.
    function Complete-Authentication {
        param($Stream, [string] $Argument)
        $mechanism = ($Argument -split ' ', 2)[0].ToUpperInvariant()
        $hasInitialResponse = $Argument.Contains(' ')
        $challenges = switch ($mechanism) {
            'PLAIN' { if ($hasInitialResponse) { @() } else { @('334 ') } }
            'LOGIN' { if ($hasInitialResponse) { @('334 UGFzc3dvcmQ6') } else { @('334 VXNlcm5hbWU6', '334 UGFzc3dvcmQ6') } }
            'CRAM-MD5' { @('334 ' + [Convert]::ToBase64String($latin1.GetBytes('<1896.697170952@localhost>'))) }
            default { @() }
        }
        foreach ($challenge in $challenges) {
            Send-Reply -Stream $Stream -Reply $challenge
            if ($null -eq (Read-Line -Stream $Stream)) { return $false }
        }
        Send-Reply -Stream $Stream -Reply '235 Authentication successful'
        return $true
    }

    # The body after DATA's 354, up to and including the lone "." line, then the reply.
    function Receive-MessageBody {
        param($Stream)
        while ($true) {
            $bodyLine = Read-Line -Stream $Stream
            if ($null -eq $bodyLine) { return $false }
            if ($bodyLine -ceq '.') { break }
        }
        $done = if ($Overrides.ContainsKey('DATADONE')) { Get-Override -Verb 'DATADONE' } else { '250 OK message accepted' }
        Send-Reply -Stream $Stream -Reply $done
        return $true
    }

    function Start-Tls {
        param($Stream)
        $secure = Wrap-Tls -Stream $Stream
        $secure.ReadTimeout = $IdleMilliseconds
        [void] $Transcript.Append("= TLS handshake completed on the control connection`r`n")
        return $secure
    }

    try {
        $client = $Listener.AcceptTcpClient()
    } catch {
        return , @(, $received.ToArray())
    }
    try {
        $stream = $client.GetStream()
        $secure = $ImplicitTls
        if ($ImplicitTls) { $stream = Wrap-Tls -Stream $stream }
        $stream.ReadTimeout = $IdleMilliseconds
        $greeting = if ($Overrides.ContainsKey('GREETING')) { Get-Override -Verb 'GREETING' } else { '220 localhost ESMTP' }
        Send-Reply -Stream $stream -Reply $greeting
        while ($true) {
            $command = Read-Line -Stream $stream
            if ($null -eq $command) { break }  # SmtpIdleMilliseconds without a byte, or curl hung up.
            $verb = ($command -split ' ', 2)[0].ToUpperInvariant()
            $argument = if ($command.Contains(' ')) { ($command -split ' ', 2)[1] } else { '' }
            if ($Overrides.ContainsKey($verb)) {
                $override = Get-Override -Verb $verb
                if ($override -ceq 'CLOSE') { break }  # Hang up instead of replying.
                Send-Reply -Stream $stream -Reply $override
                if ($verb -eq 'QUIT') { break }
                if ($verb -eq 'DATA' -and $override.StartsWith('354') -and -not (Receive-MessageBody -Stream $stream)) { break }
                if ($verb -eq 'STARTTLS' -and $override.StartsWith('220')) {
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                continue
            }
            $continue = $true
            switch ($verb) {
                'EHLO' { Send-Reply -Stream $stream -Reply (Get-EhloReply -Secure $secure) }
                'HELO' { Send-Reply -Stream $stream -Reply '250 localhost' }
                'STARTTLS' {
                    Send-Reply -Stream $stream -Reply '220 Ready to start TLS'
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                'AUTH' { $continue = Complete-Authentication -Stream $stream -Argument $argument }
                'MAIL' { Send-Reply -Stream $stream -Reply '250 OK' }
                'RCPT' { Send-Reply -Stream $stream -Reply '250 OK' }
                'DATA' {
                    Send-Reply -Stream $stream -Reply '354 End data with <CR><LF>.<CR><LF>'
                    $continue = Receive-MessageBody -Stream $stream
                }
                'VRFY' { Send-Reply -Stream $stream -Reply '250 Recorder <recorder@localhost>' }
                'EXPN' { Send-Reply -Stream $stream -Reply '250 Recorder <recorder@localhost>' }
                'HELP' { Send-Reply -Stream $stream -Reply '214 EHLO HELO STARTTLS AUTH MAIL RCPT DATA VRFY EXPN HELP NOOP RSET QUIT' }
                'NOOP' { Send-Reply -Stream $stream -Reply '250 OK' }
                'RSET' { Send-Reply -Stream $stream -Reply '250 OK' }
                'QUIT' { Send-Reply -Stream $stream -Reply '221 Bye'; $continue = $false }
                default { Send-Reply -Stream $stream -Reply '502 Command not implemented' }
            }
            if (-not $continue) { break }
        }
    } catch [System.IO.IOException] {
        # curl closed the connection mid-reply; what arrived is still recorded.
    } finally {
        $client.Close()
    }
    return , @(, $received.ToArray())
}

# The -Imap server: one connection, answered a tagged command at a time, with literals
# ({n}) read from curl after a "+" continuation and FETCH bodies sent as literals. It
# returns every byte curl sent, as one array, and writes the two-way transcript into
# $Transcript.
$serveImapSession = {
    param($Listener, [hashtable] $Overrides, [byte[]] $MessageBytes, [System.Text.StringBuilder] $Transcript, $TlsCertificate, [bool] $ImplicitTls, [int] $IdleMilliseconds, [string] $SessionHelpers)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    . ([scriptblock]::Create($SessionHelpers))
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $received = New-Object System.IO.MemoryStream

    function Send-Reply {
        param($Stream, [string] $Reply)
        $bytes = $latin1.GetBytes($Reply + "`r`n")
        $Stream.Write($bytes, 0, $bytes.Length)
        $Stream.Flush()
        foreach ($line in ($Reply -split "`r`n")) { [void] $Transcript.Append("< $line`r`n") }
    }

    # One line from curl, recorded, without its line ending; $null once curl hangs up or
    # stays silent for IdleMilliseconds.
    function Read-Line {
        param($Stream)
        $line = New-Object System.IO.MemoryStream
        while ($true) {
            try {
                $next = $Stream.ReadByte()
            } catch [System.IO.IOException] {
                return $null
            }
            if ($next -lt 0) { return $null }
            $received.WriteByte([byte] $next)
            $line.WriteByte([byte] $next)
            if ($next -eq 10) { break }
        }
        $text = $latin1.GetString($line.ToArray()).TrimEnd("`r", "`n")
        [void] $Transcript.Append("> $text`r`n")
        return $text
    }

    # One command from curl: a line and, while it ends in a literal {n} (RFC 3501 4.3),
    # the "+" continuation (none for a non-synchronizing {n+}), the n literal bytes and
    # the line that follows them. Returns the first line; $null once curl hangs up.
    function Read-Command {
        param($Stream)
        $first = Read-Line -Stream $Stream
        $line = $first
        while ($null -ne $line -and $line -match '\{(\d+)(\+?)\}$') {
            $length = [int] $Matches[1]
            if ($Matches[2] -ne '+') { Send-Reply -Stream $Stream -Reply '+ Ready for literal data' }
            $literal = New-Object byte[] $length
            $read = 0
            while ($read -lt $length) {
                try {
                    $count = $Stream.Read($literal, $read, $length - $read)
                } catch [System.IO.IOException] {
                    return $null
                }
                if ($count -le 0) { return $null }
                $read += $count
            }
            $received.Write($literal, 0, $length)
            $literalText = $latin1.GetString($literal)
            # A literal ending in CRLF adds no empty line of its own to the transcript.
            if ($literalText.EndsWith("`r`n")) { $literalText = $literalText.Substring(0, $literalText.Length - 2) }
            foreach ($literalLine in ($literalText -split "`r`n")) { [void] $Transcript.Append("> $literalLine`r`n") }
            $line = Read-Line -Stream $Stream
        }
        if ($null -eq $line) { return $null }
        return $first
    }

    # A reply for the command tagged $Tag: every line but the last sent as given, the
    # last with the tag in front.
    function Send-Tagged {
        param($Stream, [string] $Tag, [string] $Reply)
        $lines = @($Reply -split "`r`n")
        $lines[$lines.Count - 1] = "$Tag " + $lines[$lines.Count - 1]
        Send-Reply -Stream $Stream -Reply ($lines -join "`r`n")
    }

    function Get-CapabilityLine {
        param([bool] $Secure)
        # RFC 3501 6.2.1: STARTTLS is not offered again once the session is TLS.
        if ($Secure) { return '* CAPABILITY IMAP4rev1 AUTH=PLAIN AUTH=LOGIN' }
        return '* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN'
    }

    # The "+" continuations each mechanism needs before OK; $false once curl hangs up.
    function Complete-Authentication {
        param($Stream, [string] $Tag, [string] $Argument)
        $words = @($Argument -split ' ')
        $mechanism = $words[0].ToUpperInvariant()
        $hasInitialResponse = $words.Count -gt 1
        $challenges = switch ($mechanism) {
            'PLAIN' { if ($hasInitialResponse) { @() } else { @('+ ') } }
            'LOGIN' { if ($hasInitialResponse) { @('+ UGFzc3dvcmQ6') } else { @('+ VXNlcm5hbWU6', '+ UGFzc3dvcmQ6') } }
            default { @() }
        }
        foreach ($challenge in $challenges) {
            Send-Reply -Stream $Stream -Reply $challenge
            if ($null -eq (Read-Line -Stream $Stream)) { return $false }
        }
        Send-Tagged -Stream $Stream -Tag $Tag -Reply 'OK Authenticated'
        return $true
    }

    # The untagged FETCH response carrying the message as a literal, then the OK.
    function Send-FetchReply {
        param($Stream, [string] $Tag, [string] $Verb, [string] $Argument)
        $fields = @($Argument -split ' ', 2)
        $sequence = ($fields[0] -split '[:,]')[0]
        if ($sequence -notmatch '^\d+$') { $sequence = '1' }
        $item = if ($fields.Count -gt 1) { $fields[1].Trim('(', ')') } else { 'BODY[]' }
        $uid = if ($Verb -eq 'UID FETCH') { "UID $sequence " } else { '' }
        $message = $latin1.GetString($MessageBytes)
        Send-Reply -Stream $Stream -Reply "* $sequence FETCH ($uid$item {$($MessageBytes.Length)}`r`n$message)"
        Send-Tagged -Stream $Stream -Tag $Tag -Reply 'OK FETCH completed'
    }

    function Start-Tls {
        param($Stream)
        $secure = Wrap-Tls -Stream $Stream
        $secure.ReadTimeout = $IdleMilliseconds
        [void] $Transcript.Append("= TLS handshake completed on the control connection`r`n")
        return $secure
    }

    try {
        $client = $Listener.AcceptTcpClient()
    } catch {
        return , @(, $received.ToArray())
    }
    try {
        $stream = $client.GetStream()
        $secure = $ImplicitTls
        if ($ImplicitTls) { $stream = Wrap-Tls -Stream $stream }
        $stream.ReadTimeout = $IdleMilliseconds
        $greeting = if ($Overrides.ContainsKey('GREETING')) { Get-Override -Verb 'GREETING' } else { '* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready' }
        Send-Reply -Stream $stream -Reply $greeting
        while ($true) {
            $command = Read-Command -Stream $stream
            if ($null -eq $command) { break }  # ImapIdleMilliseconds without a byte, or curl hung up.
            $words = @($command -split ' ', 3)
            $tag = $words[0]
            $verb = if ($words.Count -gt 1) { $words[1].ToUpperInvariant() } else { '' }
            $argument = if ($words.Count -gt 2) { $words[2] } else { '' }
            if ($verb -eq 'UID' -and $argument -ne '') {
                $uidWords = @($argument -split ' ', 2)
                $verb = 'UID ' + $uidWords[0].ToUpperInvariant()
                $argument = if ($uidWords.Count -gt 1) { $uidWords[1] } else { '' }
            }
            if ($Overrides.ContainsKey($verb)) {
                $override = Get-Override -Verb $verb
                if ($override -ceq 'CLOSE') { break }  # Hang up instead of replying.
                Send-Tagged -Stream $stream -Tag $tag -Reply $override
                if ($verb -eq 'LOGOUT') { break }
                if ($verb -eq 'STARTTLS' -and ($override -split "`r`n")[-1].StartsWith('OK')) {
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                continue
            }
            $continue = $true
            switch ($verb) {
                'CAPABILITY' { Send-Tagged -Stream $stream -Tag $tag -Reply ((Get-CapabilityLine -Secure $secure) + "`r`nOK CAPABILITY completed") }
                'STARTTLS' {
                    Send-Tagged -Stream $stream -Tag $tag -Reply 'OK Begin TLS negotiation now'
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                'LOGIN' { Send-Tagged -Stream $stream -Tag $tag -Reply 'OK LOGIN completed' }
                'AUTHENTICATE' { $continue = Complete-Authentication -Stream $stream -Tag $tag -Argument $argument }
                'SELECT' { Send-Tagged -Stream $stream -Tag $tag -Reply "* FLAGS (\Answered \Flagged \Deleted \Seen \Draft)`r`n* 2 EXISTS`r`n* 0 RECENT`r`n* OK [UIDVALIDITY 1] UIDs valid`r`n* OK [UIDNEXT 3] Predicted next UID`r`nOK [READ-WRITE] SELECT completed" }
                'EXAMINE' { Send-Tagged -Stream $stream -Tag $tag -Reply "* FLAGS (\Answered \Flagged \Deleted \Seen \Draft)`r`n* 2 EXISTS`r`n* 0 RECENT`r`n* OK [UIDVALIDITY 1] UIDs valid`r`n* OK [UIDNEXT 3] Predicted next UID`r`nOK [READ-ONLY] EXAMINE completed" }
                'FETCH' { Send-FetchReply -Stream $stream -Tag $tag -Verb $verb -Argument $argument }
                'UID FETCH' { Send-FetchReply -Stream $stream -Tag $tag -Verb $verb -Argument $argument }
                'LIST' { Send-Tagged -Stream $stream -Tag $tag -Reply "* LIST (\HasNoChildren) `"/`" INBOX`r`n* LIST (\HasNoChildren) `"/`" Sent`r`nOK LIST completed" }
                'SEARCH' { Send-Tagged -Stream $stream -Tag $tag -Reply "* SEARCH 1 2`r`nOK SEARCH completed" }
                'UID SEARCH' { Send-Tagged -Stream $stream -Tag $tag -Reply "* SEARCH 1 2`r`nOK SEARCH completed" }
                'APPEND' { Send-Tagged -Stream $stream -Tag $tag -Reply 'OK APPEND completed' }
                'NOOP' { Send-Tagged -Stream $stream -Tag $tag -Reply 'OK NOOP completed' }
                'LOGOUT' { Send-Tagged -Stream $stream -Tag $tag -Reply "* BYE Logging out`r`nOK LOGOUT completed"; $continue = $false }
                default { Send-Tagged -Stream $stream -Tag $tag -Reply 'BAD Command not recognized' }
            }
            if (-not $continue) { break }
        }
    } catch [System.IO.IOException] {
        # curl closed the connection mid-reply; what arrived is still recorded.
    } finally {
        $client.Close()
    }
    return , @(, $received.ToArray())
}

# The -Pop3 server: one connection, answered a line at a time, with multi-line replies
# ended by a lone "." and dot-stuffed. It returns every byte curl sent, as one array, and
# writes the two-way transcript into $Transcript.
$servePop3Session = {
    param($Listener, [hashtable] $Overrides, [byte[]] $MessageBytes, [System.Text.StringBuilder] $Transcript, $TlsCertificate, [bool] $ImplicitTls, [int] $IdleMilliseconds, [string] $SessionHelpers)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    . ([scriptblock]::Create($SessionHelpers))
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $received = New-Object System.IO.MemoryStream
    $messageSize = $MessageBytes.Length

    function Send-Reply {
        param($Stream, [string] $Reply)
        $bytes = $latin1.GetBytes($Reply + "`r`n")
        $Stream.Write($bytes, 0, $bytes.Length)
        $Stream.Flush()
        foreach ($line in ($Reply -split "`r`n")) { [void] $Transcript.Append("< $line`r`n") }
    }

    # One line from curl, recorded, without its line ending; $null once curl hangs up or
    # stays silent for IdleMilliseconds.
    function Read-Line {
        param($Stream)
        $line = New-Object System.IO.MemoryStream
        while ($true) {
            try {
                $next = $Stream.ReadByte()
            } catch [System.IO.IOException] {
                return $null
            }
            if ($next -lt 0) { return $null }
            $received.WriteByte([byte] $next)
            $line.WriteByte([byte] $next)
            if ($next -eq 10) { break }
        }
        $text = $latin1.GetString($line.ToArray()).TrimEnd("`r", "`n")
        [void] $Transcript.Append("> $text`r`n")
        return $text
    }

    # A multi-line reply (RFC 1939 3): the status line, each content line with a leading
    # "." doubled, then the lone "." that ends it.
    function Send-MultiLine {
        param($Stream, [string] $Status, [string[]] $Lines)
        $stuffed = @($Lines | ForEach-Object { if ($_.StartsWith('.')) { '.' + $_ } else { $_ } })
        Send-Reply -Stream $Stream -Reply ((@($Status) + $stuffed + '.') -join "`r`n")
    }

    # Pop3Message as lines, without the line ending of its last one.
    function Get-MessageLines {
        $text = $latin1.GetString($MessageBytes)
        if ($text.EndsWith("`r`n")) { $text = $text.Substring(0, $text.Length - 2) }
        if ($text.Length -eq 0) { return , @() }
        return , @($text -split "`r`n")
    }

    # TOP's lines: the headers, the blank line and the first $BodyLines body lines.
    function Get-TopLines {
        param([int] $BodyLines)
        $lines = Get-MessageLines
        $blank = [array]::IndexOf($lines, '')
        if ($blank -lt 0) { return , $lines }
        $last = [Math]::Min($lines.Count - 1, $blank + $BodyLines)
        return , @($lines[0..$last])
    }

    function Get-CapabilityLines {
        param([bool] $Secure)
        # RFC 2595 4: STLS is not offered again once the session is TLS.
        if ($Secure) { return , @('USER', 'SASL PLAIN LOGIN', 'TOP', 'UIDL') }
        return , @('USER', 'SASL PLAIN LOGIN', 'STLS', 'TOP', 'UIDL')
    }

    # The "+" continuations each mechanism needs before +OK; $false once curl hangs up.
    function Complete-Authentication {
        param($Stream, [string] $Argument)
        $words = @($Argument -split ' ')
        $mechanism = $words[0].ToUpperInvariant()
        $hasInitialResponse = $words.Count -gt 1
        $challenges = switch ($mechanism) {
            'PLAIN' { if ($hasInitialResponse) { @() } else { @('+ ') } }
            'LOGIN' { if ($hasInitialResponse) { @('+ UGFzc3dvcmQ6') } else { @('+ VXNlcm5hbWU6', '+ UGFzc3dvcmQ6') } }
            default { @() }
        }
        foreach ($challenge in $challenges) {
            Send-Reply -Stream $Stream -Reply $challenge
            if ($null -eq (Read-Line -Stream $Stream)) { return $false }
        }
        Send-Reply -Stream $Stream -Reply '+OK Authenticated'
        return $true
    }

    function Start-Tls {
        param($Stream)
        $secure = Wrap-Tls -Stream $Stream
        $secure.ReadTimeout = $IdleMilliseconds
        [void] $Transcript.Append("= TLS handshake completed on the control connection`r`n")
        return $secure
    }

    try {
        $client = $Listener.AcceptTcpClient()
    } catch {
        return , @(, $received.ToArray())
    }
    try {
        $stream = $client.GetStream()
        $secure = $ImplicitTls
        if ($ImplicitTls) { $stream = Wrap-Tls -Stream $stream }
        $stream.ReadTimeout = $IdleMilliseconds
        $greeting = if ($Overrides.ContainsKey('GREETING')) { Get-Override -Verb 'GREETING' } else { '+OK POP3 ready <1896.697170952@localhost>' }
        Send-Reply -Stream $stream -Reply $greeting
        while ($true) {
            $command = Read-Line -Stream $stream
            if ($null -eq $command) { break }  # Pop3IdleMilliseconds without a byte, or curl hung up.
            $verb = ($command -split ' ', 2)[0].ToUpperInvariant()
            $argument = if ($command.Contains(' ')) { ($command -split ' ', 2)[1] } else { '' }
            $arguments = @($argument -split ' ' | Where-Object { $_ -ne '' })
            if ($Overrides.ContainsKey($verb)) {
                $override = Get-Override -Verb $verb
                if ($override -ceq 'CLOSE') { break }  # Hang up instead of replying.
                Send-Reply -Stream $stream -Reply $override
                if ($verb -eq 'QUIT') { break }
                if ($verb -eq 'STLS' -and $override.StartsWith('+OK')) {
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                continue
            }
            $continue = $true
            switch ($verb) {
                'CAPA' { Send-MultiLine -Stream $stream -Status '+OK Capability list follows' -Lines (Get-CapabilityLines -Secure $secure) }
                'STLS' {
                    Send-Reply -Stream $stream -Reply '+OK Begin TLS negotiation'
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                'USER' { Send-Reply -Stream $stream -Reply '+OK User accepted' }
                'PASS' { Send-Reply -Stream $stream -Reply '+OK Logged in' }
                'APOP' { Send-Reply -Stream $stream -Reply '+OK Logged in' }
                'AUTH' {
                    if ($arguments.Count -eq 0) {
                        Send-MultiLine -Stream $stream -Status '+OK SASL mechanisms follow' -Lines @('PLAIN', 'LOGIN')
                    } else {
                        $continue = Complete-Authentication -Stream $stream -Argument $argument
                    }
                }
                'STAT' { Send-Reply -Stream $stream -Reply "+OK 2 $(2 * $messageSize)" }
                'LIST' {
                    if ($arguments.Count -gt 0) {
                        Send-Reply -Stream $stream -Reply "+OK $($arguments[0]) $messageSize"
                    } else {
                        Send-MultiLine -Stream $stream -Status "+OK 2 messages ($(2 * $messageSize) octets)" -Lines @("1 $messageSize", "2 $messageSize")
                    }
                }
                'RETR' { Send-MultiLine -Stream $stream -Status "+OK $messageSize octets" -Lines (Get-MessageLines) }
                'TOP' {
                    $bodyLines = 0
                    if ($arguments.Count -gt 1) { [void] [int]::TryParse($arguments[1], [ref] $bodyLines) }
                    Send-MultiLine -Stream $stream -Status '+OK Top of message follows' -Lines (Get-TopLines -BodyLines $bodyLines)
                }
                'UIDL' {
                    if ($arguments.Count -gt 0) {
                        Send-Reply -Stream $stream -Reply "+OK $($arguments[0]) uid-$($arguments[0])"
                    } else {
                        Send-MultiLine -Stream $stream -Status '+OK Unique-ID listing follows' -Lines @('1 uid-1', '2 uid-2')
                    }
                }
                'DELE' { Send-Reply -Stream $stream -Reply '+OK Message deleted' }
                'RSET' { Send-Reply -Stream $stream -Reply '+OK' }
                'NOOP' { Send-Reply -Stream $stream -Reply '+OK' }
                'QUIT' { Send-Reply -Stream $stream -Reply '+OK Bye'; $continue = $false }
                default { Send-Reply -Stream $stream -Reply '-ERR Command not recognized' }
            }
            if (-not $continue) { break }
        }
    } catch [System.IO.IOException] {
        # curl closed the connection mid-reply; what arrived is still recorded.
    } finally {
        $client.Close()
    }
    return , @(, $received.ToArray())
}

# The -Raw server: one TCP connection, plain or (with a certificate) TLS, answered burst by
# burst from a script of replies. A burst ends when curl pauses for IdleMilliseconds or
# closes its end. It returns every byte curl sent, decrypted, as one array, and writes the
# two-way transcript into $Transcript.
$serveRawSession = {
    param($Listener, $Replies, [System.Text.StringBuilder] $Transcript, [bool] $ReplyFirst, [int] $IdleMilliseconds, $TlsCertificate)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $received = New-Object System.IO.MemoryStream
    # Over TLS the socket cannot say whether decrypted bytes are waiting, so a burst ends
    # when a read has not completed within IdleMilliseconds; that read stays pending and
    # is the first one the next burst waits on.
    $tlsRead = @{ Stream = $null; Buffer = (New-Object byte[] 65536); Pending = $null }

    # One transcript line per LF-ended piece of Bytes; the closing CRLF is left out and
    # any other byte outside printable ASCII is written \xHH.
    function Add-TranscriptBurst {
        param([string] $Prefix, [byte[]] $Bytes)
        $line = New-Object System.Text.StringBuilder
        for ($index = 0; $index -lt $Bytes.Length; $index++) {
            $byte = $Bytes[$index]
            if ($byte -eq 13 -and $index + 1 -lt $Bytes.Length -and $Bytes[$index + 1] -eq 10) {
                [void] $Transcript.Append("$Prefix$line`r`n")
                [void] $line.Clear()
                $index++
                continue
            }
            if ($byte -eq 92) { [void] $line.Append('\') }
            elseif ($byte -ge 32 -and $byte -le 126) { [void] $line.Append([char] $byte) }
            else { [void] $line.Append('\x' + $byte.ToString('X2')) }
            if ($byte -eq 10) {
                [void] $Transcript.Append("$Prefix$line`r`n")
                [void] $line.Clear()
            }
        }
        if ($line.Length -gt 0) { [void] $Transcript.Append("$Prefix$line`r`n") }
    }

    # Reads into $Burst until curl pauses for IdleMilliseconds; $true once curl hung up.
    function Read-SocketBurst {
        param($Socket, [System.IO.MemoryStream] $Burst)
        $buffer = New-Object byte[] 65536
        while ($Socket.Poll($IdleMilliseconds * 1000, [System.Net.Sockets.SelectMode]::SelectRead)) {
            try {
                $count = $Socket.Receive($buffer)
            } catch [System.Net.Sockets.SocketException] {
                $count = 0  # A reset: curl is gone.
            }
            if ($count -le 0) { return $true }
            $Burst.Write($buffer, 0, $count)
        }
        return $false
    }

    # The TLS form of Read-SocketBurst: the decrypted bytes, read from $tlsRead.Stream.
    function Read-TlsBurst {
        param([System.IO.MemoryStream] $Burst)
        while ($true) {
            if ($null -eq $tlsRead.Pending) { $tlsRead.Pending = $tlsRead.Stream.ReadAsync($tlsRead.Buffer, 0, $tlsRead.Buffer.Length) }
            try {
                if (-not $tlsRead.Pending.Wait($IdleMilliseconds)) { return $false }
                $count = $tlsRead.Pending.Result
            } catch {
                $count = 0  # A reset or a broken TLS record: curl is gone.
            }
            $tlsRead.Pending = $null
            if ($count -le 0) { return $true }
            $Burst.Write($tlsRead.Buffer, 0, $count)
        }
    }

    # What curl sends until it pauses for IdleMilliseconds, recorded; Closed says whether
    # curl hung up.
    function Read-Burst {
        param($Socket)
        $burst = New-Object System.IO.MemoryStream
        $closed = if ($null -ne $tlsRead.Stream) { Read-TlsBurst -Burst $burst } else { Read-SocketBurst -Socket $Socket -Burst $burst }
        [byte[]] $bytes = $burst.ToArray()
        $received.Write($bytes, 0, $bytes.Length)
        if ($bytes.Length -gt 0) { Add-TranscriptBurst -Prefix '> ' -Bytes $bytes }
        if ($closed) { [void] $Transcript.Append("= curl closed the connection`r`n") }
        return $closed
    }

    # Sends one reply; $false once curl has hung up and it could not be sent.
    function Send-RawReply {
        param($Socket, [byte[]] $Reply)
        try {
            if ($null -ne $tlsRead.Stream) {
                $tlsRead.Stream.Write($Reply, 0, $Reply.Length)
                $tlsRead.Stream.Flush()
            } else {
                [void] $Socket.Send($Reply)
            }
        } catch [System.Net.Sockets.SocketException], [System.IO.IOException] {
            return $false
        }
        Add-TranscriptBurst -Prefix '< ' -Bytes $Reply
        return $true
    }

    try {
        $client = $Listener.AcceptTcpClient()
    } catch {
        return , @(, $received.ToArray())  # The listener was stopped: curl never connected.
    }
    try {
        $socket = $client.Client
        if ($null -ne $TlsCertificate) {
            $secure = New-Object System.Net.Security.SslStream($client.GetStream(), $false)
            try {
                $secure.AuthenticateAsServer($TlsCertificate, $false, [System.Security.Authentication.SslProtocols]::Tls12, $false)
            } catch {
                # curl refused the certificate or hung up mid-handshake: nothing was sent.
                return , @(, $received.ToArray())
            }
            $tlsRead.Stream = $secure
            [void] $Transcript.Append("= TLS handshake completed`r`n")
        }
        $nextReply = 0
        if ($ReplyFirst -and $Replies.Count -gt 0) {
            [void] (Send-RawReply -Socket $socket -Reply $Replies[0])
            $nextReply = 1
        }
        $curlClosed = $false
        while ($true) {
            $curlClosed = Read-Burst -Socket $socket
            if ($curlClosed) { break }
            if ($nextReply -ge $Replies.Count) { break }  # Last reply sent and curl went idle.
            if (-not (Send-RawReply -Socket $socket -Reply $Replies[$nextReply])) { break }
            $nextReply++
        }
        if ($null -ne $tlsRead.Stream -and -not $curlClosed) {
            # A TLS close_notify ahead of the FIN, so curl sees the close as the end of the reply.
            try { $tlsRead.Stream.ShutdownAsync().Wait() } catch { }
        }
    } finally {
        $client.Close()
    }
    return , @(, $received.ToArray())
}

# The -Tftp server: one transfer over UDP. The first datagram arrives on the listening
# socket; every reply goes out from a new socket, the server's transfer identifier. It
# returns every datagram curl sent, as one array, writes the transcript into $Transcript
# and the DATA curl wrote into $UploadedData.
$serveTftpSession = {
    param($Listener, [byte[]] $DataBytes, [byte[]] $FirstReply, [System.Text.StringBuilder] $Transcript, [System.IO.MemoryStream] $UploadedData, [int] $IdleMilliseconds, [System.Net.IPAddress] $ListenAddress)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $received = New-Object System.IO.MemoryStream
    $buffer = New-Object byte[] 65536
    $anyAddress = if ($ListenAddress.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6) { [System.Net.IPAddress]::IPv6Any } else { [System.Net.IPAddress]::Any }
    # The main script closes the listening socket once curl has exited.
    $state = @{ ListenerOpen = $true }

    # One transcript line: direction, both ports, and the bytes with every byte outside
    # printable ASCII written \xHH and a backslash as \\.
    function Add-TranscriptDatagram {
        param([bool] $FromCurl, [int] $ServerPort, [int] $CurlPort, [byte[]] $Bytes)
        $line = New-Object System.Text.StringBuilder
        [void] $line.Append($(if ($FromCurl) { "> curl:$CurlPort -> server:$ServerPort " } else { "< server:$ServerPort -> curl:$CurlPort " }))
        foreach ($byte in $Bytes) {
            if ($byte -eq 92) { [void] $line.Append('\\') }
            elseif ($byte -ge 32 -and $byte -le 126) { [void] $line.Append([char] $byte) }
            else { [void] $line.Append('\x' + $byte.ToString('X2')) }
        }
        [void] $Transcript.Append("$line`r`n")
    }

    # One datagram from Socket, recorded as curl's, with the endpoint it came from.
    function Receive-FromSocket {
        param($Socket)
        [System.Net.EndPoint] $from = New-Object System.Net.IPEndPoint($anyAddress, 0)
        $count = $Socket.ReceiveFrom($buffer, [ref] $from)
        [byte[]] $bytes = New-Object byte[] $count
        [Array]::Copy($buffer, $bytes, $count)
        $received.Write($bytes, 0, $count)
        Add-TranscriptDatagram -FromCurl $true -ServerPort ([System.Net.IPEndPoint] $Socket.LocalEndPoint).Port -CurlPort ([System.Net.IPEndPoint] $from).Port -Bytes $bytes
        return @{ Bytes = $bytes; From = $from }
    }

    function Send-Datagram {
        param([byte[]] $Bytes)
        [void] $transfer.SendTo($Bytes, $curlEndPoint)
        Add-TranscriptDatagram -FromCurl $false -ServerPort ([System.Net.IPEndPoint] $transfer.LocalEndPoint).Port -CurlPort ([System.Net.IPEndPoint] $curlEndPoint).Port -Bytes $Bytes
    }

    # The next datagram curl sends to the transfer socket, or $null after IdleMilliseconds
    # of silence. Datagrams it sends to the listening port meanwhile are recorded only.
    function Receive-Datagram {
        $deadline = [DateTime]::UtcNow.AddMilliseconds($IdleMilliseconds)
        while ($true) {
            $remaining = ($deadline - [DateTime]::UtcNow).TotalMilliseconds
            if ($remaining -le 0) { return $null }
            $ready = New-Object System.Collections.ArrayList
            [void] $ready.Add($transfer)
            if ($state.ListenerOpen) { [void] $ready.Add($Listener) }
            try {
                [System.Net.Sockets.Socket]::Select($ready, $null, $null, [int] ($remaining * 1000))
            } catch [System.ObjectDisposedException] {
                $state.ListenerOpen = $false
                continue
            }
            if ($ready.Count -eq 0) { return $null }
            try {
                $datagram = Receive-FromSocket -Socket $ready[0]
            } catch [System.Net.Sockets.SocketException] {
                continue  # An ICMP port unreachable from an earlier send, reported here.
            } catch [System.ObjectDisposedException] {
                $state.ListenerOpen = $false
                continue
            }
            if ($ready[0] -eq $transfer) { return , $datagram.Bytes }
        }
    }

    function Get-Opcode {
        param([byte[]] $Packet)
        if ($Packet.Length -lt 2) { return -1 }
        return $Packet[0] * 256 + $Packet[1]
    }

    # Sends Packet and waits for curl's packet with Opcode and Block, sending Packet again
    # after each silence, three times at most. $null when curl sends ERROR or stays silent.
    function Send-AndAwait {
        param([byte[]] $Packet, [int] $Opcode, [int] $Block)
        for ($attempt = 0; $attempt -le 3; $attempt++) {
            Send-Datagram -Bytes $Packet
            while ($true) {
                $answer = Receive-Datagram
                if ($null -eq $answer) { break }
                $answerOpcode = Get-Opcode -Packet $answer
                if ($answerOpcode -eq 5) { return $null }
                if ($answerOpcode -eq $Opcode -and $answer.Length -ge 4 -and ($answer[2] * 256 + $answer[3]) -eq $Block) { return , $answer }
            }
        }
        return $null
    }

    # The name-value pairs of a request's or an OACK's options, from its field FirstOption on.
    function Get-OptionPairs {
        param([byte[]] $Packet, [int] $FirstOption)
        $fields = $latin1.GetString($Packet, 2, $Packet.Length - 2).Split([char] 0)
        $pairs = New-Object System.Collections.Generic.List[object]
        for ($index = $FirstOption; $index + 1 -lt $fields.Length; $index += 2) {
            if ($fields[$index].Length -gt 0) { $pairs.Add(@($fields[$index], $fields[$index + 1])) }
        }
        return , $pairs
    }

    function Get-BlockSize {
        param($Pairs)
        foreach ($pair in $Pairs) {
            $size = 0
            if ($pair[0] -ieq 'blksize' -and [int]::TryParse($pair[1], [ref] $size)) { return $size }
        }
        return 512
    }

    # The OACK accepting the options the server knows, or $null when it knows none.
    function New-OptionAcknowledgement {
        param($Pairs, [bool] $IsRead)
        $accepted = New-Object System.Text.StringBuilder
        foreach ($pair in $Pairs) {
            $value = 0
            $known = [int]::TryParse($pair[1], [ref] $value)
            $answer = switch ($pair[0].ToLowerInvariant()) {
                'blksize' { if ($known -and $value -ge 8) { [Math]::Min($value, 65464) } }
                'tsize' { if ($IsRead) { $DataBytes.Length } elseif ($known) { $value } }
                'timeout' { if ($known) { $value } }
            }
            if ($null -ne $answer) { [void] $accepted.Append("$($pair[0])`0$answer`0") }
        }
        if ($accepted.Length -eq 0) { return $null }
        return , ([byte[]] (@(0, 6) + $latin1.GetBytes($accepted.ToString())))
    }

    function New-BlockPacket {
        param([int] $Opcode, [int] $Block)
        return , ([byte[]] @(0, $Opcode, [Math]::Floor(($Block % 65536) / 256), ($Block % 256)))
    }

    $request = $null
    while ($null -eq $request) {
        try {
            if (-not $Listener.Poll(1000000, [System.Net.Sockets.SelectMode]::SelectRead)) { continue }
            $request = Receive-FromSocket -Socket $Listener
        } catch [System.ObjectDisposedException] {
            return , @(, $received.ToArray())  # The listener was closed: curl never sent a request.
        } catch [System.Net.Sockets.SocketException] {
            continue
        }
    }
    $curlEndPoint = $request.From
    [byte[]] $requestBytes = $request.Bytes
    $transfer = New-Object System.Net.Sockets.Socket($ListenAddress.AddressFamily, [System.Net.Sockets.SocketType]::Dgram, [System.Net.Sockets.ProtocolType]::Udp)
    try {
        $transfer.Bind((New-Object System.Net.IPEndPoint($ListenAddress, 0)))
        $requestOpcode = Get-Opcode -Packet $requestBytes
        if ($requestOpcode -ne 1 -and $requestOpcode -ne 2) {
            Send-Datagram -Bytes ([byte[]] (@(0, 5, 0, 4) + $latin1.GetBytes("Illegal TFTP operation`0")))
            return , @(, $received.ToArray())
        }
        $isRead = $requestOpcode -eq 1
        $blockSize = 512
        $opening = $null
        if ($FirstReply.Length -gt 0) {
            $opening = $FirstReply
            if ((Get-Opcode -Packet $FirstReply) -ne 6) {
                # Not an OACK: the transfer ends here; record what curl answers.
                Send-Datagram -Bytes $FirstReply
                while ($null -ne (Receive-Datagram)) { }
                return , @(, $received.ToArray())
            }
            $blockSize = Get-BlockSize -Pairs (Get-OptionPairs -Packet $FirstReply -FirstOption 0)
        } else {
            $opening = New-OptionAcknowledgement -Pairs (Get-OptionPairs -Packet $requestBytes -FirstOption 2) -IsRead $isRead
            if ($null -ne $opening) { $blockSize = Get-BlockSize -Pairs (Get-OptionPairs -Packet $opening -FirstOption 0) }
        }

        if ($isRead) {
            # After an OACK curl acknowledges block 0 before DATA 1 is sent.
            if ($null -ne $opening -and $null -eq (Send-AndAwait -Packet $opening -Opcode 4 -Block 0)) { return , @(, $received.ToArray()) }
            $block = 1
            $offset = 0
            while ($true) {
                $length = [Math]::Min($blockSize, $DataBytes.Length - $offset)
                $packet = New-Object byte[] (4 + $length)
                [Array]::Copy((New-BlockPacket -Opcode 3 -Block $block), $packet, 4)
                [Array]::Copy($DataBytes, $offset, $packet, 4, $length)
                if ($null -eq (Send-AndAwait -Packet $packet -Opcode 4 -Block ($block % 65536))) { break }
                $offset += $length
                if ($length -lt $blockSize) { break }  # The short block was the last.
                $block++
            }
        } else {
            # A write is opened by the OACK, or by ACK 0 when there is none.
            $packet = if ($null -ne $opening) { $opening } else { New-BlockPacket -Opcode 4 -Block 0 }
            $block = 1
            while ($true) {
                $data = Send-AndAwait -Packet $packet -Opcode 3 -Block ($block % 65536)
                if ($null -eq $data) { break }
                $UploadedData.Write($data, 4, $data.Length - 4)
                $packet = New-BlockPacket -Opcode 4 -Block $block
                if ($data.Length - 4 -lt $blockSize) {
                    Send-Datagram -Bytes $packet  # The short block was the last.
                    break
                }
                $block++
            }
        }
    } finally {
        $transfer.Close()
    }
    return , @(, $received.ToArray())
}

function New-IssuedByThrowawayRoot {
    # Signs Request with a throwaway root CA named by no store and writes the root's PEM
    # to RootCertificateFile. Neither certificate names a CRL or OCSP endpoint.
    param($Request, $Key, [string] $RootCertificateFile, [System.DateTimeOffset] $NotBefore, [System.DateTimeOffset] $NotAfter)
    $rootKey = New-Object System.Security.Cryptography.RSACng(2048)
    try {
        $rootRequest = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest('CN=Record-CurlExchange throwaway root', $rootKey, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $rootRequest.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension($true, $false, 0, $true)))
        $rootRequest.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509KeyUsageExtension([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign, $true)))
        $root = $rootRequest.CreateSelfSigned($NotBefore.AddMinutes(-5), $NotAfter.AddDays(1))
        try {
            $pem = "-----BEGIN CERTIFICATE-----`n" + [System.Convert]::ToBase64String($root.RawData, [System.Base64FormattingOptions]::InsertLineBreaks) + "`n-----END CERTIFICATE-----`n"
            [System.IO.File]::WriteAllText($RootCertificateFile, $pem.Replace("`r`n", "`n"))
            $serial = New-Object byte[] 16
            [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($serial)
            $serial[0] = $serial[0] -band 0x7F
            $issued = $Request.Create($root, $NotBefore, $NotAfter, $serial)
            try {
                return [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($issued, $Key)
            } finally {
                $issued.Reset()
            }
        } finally {
            $root.Reset()
        }
    } finally {
        $rootKey.Dispose()
    }
}

function New-ThrowawayTlsCertificate {
    # Schannel will not serve the ephemeral key CreateSelfSigned returns, so the
    # certificate is reloaded from its PFX export. Loaded without PersistKeySet, its key
    # container is deleted when the certificate is reset; no store is touched. With a
    # RootCertificateFile the leaf is issued by a throwaway root whose PEM is written there.
    # -Exportable lets the key be exported again, for the -TlsRenegotiationOff relay.
    param([string] $RootCertificateFile, [switch] $Exportable)
    $rsa = New-Object System.Security.Cryptography.RSACng(2048)
    try {
        $request = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest('CN=127.0.0.1', $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $alternativeNames = New-Object System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder
        $alternativeNames.AddIpAddress([System.Net.IPAddress]::Loopback)
        $request.CertificateExtensions.Add($alternativeNames.Build())
        $now = [System.DateTimeOffset]::UtcNow
        $selfSigned = if ([string]::IsNullOrEmpty($RootCertificateFile)) {
            $request.CreateSelfSigned($now.AddMinutes(-5), $now.AddDays(1))
        } else {
            New-IssuedByThrowawayRoot -Request $request -Key $rsa -RootCertificateFile $RootCertificateFile -NotBefore $now.AddMinutes(-5) -NotAfter $now.AddDays(1)
        }
        try {
            $pfx = $selfSigned.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx)
        } finally {
            $selfSigned.Reset()
        }
    } finally {
        $rsa.Dispose()
    }
    if ($Exportable) {
        return New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($pfx, [string] $null, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
    }
    return New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(, $pfx)
}

# The -TlsRenegotiationOff relay: a .NET 10 file-based app, because only .NET 5 and later
# can refuse renegotiation. Arguments: listen address, port, PFX path, PFX password,
# SslProtocols as an integer. It reads the backend port from its first line of standard
# input, prints "listening" once bound, then relays each connection whose handshake
# succeeds to 127.0.0.1:<backend port> until it is killed.
$tlsRelaySource = @'
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

var listenAddress = IPAddress.Parse(args[0]);
var port = int.Parse(args[1]);
using var certificate = X509CertificateLoader.LoadPkcs12FromFile(args[2], args[3]);
var protocols = (SslProtocols)int.Parse(args[4]);
// Windows PowerShell's standard input writer may lead with a byte order mark; keep the digits.
var backendPort = int.Parse(string.Concat(Console.ReadLine()!.Where(char.IsAsciiDigit)));
var listener = new TcpListener(listenAddress, port);
listener.Start();
Console.WriteLine("listening");

while (true)
{
    _ = RelayAsync(await listener.AcceptTcpClientAsync());
}

async Task RelayAsync(TcpClient client)
{
    using (client)
    {
        await using var secured = new SslStream(client.GetStream());
        try
        {
            await secured.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                EnabledSslProtocols = protocols,
                AllowRenegotiation = false,
                ClientCertificateRequired = false,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            });
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            Console.Error.WriteLine($"relay: handshake failed: {exception.Message}");
            return;
        }

        Console.Error.WriteLine($"relay: handshake completed: {secured.SslProtocol}, {secured.NegotiatedCipherSuite}");
        using var backend = new TcpClient();
        await backend.ConnectAsync(IPAddress.Loopback, backendPort);
        var backendStream = backend.GetStream();
        var toBackend = CopyThenShutdownAsync(secured, backendStream, backend.Client);
        try
        {
            await backendStream.CopyToAsync(secured);
            await secured.ShutdownAsync();
        }
        catch (IOException)
        {
            // curl hung up first.
        }
    }
}

static async Task CopyThenShutdownAsync(Stream from, Stream to, Socket toSocket)
{
    try
    {
        await from.CopyToAsync(to);
        toSocket.Shutdown(SocketShutdown.Send);
    }
    catch (Exception exception) when (exception is IOException or ObjectDisposedException or SocketException)
    {
        // The connection closed while bytes were still being relayed.
    }
}
'@

function Start-TlsRelay {
    # Writes the relay and the certificate's PFX under %TEMP%\SurlRecorder and starts the
    # relay. It must start before the backend listener exists: .NET Framework sockets are
    # inheritable, and a relay holding the listening socket would keep a stopped listener's
    # accept waiting. Returns the process and the PFX path, for Connect- and Stop-TlsRelay.
    param($Certificate, [System.Net.IPAddress] $Address, [int] $ListenPort, [int] $Protocols)
    $directory = Join-Path ([System.IO.Path]::GetTempPath()) 'SurlRecorder'
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $source = Join-Path $directory 'TlsRelay.cs'
    [System.IO.File]::WriteAllText($source, $tlsRelaySource, (New-Object System.Text.UTF8Encoding($false)))
    $password = [Guid]::NewGuid().ToString('N')
    $pfxPath = Join-Path $directory "$([Guid]::NewGuid().ToString('N')).pfx"
    [System.IO.File]::WriteAllBytes($pfxPath, $Certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password))
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'dotnet'
    $startInfo.Arguments = (@('run', $source, '--', $Address.ToString(), $ListenPort, $pfxPath, $password, $Protocols) | ForEach-Object { ConvertTo-CommandLineArgument -Argument ([string] $_) }) -join ' '
    $startInfo.WorkingDirectory = $directory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.CreateNoWindow = $true
    return @([System.Diagnostics.Process]::Start($startInfo), $pfxPath)
}

function Connect-TlsRelay {
    # Tells the relay the backend port and waits until it listens.
    param($Relay, [int] $BackendPort)
    $Relay[0].StandardInput.WriteLine([string] $BackendPort)
    $Relay[0].StandardInput.Flush()
    $line = $Relay[0].StandardOutput.ReadLine()
    if ($line -ne 'listening') {
        throw "The TLS relay did not start (it printed '$line'); -TlsRenegotiationOff needs the .NET 10 SDK's dotnet on PATH."
    }
}

function Stop-TlsRelay {
    # dotnet run starts the relay as its child, so the whole tree is stopped; cmd swallows
    # taskkill's report, which would otherwise be a terminating error here.
    param($Relay)
    & cmd.exe /c "taskkill /T /F /PID $($Relay[0].Id) >nul 2>&1"
    $Relay[0].Dispose()
    Remove-Item -LiteralPath $Relay[1] -Force
}

if ([string]::IsNullOrEmpty($Curl)) { $Curl = Get-ReferenceCurlPath }
Assert-PinnedUpstreamCurl -Path $Curl
$responseBytes = New-Object System.Collections.Generic.List[byte[]]
foreach ($text in $Response) { $responseBytes.Add((ConvertFrom-EscapedResponse -Text $text)) }
function ConvertTo-ReplyOverrides {
    # 'VERB=reply' entries as a table of verb to the list of its replies, in order.
    param([string[]] $Entries, [string] $ParameterName)
    $overrides = @{}
    foreach ($entry in $Entries) {
        $separator = $entry.IndexOf('=')
        if ($separator -lt 1) { throw "$ParameterName '$entry' is not VERB=reply." }
        $overrideVerb = $entry.Substring(0, $separator).ToUpperInvariant()
        if (-not $overrides.ContainsKey($overrideVerb)) { $overrides[$overrideVerb] = New-Object System.Collections.Generic.List[string] }
        $overrides[$overrideVerb].Add([System.Text.Encoding]::GetEncoding(28591).GetString((ConvertFrom-EscapedResponse -Text $entry.Substring($separator + 1))))
    }
    return $overrides
}
$ftpOverrides = ConvertTo-ReplyOverrides -Entries $FtpReply -ParameterName 'FtpReply'
$smtpOverrides = ConvertTo-ReplyOverrides -Entries $SmtpReply -ParameterName 'SmtpReply'
$imapOverrides = ConvertTo-ReplyOverrides -Entries $ImapReply -ParameterName 'ImapReply'
$pop3Overrides = ConvertTo-ReplyOverrides -Entries $Pop3Reply -ParameterName 'Pop3Reply'
$transcript = New-Object System.Text.StringBuilder
$uploadedData = New-Object System.IO.MemoryStream
$OutDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location).ProviderPath, $OutDirectory))
New-Item -ItemType Directory -Path $OutDirectory -Force | Out-Null

$servedTlsProtocols = switch ($TlsProtocol) {
    'Tls13' { [System.Security.Authentication.SslProtocols]::Tls13 }
    'Tls12AndTls13' { [System.Security.Authentication.SslProtocols]::Tls12 -bor [System.Security.Authentication.SslProtocols]::Tls13 }
    default { [System.Security.Authentication.SslProtocols]::Tls12 }
}
$tlsCertificate = if ($Tls -or $Ftp -or $Smtp -or $Imap -or $Pop3) { New-ThrowawayTlsCertificate -RootCertificateFile $TlsRootCertificateFile -Exportable:$TlsRenegotiationOff } else { $null }
# With -TlsRenegotiationOff the relay does the TLS, so the recorder's server serves plaintext.
$servedCertificate = if ($TlsRenegotiationOff) { $null } else { $tlsCertificate }
$tlsRelay = $null
# -NoServer binds nothing: the caller's own server answers curl.
$listener = $null
$server = $null
if ($Tftp) {
    $listener = New-Object System.Net.Sockets.Socket($ListenAddress.AddressFamily, [System.Net.Sockets.SocketType]::Dgram, [System.Net.Sockets.ProtocolType]::Udp)
    $listener.Bind((New-Object System.Net.IPEndPoint($ListenAddress, $Port)))
    $server = [System.Management.Automation.PowerShell]::Create()
} elseif ($TlsRenegotiationOff) {
    $tlsRelay = Start-TlsRelay -Certificate $tlsCertificate -Address $ListenAddress -ListenPort $Port -Protocols ([int] $servedTlsProtocols)
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $server = [System.Management.Automation.PowerShell]::Create()
} elseif (-not $NoServer) {
    $listener = New-Object System.Net.Sockets.TcpListener($ListenAddress, $Port)
    $listener.Start()
    $server = [System.Management.Automation.PowerShell]::Create()
}

# -Tftp listens on a UDP socket, which is closed rather than stopped.
function Stop-Listener {
    if ($listener -is [System.Net.Sockets.Socket]) { $listener.Close() } else { $listener.Stop() }
}
try {
    if ($NoServer) {
        $serverRun = $null
    } elseif ($Ftp) {
        [void] $server.AddScript($serveFtpSession).AddArgument($listener).AddArgument($ftpOverrides).AddArgument([byte[]] (ConvertFrom-EscapedResponse -Text $FtpData)).AddArgument($transcript).AddArgument($uploadedData).AddArgument($tlsCertificate).AddArgument([bool] $Tls).AddArgument($FtpIdleMilliseconds).AddArgument($ListenAddress).AddArgument($sessionHelpers.ToString())
    } elseif ($Smtp) {
        [void] $server.AddScript($serveSmtpSession).AddArgument($listener).AddArgument($smtpOverrides).AddArgument($transcript).AddArgument($tlsCertificate).AddArgument([bool] $Tls).AddArgument($SmtpIdleMilliseconds).AddArgument($sessionHelpers.ToString())
    } elseif ($Imap) {
        [void] $server.AddScript($serveImapSession).AddArgument($listener).AddArgument($imapOverrides).AddArgument([byte[]] (ConvertFrom-EscapedResponse -Text $ImapMessage)).AddArgument($transcript).AddArgument($tlsCertificate).AddArgument([bool] $Tls).AddArgument($ImapIdleMilliseconds).AddArgument($sessionHelpers.ToString())
    } elseif ($Pop3) {
        [void] $server.AddScript($servePop3Session).AddArgument($listener).AddArgument($pop3Overrides).AddArgument([byte[]] (ConvertFrom-EscapedResponse -Text $Pop3Message)).AddArgument($transcript).AddArgument($tlsCertificate).AddArgument([bool] $Tls).AddArgument($Pop3IdleMilliseconds).AddArgument($sessionHelpers.ToString())
    } elseif ($Raw) {
        $rawReplies = New-Object System.Collections.Generic.List[byte[]]
        foreach ($text in $RawReply) { $rawReplies.Add((ConvertFrom-EscapedResponse -Text $text)) }
        [void] $server.AddScript($serveRawSession).AddArgument($listener).AddArgument($rawReplies).AddArgument($transcript).AddArgument([bool] $RawReplyFirst).AddArgument($RawIdleMilliseconds).AddArgument($tlsCertificate)
    } elseif ($Tftp) {
        [void] $server.AddScript($serveTftpSession).AddArgument($listener).AddArgument([byte[]] (ConvertFrom-EscapedResponse -Text $TftpData)).AddArgument([byte[]] (ConvertFrom-EscapedResponse -Text $TftpReply)).AddArgument($transcript).AddArgument($uploadedData).AddArgument($TftpIdleMilliseconds).AddArgument($ListenAddress)
    } else {
        [void] $server.AddScript($serveConnections).AddArgument($listener).AddArgument($responseBytes).AddArgument($Connections).AddArgument($ResponseDelayMilliseconds).AddArgument([bool] $Reset).AddArgument($RespondAfterBodyBytes).AddArgument($servedCertificate).AddArgument($HoldOpenMilliseconds).AddArgument([int] $servedTlsProtocols).AddArgument([bool] $CloseUnread).AddArgument([byte[]] (ConvertFrom-EscapedResponse -Text $InterimResponse))
    }
    if ($null -ne $server) { $serverRun = $server.BeginInvoke() }
    if ($null -ne $tlsRelay) { Connect-TlsRelay -Relay $tlsRelay -BackendPort $listener.LocalEndpoint.Port }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $Curl
    $startInfo.Arguments = (@($CurlArgs | ForEach-Object { ConvertTo-CommandLineArgument -Argument $_ }) -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    # The standard input writer takes the console's input encoding, whose UTF-8 byte order
    # mark would reach curl ahead of StandardInput; Latin-1 has none. Windows PowerShell 5.1
    # has no ProcessStartInfo.StandardInputEncoding to set instead.
    $consoleInputEncoding = [System.Console]::InputEncoding
    [System.Console]::InputEncoding = [System.Text.Encoding]::GetEncoding(28591)
    try {
        $curlClock = [System.Diagnostics.Stopwatch]::StartNew()
        $curlProcess = [System.Diagnostics.Process]::Start($startInfo)
    } finally {
        [System.Console]::InputEncoding = $consoleInputEncoding
    }
    try {
        $standardInputBytes = ConvertFrom-EscapedResponse -Text $StandardInput
        $curlProcess.StandardInput.BaseStream.Write($standardInputBytes, 0, $standardInputBytes.Length)
        $curlProcess.StandardInput.Close()
        $stdout = New-Object System.IO.MemoryStream
        $stderr = New-Object System.IO.MemoryStream
        # Both pipes drain at once, so curl never blocks on a full one.
        $stdoutCopy = $curlProcess.StandardOutput.BaseStream.CopyToAsync($stdout)
        $stderrCopy = $curlProcess.StandardError.BaseStream.CopyToAsync($stderr)
        $curlProcess.WaitForExit()
        $curlClock.Stop()
        [System.Threading.Tasks.Task]::WaitAll(@($stdoutCopy, $stderrCopy))
        $exitCode = $curlProcess.ExitCode
    } finally {
        $curlProcess.Dispose()
    }

    $requests = @()
    if ($null -ne $server) {
        # A connection curl did not open would block the server forever; stopping the
        # listener ends the wait. Give an accepted connection a moment to finish first.
        [void] $serverRun.AsyncWaitHandle.WaitOne(2000)
        Stop-Listener
        $requests = $server.EndInvoke($serverRun)
        if ($server.Streams.Error.Count -gt 0) { throw $server.Streams.Error[0] }
    }
} finally {
    if ($null -ne $tlsRelay) { Stop-TlsRelay -Relay $tlsRelay }
    if ($null -ne $listener) { Stop-Listener }
    if ($null -ne $server) { $server.Dispose() }
    # Reset deletes the key container the PFX import created.
    if ($null -ne $tlsCertificate) { $tlsCertificate.Reset() }
}

$requestBytes = New-Object System.IO.MemoryStream
foreach ($request in $requests) {
    foreach ($bytes in $request) { $requestBytes.Write($bytes, 0, $bytes.Length) }
}

if (-not $NoServer) { [System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'request.bin'), $requestBytes.ToArray()) }
[System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'stdout.bin'), $stdout.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'stderr.txt'), $stderr.ToArray())
[System.IO.File]::WriteAllText((Join-Path $OutDirectory 'exitcode.txt'), [string] $exitCode, [System.Text.Encoding]::ASCII)
if ($Ftp -or $Tftp) {
    [System.IO.File]::WriteAllText((Join-Path $OutDirectory 'transcript.txt'), $transcript.ToString(), [System.Text.Encoding]::GetEncoding(28591))
    [System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'upload.bin'), $uploadedData.ToArray())
}
if ($Smtp -or $Imap -or $Pop3 -or $Raw) {
    [System.IO.File]::WriteAllText((Join-Path $OutDirectory 'transcript.txt'), $transcript.ToString(), [System.Text.Encoding]::GetEncoding(28591))
}

Write-Host "curl exited $exitCode after $($curlClock.ElapsedMilliseconds) ms; fixtures written to $OutDirectory"
