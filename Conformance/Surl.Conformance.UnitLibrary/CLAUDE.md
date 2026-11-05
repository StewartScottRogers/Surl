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
kept here so they are unit tested while the driver itself only calls libcurl.
`UpstreamCurlRunner` takes the locator's `UpstreamCurlLocation` - never a path - so the
only curl it starts is a verified pin; it passes arguments through
`ProcessStartInfo.ArgumentList`, writes the given standard input bytes (none by default)
all at once and closes stdin, as `Record-CurlExchange.ps1` does, sets any environment
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
to make the port pass. One case is compared and judged here (BL-578):

- `CurlPortMeasurementCase` is one case: the Phase 7 test's name, the test it mirrors, the protocol
  `LocateForProtocol` pairs an upstream build for, curl's arguments with `{surl}/` standing for each
  run's `surl` base URL (`ArgumentsFor`), `surl`'s scheme, options and files, and the mirrored test's
  assertions as `CheckMirroredAssertions`, applied to upstream's run only. `AssertedStandardErrorFragments`
  picks the stderr lines compared (none by default), `ComparedStoredFiles` the uploads compared, and
  `ReleaseDifference` names an upstream release-notes entry (decision 2).
- `CurlPortSurlSide` is what a run's own `surl` shows: its port, its `-v` notes, the stored files the
  case compares, and `ClientRequestBytes`, the bytes per plaintext connection, which BL-579's relay
  fills in; until both runs carry them they are not compared.
- `CurlPortMeasuredRun.Of` makes one run from an `UpstreamCurlRunResult` or a `CurlPortClientRunResult`.
- `CurlPortResultNormaliser` is decision 4's normalisation column: the `surl` port as `<port>`,
  `Date:` and `Last-Modified:` values as `<date>`, the `-w` values of `%{time_*}` and `%{speed_*}` as
  `<time>` (found by matching the case's `-w` format), stderr cut to the asserted lines, `surl` notes'
  loopback ports and time stamps, and the `surl` port in request bytes (BL-579 adds the other request
  byte rules).
- `CurlPortComparison.Compare` takes the two runners' results - never a path - and gives a
  `CurlPortVerdict` of decision 5's `CurlPortVerdictKind`: `SurlDefect` when upstream's run fails the
  mirrored assertions (the port is not judged), `Agreement` when every compared item agrees,
  `NotComparable` ("release difference") when the case names a `ReleaseDifference` and the paired build
  is not 8.21.0, and otherwise `PortDefect` with a `CurlPortFinding` in decision 6's form: test, mirrored
  test, command line, upstream build (SHA-256, version line), port client (commit, SHA-256), Surl
  commit, both runs and each `CurlPortDifference` (exit code, first differing line, or first differing
  byte with its connection and offset).

The tests' `PinnedCurlPortBesideUpstreamCurl.MeasureAsync` is the one call each Phase 7 test class
makes: it locates both pinned clients (Inconclusive naming the missing file), runs each against its
own fresh `SurlOnLoopback` with `HOME`, `CURL_HOME` and `USERPROFILE` pointed at an empty folder, is
Inconclusive when `CURL_EXTENSIONS` or `SURL_EXTENSIONS` is set in the test process (decision 3 runs
both as shipped), and returns the verdict. Reading a family's findings file is the family tasks' work
(BL-580 on).

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
  double cut are verification rules and belong to BL-390.
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
  Windows reference pin, BL-389's Notes). Never `Debug`, `TrackMemory` or `unittest`.
- `UpstreamTestCaseDisabledList.Parse` reads `DISABLED`, its `%if` lines against those features.
- `UpstreamTestCaseSelector.FindSkipReason(number, case)` gives the first of decision 3's
  `UpstreamTestCaseSkipReasons` that applies, or `null` when the case can run, under
  `UpstreamTestCaseSelectionRules`. `ForBuild(pin, disabled)` is Surl as it stands: no server
  command and no strip line accepted; the server halves and the verify checker replace those
  predicates with `with` as they land. Parse a case for selection with the pin's features and
  without `SRCDIR` or `PERL`, so `needs-perl` still sees them.
- `UpstreamTestCaseCorpusTests` writes the count per skip reason for each reference pin to the
  test log.

The rest is intent until its task lands. This library will also hold, with no reference but
Abstractions:
- each `<server>` name's port variable, beside its `UpstreamTestCaseServers` row;
- building each command line as upstream's runtests does (`-q`, `--output`, `--include`,
  `--trace-ascii`);
- generating the test certificates from the imported `.prm` files with `CertificateRequest`, and
  the SSH keys and `%SSHSRVMD5`/`%SSHSRVSHA256`;
- checking a `<verify>` section, with the `<strip>`/`<strippart>`/`<stripfile>` interpreter of
  ADR-0080 decision 4's Perl subset (nothing outside it: `strippart-unsupported`);
- the per-run result lines and reading the pass-rate file and passing lists of decision 5.

It does not hold the server halves: each protocol library carries its own optional seam for a
case's reply, and `Surl.Conformance.UnitTests` composes it. It hands a reply over as BCL types
only (parts by name as bytes, `<servercmd>` lines as strings). The data itself is not here: it is
`Surl.Conformance.UnitTests/UpstreamTestCases/`, with curl's `COPYING` and `SHA256SUMS`.
