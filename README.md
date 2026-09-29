# Surl

[![Surl's history, animated with Gource - open the 8K viewer](https://raw.githubusercontent.com/StewartScottRogers/Surl/gource/gource.gif)](https://stewartscottrogers.github.io/Surl/)

[![Code coverage](https://raw.githubusercontent.com/StewartScottRogers/Surl/gource/coverage/badge.svg)](https://stewartscottrogers.github.io/Surl/coverage/)

The animation is Surl's history across every branch, re-rendered by
`.github/workflows/gource.yml` whenever the repository moves; click it for the 8K viewer.
The badge links to the coverage report, measured on each render against the quality gates.

Surl ("Server URL") is the server-side mate of [curl](https://curl.se): for every request
upstream curl can make, protocol for protocol, Surl is the server that answers it. It is
written in C# on .NET 10, publishes as a single native ahead-of-time (AOT) executable named
`surl`, and depends on nothing but the .NET base class library.

Its command line mirrors curl's. Where `curl [options] <url>` names what to fetch,
`surl [options] <url>` names what to listen on: the scheme picks the protocol, the host
and port pick the bind address, and several URLs mean several listeners at once.

```
surl https://0.0.0.0:8443/ --cert server.pem --key server.key
curl https://localhost:8443/readme.md --cacert server.pem
```

That is the intent. **Today `surl` serves one protocol**: `surl http://127.0.0.1:8080/`
answers `GET` and `HEAD` for the files of the current directory (or `--directory <dir>`)
over HTTP/1.1 until Ctrl+C. Every other scheme, `https` included, is refused with exit
code 1 until its server lands.

## Upstream curl validates Surl; Surl later validates the Curl port

Surl is measured against **upstream curl** - the original C implementation at
[github.com/curl/curl](https://github.com/curl/curl), release 8.21.0 - and nothing else.
A Surl behaviour is right when a pinned upstream curl build completes the exchange
against it. Each build is pinned by path and SHA-256 in
[`UpstreamCurlBuilds.json`](UpstreamCurlBuilds.json), because a bare `curl` on a command
line can be any number of other programs.

Once Surl stands on its own, it becomes the instrument that measures
[the Curl port](https://github.com/StewartScottRogers/Curl), a C# port of curl: the port
runs the same conversations against Surl beside upstream curl, and wherever the two
disagree, upstream is right. The port is never used to validate Surl, so that check can
never come out circular. See
[ADR-0003](Documentation/Planning/Decisions/ADR-0003-upstream-curl-is-surls-only-oracle.md).

## Built by a dark factory

Surl is built the way the Curl port is: by a *dark factory* - an unattended production
line of Claude Code agents shaped for this one job, not a general-purpose coding bot.

1. **Plan.** The work is decomposed into small tasks on the [task board](Tasks/README.md),
   one Markdown file per task, each with dependencies and checkable acceptance criteria.
2. **Run.** `RunDarkFactory.cmd` takes the next ready task and hands it to a headless
   Claude Code run that is not allowed to ask a question. It repeats until nothing is
   ready or the shift ends.
3. **Specialise.** Each run uses agents shaped for a server mate of curl, in
   `.claude/agents`: a protocol architect and implementer, a test writer, a build fixer, a
   code reviewer, a coverage auditor, and a conformance auditor that points pinned
   upstream curl at Surl and checks every exchange.
4. **Gate.** Nothing lands unless it builds with warnings as errors and holds every
   library to 100% line and branch coverage, cyclomatic complexity of at most 10 and a
   Change Risk Anti-Patterns (CRAP) score of at most 30.
5. **Escalate.** Anything that needs a human decision is moved to `Blocked` with the
   question written down, and the shift ends with an alarm until someone answers it.

The lights stay off; a person sets direction and answers blocked questions. The Gource
video of the project's history and the published coverage report will appear here once
the repository is on GitHub and `.github/workflows/gource.yml` has rendered them.

## Build and test

Needs the .NET 10 SDK. Builds and tests on Windows, Linux and macOS.

```
dotnet build
dotnet test --filter "TestCategory!=Integration"
```

Run a factory shift (Windows):

```
RunDarkFactory.cmd -Hours 4 -MaxTasks 3
```

## Read more

- [Product overview](Documentation/Product/Product-Overview.md) - scope, architecture, phases and the oracle
- [Task board](Tasks/README.md) - what is being worked on, one Markdown file per task
- [Decisions](Documentation/Planning/Decisions/README.md) - every architecture decision record
- [Download](DOWNLOAD.md) - no release yet

## Licence

See [LICENSE.txt](LICENSE.txt).
