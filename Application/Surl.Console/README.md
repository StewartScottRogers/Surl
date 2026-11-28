# Surly.Surl.Console

`surl`, the server-side mate of curl: for each request upstream curl makes, `surl` is the server
that answers it. Where `curl [options] <url>` names what to fetch, `surl [options] <url>` names
what to listen on.

## Install it

This is the .NET tool package of the `surl` command line. It needs the .NET 10 SDK.

```
dotnet tool install -g Surly.Surl.Console            # installs the surl command
dotnet dnx Surly.Surl.Console -- <options> <url>     # or runs surl without that install
```

`surl`'s arguments go after `--`: `dnx` takes options such as `--version` and `--help` for itself.

A script or a test harness should install the tool rather than run it through `dnx`: run from
`cmd.exe`, `dnx` exited 0 when `surl` exited 2 (measured on Windows with the .NET 10.0.404 SDK).

The [GitHub releases](https://github.com/StewartScottRogers/Surl/releases) carry `surl` as a
native binary that needs no .NET.

## Use it

```
surl http://127.0.0.1:8080/
```

serves HTTP on the loopback address until it is stopped with Ctrl+C or SIGTERM. From another
shell:

```
curl http://127.0.0.1:8080/
```

`surl --help` lists the options and `surl --manual` describes them. `surl --aihelp` is the same
reference written for an AI agent. `surl --version` names the schemes the build serves.

To start the same servers inside a .NET test suite, use the `Surly.Surl.Api` package; for an AI
agent, the `Surly.Surl.Mcp` package. Source, documents and licence (MIT) are at
https://github.com/StewartScottRogers/Surl.
