# Surl.Conformance.UnitLibrary

Phase 1 onward; the full push is Phase 6.

The server half of upstream curl's own test suite. Upstream curl 8.21.0 ships 2,013 test
cases in `tests/data` (tag `curl-8_21_0`, counted 2026-09-28); each names a command line,
the server's scripted replies and what to verify. This library parses those cases, plays
each case's replies from Surl while a pinned upstream curl build runs the case's command
line, and checks the case's verify section - so upstream curl's own suite, not one of
Surl's invention, decides whether Surl answers correctly.

Only builds pinned in `UpstreamCurlBuilds.json` run here as the oracle; the Curl port is never
the oracle, a test client of Surl or an expected value (ADR-0003). It runs here only as the thing
measured: in the extended servers' `Integration` tests (ADR-0083) and in Phase 7, beside pinned
upstream curl (ADR-0003 decision 6, ADR-0089; "Phase 7" below).
`UpstreamCurlBuildPins.Parse` reads the pin file and `UpstreamCurlLocator` finds the
pinned build for a platform and role (`Locate`) or for the protocol a test measures
(`LocateForProtocol`: the reference build when it supports the protocol, else the first
supplementary build that does), or refuses any curl whose SHA-256 is not pinned.
Get every curl you run through it.
Each pin has a `Kind`: `Curl` (the default) or `Library`, a shared libcurl such as the
reference build's `libcurl-4.dll` (ADR-0071 decision 10). `Locate`, `LocateForProtocol` and
`RequirePinned` only ever answer with a curl; `LocateLibrary` and `RequirePinnedLibrary` only
with a library, which `Run-LibcurlWebSocketScript.cs` at the repository root loads to drive
`curl_ws_send` and `curl_ws_recv`, and `Run-LibcurlRtspScript.cs` loads to drive RTSP requests
(`CURLOPT_RTSP_REQUEST`) and the interleaved receive (ADR-0074 decision 12); the tests run both
through `PinnedLibcurlDriver`.
`UpstreamCurlRunner` refuses a library. `PinnedLibcurlChoice.Choose` decides which file a
driver may load before anything is loaded; `LibcurlRtspScript`, `LibcurlRtspReport` and
`LibcurlBytes` are the RTSP driver's command line, its output lines and the byte escapes,
and `LibcurlWebSocketScript` (with its `LibcurlWebSocketStep`s), `LibcurlWebSocketFlags` and
`LibcurlWebSocketReport` the WebSocket driver's command line, `CURLWS_*` flag names and output
lines, kept here so they are unit tested while the drivers themselves only call libcurl, and so
the Curl port's twin drivers take the same arguments and print the same lines (ADR-0089
decision 1 (c)).
`UpstreamCurlRunner` takes the locator's `UpstreamCurlLocation` - never a path - so the
only curl it starts is a verified pin; it passes arguments through
`ProcessStartInfo.ArgumentList`, writes the given standard input bytes (none by default)
all at once and closes stdin, as `Record-CurlExchange.ps1` does - after it has started
reading stdout and stderr, and ignoring the `IOException` a curl that exits without reading
its stdin leaves behind (`WriteStandardInputAsync`, BL-884) - sets any environment
variables it is given (the SSH tests point `HOME` and `USERPROFILE` at a temporary directory,
ADR-0051 decision 8), and returns an `UpstreamCurlRunResult`
(exit code, stdout bytes, stderr text), killing curl at its timeout.
The Curl port's client has its own types, never shared with upstream curl's (ADR-0083
decision 2). It runs in the extended servers' `Integration` tests and in Phase 7's
(ADR-0089), and never as an expected value. `CurlPortClientPins.Parse` reads `CurlPortBuilds.json`
(platform, path, sha256, commit, publishCommand, published) into `PinnedCurlPortClient`s;
`CurlPortClientLocator.Locate` expands `%NAME%` variables in the path, never returns a path
`UpstreamCurlBuilds.json` lists, and refuses a file whose SHA-256 is not its pin, or is an
upstream pin's, with `UnpinnedCurlPortClientException`; `CurlPortClientRunner` takes its
`CurlPortClientLocation` and returns a `CurlPortClientRunResult`. A missing client is a
`CurlPortClientUnavailability`, which the tests' `PinnedCurlPortClientOnThisMachine` turns
into an inconclusive test.
Parsing and matching are unit tested with no process and no network; a test that starts
upstream curl is `[TestCategory("Integration")]`. Upstream test data copied into this
repository keeps curl's `COPYING` notice beside it.

## Phase 7: the Curl port beside upstream curl (ADR-0089)

Phase 7 measures the Curl port against Surl beside pinned upstream curl; wherever the two disagree,
upstream is right and the disagreement is the port's defect (ADR-0003 decision 6). Surl never changes
to make the port pass. One case is compared and judged here (BL-578), request bytes included (BL-579):

- `CurlPortMeasurementCase` is one case: the Phase 7 test's name, the test it mirrors, the protocol
  `LocateForProtocol` pairs an upstream build for, curl's arguments with `{surl}/` standing for each
  run's `surl` base URL (`ArgumentsFor`), `surl`'s scheme, options and files, and the mirrored test's
  assertions as `CheckMirroredAssertions`, applied to upstream's run only. `AssertedStandardErrorFragments`
  picks the stderr lines compared (none by default), `ComparedStoredFiles` the uploads compared, and
  `ReleaseDifference` names an upstream release-notes entry (decision 2). `RecordsClientBytes` puts a
  recording relay in front of each `surl`; it defaults to true for the `PlaintextTcpSchemes` and a case
  that turns to TLS part way (`STARTTLS`, `AUTH TLS`) sets it false.
- `ClientByteTranscript` holds the bytes a client sent through the relay, one entry per connection in
  the order the relay accepted them (`BeginConnection`, `Record`, `ToConnections`); thread-safe.
- `CurlPortSurlSide` is what a run's own `surl` shows: its port, its `-v` notes, the stored files the
  case compares, `ClientRequestBytes` (the transcript's connections, compared only when both runs carry
  them) and `RelayPort`; `ClientFacingPort` is the relay's port when there is one, the port the client's
  output and request bytes name.
- `CurlPortMeasuredRun.Of` makes one run from an `UpstreamCurlRunResult` or a `CurlPortClientRunResult`.
- `CurlPortResultNormaliser` is decision 4's normalisation column: the client-facing port as `<port>` (in standard output `surl`'s own port too, when a relay stands between them),
  `Date:` and `Last-Modified:` values as `<date>`, the `-w` values of `%{time_*}` and `%{speed_*}` as
  `<time>` (found by matching the case's `-w` format), stderr cut to the asserted lines, `surl` notes'
  loopback ports and time stamps. `NormaliseRequestBytes` applies the request-byte rules, which the
  internal `RequestBytesNormalisation` holds: the client port in FTP `PORT` and `EPRT`, `User-Agent:` and DICT's `CLIENT` text as
  `<user-agent>`, the 8 random characters after `curl` in an MQTT `CONNECT`'s client identifier as `<client-id>`, every multipart boundary as `<boundary>`, Digest `nonce` (surl's own, echoed), `cnonce` and `response` as `<nonce>`, `<cnonce>`
  and `<response>` (also inside a base64 SASL `DIGEST-MD5` line), and in every NTLM AUTHENTICATE message,
  raw (SMB) or base64 (HTTP, SASL), the LM and NT responses, the encrypted session key and the MIC zeroed;
  `Sec-WebSocket-Key:` as `<websocket-key>`; and, first of all, the internal `WebSocketFrameUnmasking`
  unmasks every client frame after a connection's last WebSocket upgrade request and zeroes its masking
  key (ADR-0089 Amendment 2).
- `CurlPortComparison.Compare` takes the two runners' results - never a path - and gives a
  `CurlPortVerdict` of decision 5's `CurlPortVerdictKind`: `SurlDefect` when upstream's run fails the
  mirrored assertions (the port is not judged), `Agreement` when every compared item agrees,
  `NotComparable` ("release difference") when the case names a `ReleaseDifference` and the paired build
  is not 8.21.0, and otherwise `PortDefect` with a `CurlPortFinding` in decision 6's form: test, mirrored
  test, command line, upstream build (SHA-256, version line), port client (commit, SHA-256), Surl
  commit, both runs and each `CurlPortDifference` (exit code, first differing line, or first differing
  byte with its connection and offset).

A libcurl row (decision 1 (c), BL-935) is judged the same way: `CurlPortLibcurlCase` is a pinned-libcurl
test's step list with `{surl}` standing for each run's authority, its mirrored assertions and the stored
files it compares; `CurlPortLibcurlRun` is one driver run (exit code, report lines, stderr, its
`CurlPortSurlSide`); `CurlPortLibcurlReportNormaliser` masks what varies between two correct runs -
`surl`'s port, a session ID `surl` drew (not one the script set), a send's call count, and the SSRCs and
timestamps of interleaved RTP/RTCP frames but never their body; and `CurlPortLibcurlComparison.Compare`
gives decision 5's `CurlPortVerdict`, reusing `CurlPortComparison`'s internal `AddTextDifference` and
`AddBytesDifference`.

The tests' `PinnedCurlPortBesideUpstreamCurl.MeasureAsync` is the one call each Phase 7 test class
makes: it locates both pinned clients (Inconclusive naming the missing file), runs each against its
own fresh `SurlOnLoopback` with `HOME`, `CURL_HOME` and `USERPROFILE` pointed at an empty folder - through
its own `ClientBytesRelay` when the case `RecordsClientBytes` - is Inconclusive when `CURL_EXTENSIONS` or
`SURL_EXTENSIONS` is set in the test process (decision 3 runs both as shipped), and returns a
`PinnedCurlPortMeasurement`: the verdict and both runs' `CurlPortSurlSide`. `ClientBytesRelay` lives in
the tests because it opens sockets: it forwards both directions unchanged, records only client-to-server
bytes, and shuts down sending to one side when the other stops. An FTP data connection goes straight to
the port `surl` announced, so only the control connection is recorded (decision 4). Reading a family's
findings file is `CurlPortFindingsFile`'s (BL-580): decision 5's pass rule over a family's table. A case whose
mirrored test runs a supplementary build, a relay of its own, several listeners, per-run arguments or standard input passes
a `CurlPortRunSetup`, applied identically to both runs.

Corpus part (b), upstream's own test cases with the port as the client, is built (BL-591, ADR-0089
amendment 7):

- `IUpstreamTestCaseClient` is the runner's client seam: `ExecutablePath` (a case's `%CURL`),
  `Description` and `RunAsync` (arguments, stdin, environment, working directory) giving an
  `UpstreamTestCaseClientRun` (exit code, stdout, stderr; `From` either runner's result).
  `UpstreamCurlTestCaseClient` wraps only an `UpstreamCurlRunner` and `CurlPortTestCaseClient` only a
  `CurlPortClientRunner` (whose `Path` is the verified client's, and whose `RunAsync` takes a working
  directory), so neither client can be made from the other's location. `UpstreamTestCaseRunRecord.FromRun`
  takes either run.
- `CurlVersionReport` is a client's `--version` as runtests reads it: the first line, `Version`
  (`%VERSION`), `VersionNumber` (`%VERNUM`), `Protocols` and `Features`; `Parse` reads a client's
  output and `FromPin` a pin. `UpstreamCurlBuildFeatures.For(CurlVersionReport)` and
  `UpstreamTestCaseSelectionRules.ForClient` derive features and selection from one, so the port runs a
  case with its own version, path and features, as runtests would run it.
- `UpstreamTestCasePortRow` is one case played by both clients with decision 5's `Verdict` (Surl
  defect when upstream did not pass, not comparable when the port skipped, agreement, port defect);
  `UpstreamTestCasePortComparison.Compare` judges the rows against the case numbers
  `ListedFindings` reads from `Documentation/Conformance/CurlPort/Upstream-Test-Cases.md`
  (`Passes`, `DescribeFailures`), and `FormatFamilyTable` writes the per-family counts
  (`UpstreamTestCasePortFamilyCount`).
- The tests' `CurlPortBesideUpstreamCurlOverUpstreamTestCasesTests` (`Integration`, `UpstreamTestCases`)
  plays every case on the platform's passing list twice through `UpstreamTestCasesAgainstSurlTests.CaseRun`
  (`RunBesideAsync`): with the paired upstream build, then with the port, each with fresh server halves
  and its own folder (`port-test<N>` for the port).

## Upstream test cases (ADR-0080)

ADR-0080 decides how Surl plays the server half of upstream curl's test cases; BL-387 to BL-397
build it. Parsing is built (BL-388):

- `UpstreamTestCasePreprocessor.Preprocess` applies FILEFORMAT.md's preprocessing to a case's
  bytes line by line in the order upstream's `prepro` (`tests/runner.pm` at the tag) does:
  CRLF read as LF; `%if`/`%else`/`%endif` (`UpstreamTestCaseConditionals`); variables
  (`UpstreamTestCaseVariables`, upstream's `subvariables` order, an unknown or ungiven `%NAME`
  left as written); `%includetext`; `%SP` `%TAB` `%CR` `%LT` `%GT` `%AMP`; `%b64[]`, `%hex[]`,
  `%repeat[]`, `%days[]` (to `%alternatives[a,b]`), `%include`, `%sha256b64file[]`,
  `%strippemfile[]` (`UpstreamTestCaseInstructions`); then `crlf=` inside `<data*>` and
  `<connect*>` (`UpstreamTestCaseNewlines`, upstream's `subnewlines`, double-quoted values
  only, as upstream matches). Its `UpstreamTestCaseEnvironment` carries the variables, the
  features, an `IUpstreamTestCaseFileReader` for included files and a `TimeProvider`. Text is
  Latin-1 throughout, so every byte survives. Each output line keeps the source line it came
  from.
- `UpstreamTestCaseParser.Parse` reads the lines into an `UpstreamTestCase`: sections
  (`info`, `reply`, `client`, `disable`, `verify`), each with its parts and its loose `Lines`;
  each `UpstreamTestCasePart` with its attributes, its `Content` and
  `ContentWithNewlineAttributes(UpstreamTestCaseNewlineRule)` (`nonewline` and `crlf` in the
  order and form runtests uses for that kind of part, set by Perl truthiness). A missing
  `%include` file includes as nothing, as upstream's does. Tags are matched by their start
  as upstream's `getpart` matches them; anything `getpart` would silently lose - a part outside
  every section, a part or section left open, a stray closing tag - is an
  `UpstreamTestCaseFormatException` naming the source line. `mode="text"` and the `<upload>`
  double cut are verification rules, applied by the verify checker below.
- `Surl.Conformance.UnitTests`' `UpstreamTestCaseCorpusTests` parses all 2,013 imported cases;
  4 (758, 1569, 1570, 1614) are listed there with their reasons: each has a part outside every
  section.

Selection is built (BL-389):

- `UpstreamTestCaseServers` is decision 2's table: each `<server>` name's `UpstreamTestCaseServer`
  row - its scheme and whether its server half is built - and `NoServerHalf` (`file`, `none`).
- `UpstreamCurlBuildFeatures.For(pin)` derives a pin's features as runtests'
  `checksystemfeatures` does: the `Features:` words runtests reads (`PinnedUpstreamCurlBuild.Features`,
  from the pin's `features` line), the TLS backend and libraries of the version line, `win32`,
  `threaded-resolver`, `http/2`, each protocol with its `-ipv6` and `-unix` forms, and
  `ReleaseBuildOptions`, the `curlinfo` options a default release build enables (probed on the
  Windows reference pin, BL-389's Notes). The tests' `UpstreamCurlShowsItsBuildOptionsTests`
  (`Integration`) probes every one of them and the platform options `For` claims with the
  reference pin of the platform it runs on, so CI's Linux and macOS conformance steps probe theirs
  (BL-598); `NotShownByTheTool` names the options no curl command can show, and why. Never `Debug`, `TrackMemory` or `unittest`.
- `UpstreamTestCaseDisabledList.Parse` reads `DISABLED`, its `%if` lines against those features.
- `UpstreamTestCaseSelector.FindSkipReason(number, case)` gives the first of decision 3's
  `UpstreamTestCaseSkipReasons` that applies, or `null` when the case can run, under
  `UpstreamTestCaseSelectionRules`. `ForBuild(pin, disabled, testServerChoices)` is Surl as it stands: the server
  commands `UpstreamTestCaseServerCommands` lists for each built server half, the strip lines
  the verify checker interprets, and the cases the exclusions file lists. `UpstreamTestCaseServerCommands` has one row per server name,
  the same words the protocol library's server half accepts (a test pins the HTTP row to
  `HttpUpstreamTestCaseReplies.SupportedServerCommands`); a server half that lands adds its row. A
  case with a `<postcmd>` asks for `postcmd`, which no row has, so it skips
  `servercmd-unsupported:postcmd`. Parse a case for selection with the pin's features and
  without `SRCDIR` or `PERL`, so `needs-perl` still sees them.
- `UpstreamTestCaseCorpusTests` writes the count per skip reason for each reference pin to the
  test log.

Verify checking is built (BL-390):

- `UpstreamTestCaseVerifyChecker(files, features).Check(case, run)` checks a case's `<verify>`
  against an `UpstreamTestCaseRunRecord` (exit code, stdout, stderr, the `--output` file, and the
  bytes the server half received, stored as an upload, or a proxy received; `FromRun` builds one
  from an `UpstreamCurlRunResult`) in `singletest_check`'s order: `stdout`, `stderr`, `protocol`,
  the reply's `data`/`datacheck`, `upload`, `proxy`, `file` to `file4`, `errorcode`, `notexists`.
  It gives an `UpstreamTestCaseVerifyResult`: pass, or the first differing section and its first
  differing line on each side, escaped by `Escape` (NFR-020). Each section applies `nonewline`,
  `crlf`, `mode="text"` (both sides CRLF), `%EMPTY`, `loadfile`, `%alternatives[a,b]` and the strips
  where and in the order runtests does; `<stderr mode="warn">` always passes, as runtests' `join`
  makes it. Files come through `IUpstreamTestCaseRunFiles` (`FileSystemUpstreamTestCaseRunFiles`).
- `UpstreamTestCaseStripLine` is decision 4's Perl subset as amended in BL-390 (`s///` with `g`
  and `i`, `or $_ = ''`, `$_ = '' if` with `UpstreamTestCaseStripCondition`), and
  `UpstreamTestCasePerlRegex` makes .NET regexes that match as Perl does on bytes.
  `UpstreamTestCaseStripLine.IsSupported` is `ForBuild`'s `SupportsStripLine`; a line outside the
  subset makes `Check` throw `NotSupportedException`, since the selector skips that case first.
- `UpstreamTestCaseCorpusTests` lists every strip line at the tag outside the subset with its
  cases: only test 1286's.

Running the http and https cases is built (BL-392):

- `UpstreamTestCaseCommandLine.Build(number, case, logDirectory)` is what runtests runs
  (`singletest_prepare`, `singletest_setenv`, `singletest_run` in `tests/runner.pm` at the tag):
  `-q`, `--output %LOGDIR/curl<N>.out`, `--include`, `--trace-ascii` (or `--trace`) and
  `--trace-time` as the command's `option` attribute and `<verify><stdout>` decide, then the
  command split by `UpstreamTestCaseShellWords` (POSIX shell quoting, no expansion); `<stdin>`'s
  bytes, the `<file>` to `<file4>` files to create, `<setenv>`'s variables (a removal sets the
  variable empty), and the `stdout<N>`/`stderr<N>` files runtests redirects curl into.
- `UpstreamTestCaseResult` is one line of decision 5's results file (`test<N> pass`,
  `test<N> fail <section>`, `fail timeout`, `test<N> skip <reason>`), `Format` the file's text in
  number order and `FileNameFor(platform)` its name, `upstream-test-case-results.<platform>.txt`.
- `UpstreamCurlRunner.RunAsync` takes a working directory, so a case's relative paths are
  relative to its own folder, as they are to runtests'.
- `Surl.Conformance.UnitTests`' `UpstreamTestCasesAgainstSurlTests` (`Integration`,
  `UpstreamTestCases`) runs every case on the reference build, each with its own
  `UpstreamTestCaseServerHalves` (HTTP in plaintext and over TLS, the TLS listener on the `wss`
  scheme so ALPN offers only `http/1.1`; FTP on `127.0.0.1` and `[::1]` and over implicit TLS for
  `ftps`, with socket data connections and the accept-all `AnonymousAuthenticationPolicy`;
  Gopher on `127.0.0.1` and `[::1]` and over TLS for `gophers`; MQTT in plaintext and over TLS;
  TFTP on a datagram listener; and Surl's own DICT and TELNET servers, whose cases give them
  nothing to send), its own folder as curl's working directory, `%PWD` and `HOME`, `%LOGDIR` the
  relative `log`, and a 60-second limit; a case naming a server other than `http`, `https`, `ftp`,
  `ftp-ipv6`, `ftps`, `gopher`, `gopher-ipv6`, `gophers`, `mqtt`, `mqtts`, `tftp`, `dict`, `scp`, `sftp`, `smb`,
  `telnet`, `imap`, `pop3` and `smtp` is written `skip server-half-not-composed:<name>` and an unparsable one
  `skip unparsable`. After curl exits the run waits up to 2 seconds for every exchange to end, so a
  server half has read curl's last packet before its log is checked. A case's `<protocol>` is
  checked against each server half's received bytes in the order the case names its servers, and
  `<upload>` against the FTP, TFTP and mail server halves' uploads. CI's
  `Upstream test cases` job runs it on Linux and macOS and uploads the results file and the
  run's passing list (both are written to the test assembly's folder).

Playing the ftp, ftp-ipv6 and ftps cases is built (BL-394): `UpstreamTestCaseServerCommands.Ftp`
is the FTP row, the same words as `Surl.Protocol.Ftp`'s
`FtpUpstreamTestCaseReplies.SupportedServerCommands` (`Testnum`, `REPLY`, `REPLYLF`, `COUNT`,
`RETRNOSIZE`, `RETRSIZE`, `NOSAVE`, `STOR`); `DELAY`, `RETRWEIRDO`, `SLOWDOWN`, `SLOWDOWNDATA`,
`PASVBADIP` and the `NODATACONN` forms skip `servercmd-unsupported:<command>`.

Playing the dict, gopher, gopher-ipv6, gophers, mqtt, mqtts, telnet and tftp cases is built
(BL-396): `UpstreamTestCaseServerCommands.Tftp` is the TFTP row (`Testnum`, `writedelay:`, the same
words as `TftpUpstreamTestCaseReplies.SupportedServerCommands`), and `UpstreamTestCaseServerCommands.None`
the row of the Gopher, MQTT, DICT and TELNET server halves, which implement no command: MQTT's
`error-CONNACK`, `remlen-CONNACK`, `PUBLISH-before-SUBACK`, `short-PUBLISH`,
`PINGRESP-as-CONNACK`, `DISCONNECT-malformed` and `excessive-remaining` skip
`servercmd-unsupported:<command>`. Every `rtsp` case at the tag is a libtest, so no RTSP server
half is composed.

Playing the scp, sftp and smb cases is built (BL-397): their rows in
`UpstreamTestCaseServerCommands` are `None`, since FILEFORMAT gives these servers no commands. The
server halves are Surl's own servers, made by the tests' `UpstreamTestCaseFileServers`:

- SSH on `%SSHPORT` serves the case's folder as its root, uploads, listings, dot files and links all
  allowed, so `%SFTP_PWD` and `%SCP_PWD` are empty (`sftp://host:port%SFTP_PWD/%LOGDIR/f` is
  `/log/f`). Its logins are `UpstreamTestCaseSshLoginPolicy`'s, as upstream's `sshserver.pl`
  configures sshd: only `%USER` with the run's client key; `none`, passwords, other keys and other
  users are refused, so the invalid-user and missing-key cases end in curl's 67.
- `%USER` is the run's user name without the characters an unquoted `-u %USER:` would split
  (`SshLoginNameFor`: "First Last" is `FirstLast`); only SCP and SFTP cases name it.
- One RSA host key per run gives `%SSHSRVMD5` (32 lower-case hex digits), `%SSHSRVSHA256` (base64,
  no padding, as `ssh-keygen -l` writes it) and `%SSHKEYALGO` (`ssh-rsa`); one RSA client key pair
  per run is written to each SSH case's `%LOGDIR/server/curl_client_key` (PKCS #1 PEM) and `.pub`.
- An SCP or SFTP upload lands in the case's folder, so `<upload>` is checked against
  `%LOGDIR/upload.<N>`, the file runtests reads.
- SMB on `%SMBPORT` serves a `TESTS` share holding the case's `<data>` as the file `<N>`, as
  upstream's `smbserver.py` answers `smb://host/TESTS/<N>`, to any login. An smb case runs with the
  build `UpstreamCurlLocator.LocateForProtocol` pairs with `smb`: the reference build where it has
  smb, else (Windows) the supplementary build ADR-0030 pinned, selected under that build's features.

Playing the imap, pop3 and smtp cases is built (BL-879): `UpstreamTestCaseServerCommands.Mail` is
their row, the same words as `Surl.LineProtocol`'s `MailUpstreamTestCaseReplies.SupportedServerCommands`
(`Testnum`, `REPLY`, `REPLYLF`, `COUNT`, `CAPA`, `AUTH`, `POSTFETCH`); `DELAY`, `SLOWDOWN`,
`SLOWDOWNDATA` and `NOSAVE` skip `servercmd-unsupported:<command>`. The tests' server halves add
`ImapUpstreamTestCaseServer`, `Pop3UpstreamTestCaseServer` and `SmtpUpstreamTestCaseServer` in
plaintext on `127.0.0.1` and `[::1]` (`%IMAPPORT`, `%POP3PORT`, `%SMTPPORT` and their `6` forms),
sharing one `MailUpstreamTestCaseReplies` per case built from its `<data*>` parts, with logins
going to the accept-all `AnonymousAuthenticationPolicy`; `<protocol>` reads its `ReceivedBytes` and
`<upload>` its `UploadedBytes`. `imaps`, `pop3s` and `smtps` are not composed.

Playing the http-proxy and https-proxy cases is built (BL-556): `UpstreamTestCaseServerCommands.HttpProxy` is their
row (`Testnum`, the same words as `Surl.Protocol.HttpProxy`'s `HttpProxyUpstreamTestCaseReplies.SupportedServerCommands`),
and `UpstreamTestCaseServer.PortVariable` names `%PROXYPORT` and `%HTTPSPROXYPORT`. The tests' server halves add
`HttpProxyUpstreamTestCaseServer` on the `http-proxy` scheme (plaintext) and on `https` with `ListenUrl.IsProxy` (behind TLS),
tunnelling through `Surl.Networking`'s `SocketOriginConnector` limited to loopback; `<proxy>` reads the proxy's record and
`<protocol>` the origin half's. 1230 skips `server-half-not-composed:http-ipv6`.

Playing the socks4, socks5 and socks5unix cases is built (BL-562): `UpstreamTestCaseServerCommands.Socks` is their row (`Testnum`, `response`, `method`, `user`, `password`, `backendport`, the same words as `Surl.Protocol.Socks`'s `SocksUpstreamTestCaseReplies.SupportedServerCommands`), and the `socks4` and `socks5` rows name `%SOCKSPORT` (the run gives the SOCKS4 listener's port to a case naming `socks4`, else the SOCKS5 one's); `socks5unix` keeps `%SOCKSUNIXPATH`. 1467, 1468 and 1470 need `!win32` and run on the Linux and macOS pins only.

Playing the dns cases is built (BL-570, ADR-0123): the `dns` row of `UpstreamTestCaseServers` says built and
names `%DNSPORT`, and `UpstreamTestCaseServerCommands.Dns` (`Testnum`, `A`, `AAAA`, the same words as
`Surl.Extended.Protocol.DnsQuery`'s `DnsQueryUpstreamTestCaseReplies.SupportedLineNames` plus `Testnum`) is its
row. The selector reads the names of a case's `<reply><dns>` lines (the text before the colon) as server commands, so
`HTTPS` and the `Delay-` lines skip `servercmd-unsupported:<name>`. The tests' server halves add a `DnsUpstreamTestCaseServer`
on a datagram listener for `%DNSPORT`. Cases 2102, 2103 and 2104 need `override-dns`, which no pinned
build lists, so each skips `feature-missing:override-dns`; the server half is proven by its unit tests and
runs for real if a debug build is ever pinned. The `<verify><dns>` check is not in `UpstreamTestCaseVerifyChecker` yet.

The upstream test certificates are generated (BL-682): `UpstreamTestCertificateDescription.Parse`
reads a `tests/certs/*.prm` file (subject, `DNS:` or `DER:` subject alternative name, key usage,
extended key usage, `CA:true`; the `authorityInfoAccess` and `crlDistributionPoints` extensions are
not read) and `UpstreamTestCertificateGenerator.Generate` makes, with ECDSA P-256 keys and
SHA-256, the authority `test-ca` (`.crt`, `.cacert`, `.key`) and for every other description `.crt`,
`.der`, `.key`, `.pem` (key then certificate), `.pub.der`, `.pub.pem` and `.crl` (revoking it), all
as bytes. `UpstreamTestCasesAgainstSurlTests` writes them under the run's `%CERTDIR/certs/` and
serves `https` with `test-localhost`.

The stated pass rate and its guard are built (BL-393):

- `UpstreamTestCasePassingList` reads and writes a platform's passing list,
  `Documentation/Conformance/upstream-test-cases-passing.<platform>.txt` (`FileNameFor`), one
  case number per line, ascending.
- `UpstreamTestCasePassingListComparison.Compare(platform, list, results)` is the guard:
  `Regressions` are the listed cases that did not pass (`DescribeRegressions` names each and how
  it ended), `NewlyPassing` the passing cases not listed (`DescribeNewlyPassing` gives the lines
  to add). The suite asserts no regressions and writes the newly passing to its log.
- `UpstreamTestCasePassRateFile.Parse` reads `Upstream-Test-Case-Pass-Rate.md`'s `## Current` and
  `## Releases` tables into `UpstreamTestCasePassRateRow`s (platform, version, passed or
  `not run`, and the `test-server-choice` count where the table has that column);
  `FindFallingReleases` names each fall of a platform's count between releases.
- `UpstreamTestCaseExclusions.Parse` reads decision 5's exclusion list,
  `Upstream-Test-Case-Exclusions.md`: the `Case` column (`test<N>` or `<N>`) of its first table
  with that column, into the set every run passes to `ForBuild` or `ForClient` as
  `TestServerChoices` (BL-689); a malformed or repeated case, a ragged row or no such table is a
  `FormatException`.
  `Surl.Conformance.UnitTests`' `UpstreamTestCasePassRateStatementTests` (fast) holds each current
  row's Passed to its list's line count and its test-server-choice to the exclusions' row count,
  no excluded case to a passing list, and the releases to never falling;
  `UpstreamTestCaseCorpusTests` holds every excluded case to reaching decision 5's check on every
  reference pin, and the upstream test-case run its own test-server-choice count to the row count.

The rest is intent until its task lands. This library will also hold, with no reference but
Abstractions:
- each `<server>` name's port variable, beside its `UpstreamTestCaseServers` row;
- the SSH keys and `%SSHSRVMD5`/`%SSHSRVSHA256`.

It does not hold the server halves: each protocol library carries its own optional seam for a
case's reply, and `Surl.Conformance.UnitTests` composes it. It hands a reply over as BCL types
only (parts by name as bytes, `<servercmd>` lines as strings). The data itself is not here: it is
`Surl.Conformance.UnitTests/UpstreamTestCases/`, with curl's `COPYING` and `SHA256SUMS`.
